using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Radegast.Veles.VPN;

public enum VpnState
{
    Disconnected,
    Connected,
    Error
}

public class VpnManager : IDisposable
{
    private readonly string _helperPath;
    private Process? _helper;
    private StreamWriter? _helperStdin;
    private StreamReader? _helperStdout;
    private readonly object _lock = new();
    private int _nextId;

    public event EventHandler? AvailabilityChanged;

    public bool IsAvailable { get; private set; }

    public VpnManager(string helperPath)
    {
        _helperPath = helperPath;
        IsAvailable = File.Exists(_helperPath);
    }

    public void Start()
    {
        if (!IsAvailable) return;
        lock (_lock)
        {
            if (_helper != null) return;

            var psi = new ProcessStartInfo
            {
                FileName = _helperPath,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                StandardInputEncoding = Encoding.UTF8,
                StandardOutputEncoding = Encoding.UTF8,
                CreateNoWindow = true,
            };

            try
            {
                _helper = new Process { StartInfo = psi };
                _helper.Start();
                _helperStdin = _helper.StandardInput;
                _helperStdout = _helper.StandardOutput;
            }
            catch
            {
                _helper = null;
                _helperStdin = null;
                _helperStdout = null;
            }
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            if (_helper == null) return;

            try { _helperStdin?.WriteLine("{\"cmd\":\"shutdown\"}"); _helperStdin?.Flush(); } catch { }
            try { _helper.WaitForExit(2000); } catch { }
            if (!_helper.HasExited) try { _helper.Kill(); } catch { }

            _helper.Dispose();
            _helper = null;
            _helperStdin = null;
            _helperStdout = null;
        }
    }

    public async Task<bool> Connect(string profileName, VpnProfile profile)
    {
        if (!IsAvailable) return false;

        var config = new
        {
            private_key = profile.PrivateKey,
            address = profile.Address,
            peers = profile.Peers.Select(p => new
            {
                public_key = p.PublicKey,
                endpoint = p.Endpoint,
                allowed_ips = p.AllowedIPs,
                persistent_keepalive = p.PersistentKeepalive,
            }).ToList(),
        };

        var resp = await SendCommand("up", profileName, config);
        return resp?.ok == true;
    }

    public async Task Disconnect(string profileName)
    {
        await SendCommand("down", profileName);
    }

    public async Task<VpnState> GetStatus(string profileName)
    {
        var resp = await SendCommand("status", profileName);
        if (resp?.status?.up == true) return VpnState.Connected;
        return VpnState.Disconnected;
    }

    public async Task<List<string>> ListTunnels()
    {
        var resp = await SendCommand("list", "");
        return resp?.tunnels ?? [];
    }

    public void Dispose()
    {
        Stop();
    }

    private async Task<VpnResponseJson?> SendCommand(string cmd, string name, object? config = null)
    {
        var id = Interlocked.Increment(ref _nextId);

        var obj = new Dictionary<string, object?>
        {
            ["id"] = id,
            ["cmd"] = cmd,
            ["name"] = name,
        };
        if (config != null) obj["config"] = config;

        var json = JsonSerializer.Serialize(obj);

        lock (_lock)
        {
            if (_helperStdin == null) return null;
            _helperStdin.WriteLine(json);
            _helperStdin.Flush();
        }

        // Read exactly one response line
        try
        {
            var line = await _helperStdout!.ReadLineAsync();
            if (line == null) return null;
            return JsonSerializer.Deserialize<VpnResponseJson>(line);
        }
        catch
        {
            return null;
        }
    }
}

internal class VpnResponseJson
{
    public int id { get; set; }
    public bool ok { get; set; }
    public string? error { get; set; }
    public VpnStatusJson? status { get; set; }
    public List<string>? tunnels { get; set; }
}

internal class VpnStatusJson
{
    public bool up { get; set; }
    public string? address { get; set; }
    public long bytes_rx { get; set; }
    public long bytes_tx { get; set; }
}
