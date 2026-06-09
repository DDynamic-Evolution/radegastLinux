using System;
using System.Buffers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Protocol;

namespace Radegast.Veles.MQTT;

public sealed class MqttManager : IDisposable
{
    private readonly MqttClientFactory _factory = new();
    private IMqttClient? _client;
    private MqttConfig _config = new();
    private CancellationTokenSource? _cts;

    public MqttConfig Config
    {
        get => _config;
        set
        {
            _config = value ?? new MqttConfig();
            ApplyClientId();
        }
    }

    public bool IsConnected => _client?.IsConnected ?? false;

    public event EventHandler<bool>? ConnectionStateChanged;

    public event EventHandler<MqttCommandEventArgs>? CommandReceived;

    public MqttManager()
    {
        ApplyClientId();
    }

    private void ApplyClientId()
    {
        if (string.IsNullOrWhiteSpace(_config.ClientId))
            _config.ClientId = $"radegast-{Guid.NewGuid():N}"[..20];
    }

    public async Task ConnectAsync()
    {
        if (_client != null)
        {
            await DisconnectAsync();
            _client.Dispose();
        }

        _cts = new CancellationTokenSource();
        _client = _factory.CreateMqttClient();

        _client.ConnectedAsync += OnConnectedAsync;
        _client.DisconnectedAsync += OnDisconnectedAsync;
        _client.ApplicationMessageReceivedAsync += OnMessageReceivedAsync;

        var builder = new MqttClientOptionsBuilder()
            .WithTcpServer(_config.Host, _config.Port)
            .WithClientId(_config.ClientId);

        if (!string.IsNullOrWhiteSpace(_config.Username))
            builder.WithCredentials(_config.Username, _config.Password);

        if (_config.UseTls)
            builder.WithTlsOptions(o => o.UseTls());

        builder.WithCleanStart();

        try
        {
            await _client.ConnectAsync(builder.Build(), _cts.Token);

            if (_config.SubscribeCommands)
            {
                var topic = $"{_config.RootTopic}/cmd/+";
                await _client.SubscribeAsync(new MqttTopicFilterBuilder()
                    .WithTopic(topic)
                    .WithQualityOfServiceLevel((MqttQualityOfServiceLevel)_config.Qos)
                    .Build());
            }
        }
        catch
        {
            ConnectionStateChanged?.Invoke(this, false);
        }
    }

    public async Task DisconnectAsync()
    {
        if (_client == null) return;

        _cts?.Cancel();

        try
        {
            if (_client.IsConnected)
            {
                await _client.DisconnectAsync(new MqttClientDisconnectOptions
                {
                    Reason = (MqttClientDisconnectOptionsReason)MqttClientDisconnectReason.NormalDisconnection
                });
            }
        }
        catch { }

        _client.ConnectedAsync -= OnConnectedAsync;
        _client.DisconnectedAsync -= OnDisconnectedAsync;
        _client.ApplicationMessageReceivedAsync -= OnMessageReceivedAsync;

        _cts?.Dispose();
        _cts = null;
    }

    public async Task PublishAsync(string relativeTopic, string payload)
    {
        if (_client == null || !_client.IsConnected) return;

        var topic = string.IsNullOrWhiteSpace(_config.RootTopic)
            ? relativeTopic
            : $"{_config.RootTopic}/{relativeTopic}";

        try
        {
            await _client.PublishAsync(new MqttApplicationMessage
            {
                Topic = topic,
                Payload = new(Encoding.UTF8.GetBytes(payload ?? string.Empty)),
                QualityOfServiceLevel = (MqttQualityOfServiceLevel)_config.Qos,
                Retain = false
            }, _cts?.Token ?? CancellationToken.None);
        }
        catch { }
    }

    private Task OnConnectedAsync(MqttClientConnectedEventArgs args)
    {
        ConnectionStateChanged?.Invoke(this, true);
        return Task.CompletedTask;
    }

    private Task OnDisconnectedAsync(MqttClientDisconnectedEventArgs args)
    {
        ConnectionStateChanged?.Invoke(this, false);
        return Task.CompletedTask;
    }

    private Task OnMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs args)
    {
        var topic = args.ApplicationMessage.Topic;
        var raw = args.ApplicationMessage.Payload;
        var payload = raw.Length > 0
            ? Encoding.UTF8.GetString(raw.ToArray())
            : string.Empty;

        CommandReceived?.Invoke(this, new MqttCommandEventArgs(topic, payload));
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();

        if (_client != null)
        {
            if (_client.IsConnected)
            {
                try
                {
                    _client.DisconnectAsync(new MqttClientDisconnectOptions
                    {
                Reason = (MqttClientDisconnectOptionsReason)MqttClientDisconnectReason.NormalDisconnection
                    }).GetAwaiter().GetResult();
                }
                catch { }
            }

            _client.ConnectedAsync -= OnConnectedAsync;
            _client.DisconnectedAsync -= OnDisconnectedAsync;
            _client.ApplicationMessageReceivedAsync -= OnMessageReceivedAsync;
            _client.Dispose();
        }
    }
}

public class MqttCommandEventArgs : EventArgs
{
    public string Topic { get; }
    public string Payload { get; }

    public MqttCommandEventArgs(string topic, string payload)
    {
        Topic = topic;
        Payload = payload;
    }
}
