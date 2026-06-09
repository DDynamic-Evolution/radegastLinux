using System;
using System.IO;
using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Loaders;
using OpenMetaverse;

namespace Radegast.Veles.Scripting;

public sealed class LuaPlugin : IDisposable
{
    private readonly Script _script;
    private readonly string _filePath;

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

    private DynValue? GetHook(string name)
    {
        var val = _script.Globals.Get(name);
        return val.Type == DataType.Function ? val : null;
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

        var fn = GetHook("on_start");
        if (fn != null)
        {
            try { _script.Call(fn); }
            catch (Exception ex) { OpenMetaverse.Logger.Warn($"[Lua:{Name}] on_start error: {ex.Message}"); }
        }
    }

    public void Stop()
    {
        if (!IsRunning) return;
        IsRunning = false;

        var fn = GetHook("on_stop");
        if (fn != null)
        {
            try { _script.Call(fn); }
            catch (Exception ex) { OpenMetaverse.Logger.Warn($"[Lua:{Name}] on_stop error: {ex.Message}"); }
        }
    }

    public void FireEvent(string eventName, params object[] args)
    {
        if (!IsRunning) return;

        var fn = GetHook(eventName);
        if (fn != null)
        {
            try { _script.Call(fn, args); }
            catch (Exception ex) { OpenMetaverse.Logger.Warn($"[Lua:{Name}] {eventName} error: {ex.Message}"); }
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
