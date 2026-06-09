using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Threading;
using OpenMetaverse;
using Radegast.Veles.Core;

namespace Radegast.Veles.Scripting;

public sealed class LuaPluginManager : IDisposable
{
    private readonly RadegastInstanceAvalonia _instance;
    private readonly string _scriptsDir;
    private readonly List<LuaPlugin> _plugins = new();
    private readonly DispatcherTimer _tickTimer;

    public IReadOnlyList<LuaPlugin> Plugins => _plugins;

    public LuaPluginManager(RadegastInstanceAvalonia instance)
    {
        _instance = instance;
        _scriptsDir = GetScriptsDirectory();
        LuaApi.Instance = instance;

        _tickTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(500), DispatcherPriority.Normal, OnTick);

        ConnectEvents();
        DiscoverAndLoad();
    }

    private static string GetScriptsDirectory()
    {
        var dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RadegastVeles", "scripts");
        Directory.CreateDirectory(dataDir);
        return dataDir;
    }

    private void ConnectEvents()
    {
        _instance.NetCom.ClientConnected += OnClientConnected;
        _instance.NetCom.ClientDisconnected += OnClientDisconnected;
        _instance.NetCom.ChatReceived += OnChatReceived;
        _instance.NetCom.InstantMessageReceived += OnInstantMessageReceived;
        _instance.NetCom.TeleportStatusChanged += OnTeleportStatusChanged;
    }

    private void DisconnectEvents()
    {
        _instance.NetCom.ClientConnected -= OnClientConnected;
        _instance.NetCom.ClientDisconnected -= OnClientDisconnected;
        _instance.NetCom.ChatReceived -= OnChatReceived;
        _instance.NetCom.InstantMessageReceived -= OnInstantMessageReceived;
        _instance.NetCom.TeleportStatusChanged -= OnTeleportStatusChanged;
    }

    public void DiscoverAndLoad()
    {
        StopAll();
        _plugins.Clear();

        if (!Directory.Exists(_scriptsDir))
        {
            Directory.CreateDirectory(_scriptsDir);
            return;
        }

        foreach (var file in Directory.GetFiles(_scriptsDir, "*.lua"))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            var plugin = new LuaPlugin(name, file);
            if (plugin.Load())
            {
                _plugins.Add(plugin);
                OpenMetaverse.Logger.Warn($"[Lua] Loaded script: {name}");
            }
        }

        if (_plugins.Count == 0)
            OpenMetaverse.Logger.Warn($"[Lua] No scripts found in {_scriptsDir}");
    }

    public void Reload(string name)
    {
        var existing = _plugins.FirstOrDefault(p => p.Name == name);
        if (existing != null)
        {
            existing.Dispose();
            _plugins.Remove(existing);
        }

        var file = Path.Combine(_scriptsDir, $"{name}.lua");
        if (!File.Exists(file)) return;

        var plugin = new LuaPlugin(name, file);
        if (plugin.Load())
        {
            _plugins.Add(plugin);
            if (_instance.NetCom.IsLoggedIn)
                plugin.Start();
            OpenMetaverse.Logger.Warn($"[Lua] Reloaded script: {name}");
        }
    }

    public void StartAll()
    {
        foreach (var plugin in _plugins)
            plugin.Start();
        _tickTimer.Start();
    }

    public void StopAll()
    {
        _tickTimer.Stop();
        foreach (var plugin in _plugins)
            plugin.Stop();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        LuaApi.TickScheduled();
    }

    private void OnClientConnected(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            StartAll();
            foreach (var plugin in _plugins)
                plugin.FireEvent("on_connected");
        });
    }

    private void OnClientDisconnected(object? sender, DisconnectedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            foreach (var plugin in _plugins)
                plugin.FireEvent("on_disconnected");
            StopAll();
        });
    }

    private void OnTeleportStatusChanged(object? sender, TeleportEventArgs e)
    {
        if (e.Status != TeleportStatus.Finished) return;

        var sim = _instance.Client.Network.CurrentSim;
        if (sim == null) return;

        var pos = _instance.Client.Self.SimPosition;
        foreach (var plugin in _plugins)
            plugin.FireEvent("on_teleport", sim.Name, (double)pos.X, (double)pos.Y, (double)pos.Z);
    }

    private void OnChatReceived(object? sender, ChatEventArgs e)
    {
        foreach (var plugin in _plugins)
            plugin.FireEvent("on_chat", e.Message, (int)e.Type, e.FromName ?? "Unknown");
    }

    private void OnInstantMessageReceived(object? sender, InstantMessageEventArgs e)
    {
        var msg = e.IM;
        foreach (var plugin in _plugins)
            plugin.FireEvent("on_im", msg.FromAgentID.ToString(), msg.FromAgentName, msg.Message);
    }

    public void Dispose()
    {
        DisconnectEvents();
        StopAll();
        _plugins.Clear();
        _tickTimer.Stop();
    }
}
