using System;
using System.Collections.Generic;
using System.IO;
using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Loaders;
using OpenMetaverse;

namespace Radegast.Veles.Scripting;

public sealed class LuaPlugin : IDisposable
{
    private readonly Script _script;
    private readonly string _filePath;
    private readonly Dictionary<string, Closure> _callbacks = new();

    public string Name { get; }
    public string FilePath => _filePath;
    public bool IsRunning { get; private set; }

    public LuaPlugin(string name, string filePath)
    {
        Name = name;
        _filePath = filePath;

        _script = new Script(CoreModules.Preset_SoftSandbox);
        _script.Options.DebugPrint = s => OpenMetaverse.Logger.Warn($"[Lua:{Name}] {s}");

        var scriptsDir = Path.GetDirectoryName(filePath) ?? ".";
        _script.Options.ScriptLoader = new FileSystemScriptLoader
        {
            ModulePaths = new[] { scriptsDir + "/?", scriptsDir + "/?.lua" }
        };

        RegisterCoreApi();
    }

    private void RegisterCoreApi()
    {
        _script.Globals["on_chat"] = (Action<Closure>)RegisterCallback("on_chat");
        _script.Globals["on_im"] = (Action<Closure>)RegisterCallback("on_im");
        _script.Globals["on_connected"] = (Action<Closure>)RegisterCallback("on_connected");
        _script.Globals["on_disconnected"] = (Action<Closure>)RegisterCallback("on_disconnected");
        _script.Globals["on_teleport"] = (Action<Closure>)RegisterCallback("on_teleport");
        _script.Globals["send_chat"] = (Action<string, double>)LuaApi.SendChat;
        _script.Globals["send_im"] = (Action<string, string>)LuaApi.SendIM;
        _script.Globals["teleport"] = (Action<string, double, double, double>)LuaApi.Teleport;
        _script.Globals["log"] = (Action<string>)LuaApi.Log;
        _script.Globals["log_info"] = (Action<string>)LuaApi.LogInfo;
        _script.Globals["log_warn"] = (Action<string>)LuaApi.LogWarn;
        _script.Globals["log_error"] = (Action<string>)LuaApi.LogError;
        _script.Globals["get_setting"] = (Func<string, string?>)LuaApi.GetSetting;
        _script.Globals["set_setting"] = (Action<string, string>)LuaApi.SetSetting;
        _script.Globals["http_get"] = (Action<string, Closure>)LuaApi.HttpGet;
        _script.Globals["schedule"] = (Action<double, Closure>)LuaApi.Schedule;
    }

    private Action<Closure> RegisterCallback(string name)
    {
        return closure => { _callbacks[name] = closure; };
    }

    public bool Load()
    {
        try
        {
            _script.DoFile(_filePath);
            return true;
        }
        catch (Exception ex)
        {
            OpenMetaverse.Logger.Warn($"[Lua:{Name}] Failed to load: {ex.Message}");
            return false;
        }
    }

    public void Start()
    {
        if (IsRunning) return;
        IsRunning = true;

        if (_callbacks.TryGetValue("on_start", out var onStart))
        {
            try { _script.Call(onStart); }
            catch (Exception ex) { OpenMetaverse.Logger.Warn($"[Lua:{Name}] on_start error: {ex.Message}"); }
        }
    }

    public void Stop()
    {
        if (!IsRunning) return;
        IsRunning = false;

        if (_callbacks.TryGetValue("on_stop", out var onStop))
        {
            try { _script.Call(onStop); }
            catch (Exception ex) { OpenMetaverse.Logger.Warn($"[Lua:{Name}] on_stop error: {ex.Message}"); }
        }
    }

    public void FireEvent(string eventName, params object[] args)
    {
        if (!IsRunning) return;

        if (_callbacks.TryGetValue(eventName, out var closure))
        {
            try { _script.Call(closure, args); }
            catch (Exception ex) { OpenMetaverse.Logger.Warn($"[Lua:{Name}] {eventName} error: {ex.Message}"); }
        }
    }

    public void Dispose()
    {
        Stop();
        _callbacks.Clear();
    }
}
