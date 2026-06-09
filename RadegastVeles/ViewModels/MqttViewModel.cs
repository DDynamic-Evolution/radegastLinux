using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenMetaverse;
using Radegast.Veles.Core;
using Radegast.Veles.MQTT;

namespace Radegast.Veles.ViewModels;

public partial class MqttViewModel : ObservableObject, IDisposable
{
    private readonly RadegastInstanceAvalonia _instance;
    private readonly MqttManager _manager;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private bool _isConnecting;

    [ObservableProperty]
    private string _host = "localhost";

    [ObservableProperty]
    private int _port = 1883;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _rootTopic = "radegast";

    [ObservableProperty]
    private int _qos = 1;

    [ObservableProperty]
    private bool _useTls;

    [ObservableProperty]
    private bool _publishChatSend = true;

    [ObservableProperty]
    private bool _publishChatReceive = true;

    [ObservableProperty]
    private bool _publishImSend = true;

    [ObservableProperty]
    private bool _publishImReceive = true;

    [ObservableProperty]
    private bool _publishLocation;

    [ObservableProperty]
    private bool _subscribeCommands = true;

    [ObservableProperty]
    private bool _autoConnect;

    public MqttManager Manager => _manager;

    public MqttViewModel(RadegastInstanceAvalonia instance, MqttManager manager)
    {
        _instance = instance;
        _manager = manager;

        // Load persisted config
        var savedJson = _instance.GlobalSettings["mqtt_config"]?.AsString();
        if (!string.IsNullOrWhiteSpace(savedJson))
        {
            try
            {
                var cfg = JsonSerializer.Deserialize<MqttConfig>(savedJson);
                if (cfg != null)
                {
                    _manager.Config = cfg;
                    ApplyConfigToProperties();
                }
            }
            catch { }
        }

        _manager.ConnectionStateChanged += OnConnectionStateChanged;
        _instance.NetCom.ClientLoginStatus += NetCom_ClientLoginStatus;
    }

    private void NetCom_ClientLoginStatus(object? sender, LoginProgressEventArgs e)
    {
        if (e.Status != LoginStatus.Success) return;
        TryAutoConnect();
    }

    private async void TryAutoConnect()
    {
        if (AutoConnect && !string.IsNullOrWhiteSpace(_host) && _host != "localhost")
        {
            try
            {
                IsConnecting = true;
                await _manager.ConnectAsync();
            }
            catch
            {
                IsConnecting = false;
            }
        }
    }

    private void OnConnectionStateChanged(object? sender, bool connected)
    {
        Dispatcher.UIThread.Post(() =>
        {
            IsConnected = connected;
            IsConnecting = false;
        });
    }

    private void ApplyConfigToProperties()
    {
        var cfg = _manager.Config;
        _host = cfg.Host;
        _port = cfg.Port;
        _username = cfg.Username;
        _password = cfg.Password;
        _rootTopic = cfg.RootTopic;
        _qos = cfg.Qos;
        UseTls = cfg.UseTls;
        PublishChatSend = cfg.PublishChatSend;
        PublishChatReceive = cfg.PublishChatReceive;
        PublishImSend = cfg.PublishImSend;
        PublishImReceive = cfg.PublishImReceive;
        PublishLocation = cfg.PublishLocation;
        SubscribeCommands = cfg.SubscribeCommands;
        AutoConnect = cfg.AutoConnect;
    }

    private void ApplyPropertiesToConfig()
    {
        var cfg = _manager.Config;
        cfg.Host = _host;
        cfg.Port = _port;
        cfg.Username = _username;
        cfg.Password = _password;
        cfg.RootTopic = _rootTopic;
        cfg.Qos = _qos;
        cfg.UseTls = UseTls;
        cfg.PublishChatSend = PublishChatSend;
        cfg.PublishChatReceive = PublishChatReceive;
        cfg.PublishImSend = PublishImSend;
        cfg.PublishImReceive = PublishImReceive;
        cfg.PublishLocation = PublishLocation;
        cfg.SubscribeCommands = SubscribeCommands;
        cfg.AutoConnect = AutoConnect;
    }

    [RelayCommand]
    private async Task ToggleConnection()
    {
        if (IsConnected)
        {
            await _manager.DisconnectAsync();
        }
        else
        {
            ApplyPropertiesToConfig();
            IsConnecting = true;
            await _manager.ConnectAsync();
        }
    }

    [RelayCommand]
    private void Save()
    {
        ApplyPropertiesToConfig();
        var json = JsonSerializer.Serialize(_manager.Config);
        _instance.GlobalSettings["mqtt_config"] = OpenMetaverse.StructuredData.OSD.FromString(json);
    }

    public void Dispose()
    {
        _instance.NetCom.ClientLoginStatus -= NetCom_ClientLoginStatus;
        _manager.ConnectionStateChanged -= OnConnectionStateChanged;
    }
}
