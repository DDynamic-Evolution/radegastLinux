using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using MoonSharp.Interpreter;
using OpenMetaverse;
using Radegast.Veles.Core;

namespace Radegast.Veles.Scripting;

public static class LuaApi
{
    private static readonly HttpClient s_http = new();

    internal static RadegastInstanceAvalonia? Instance { get; set; }

    public static void SendChat(string message, double channel)
    {
        if (Instance == null) return;
        Instance.NetCom.ChatOut(message, ChatType.Normal, (int)channel);
    }

    public static void SendIM(string agentId, string message)
    {
        if (Instance == null) return;
        if (UUID.TryParse(agentId, out var id))
            Instance.Client.Self.InstantMessage(id, message);
    }

    public static void Teleport(string region, double x, double y, double z)
    {
        if (Instance == null) return;
        Task.Run(() =>
            Instance.Client.Self.Teleport(region, new Vector3((float)x, (float)y, (float)z)));
    }

    public static void Log(string message) =>
        OpenMetaverse.Logger.Warn($"[Lua] {message}");

    public static void LogInfo(string message) =>
        OpenMetaverse.Logger.Warn($"[Lua] {message}");

    public static void LogWarn(string message) =>
        OpenMetaverse.Logger.Warn($"[Lua] {message}");

    public static void LogError(string message) =>
        OpenMetaverse.Logger.Warn($"[Lua] {message}");

    public static string? GetSetting(string key)
    {
        if (Instance == null) return null;
        var val = Instance.GlobalSettings[key];
        return val?.AsString();
    }

    public static void SetSetting(string key, string value)
    {
        if (Instance == null) return;
        Instance.GlobalSettings[key] = OpenMetaverse.StructuredData.OSD.FromString(value);
    }

    public static void HttpGet(string url, Closure callback)
    {
        Task.Run(async () =>
        {
            try
            {
                var response = await s_http.GetStringAsync(url);
                callback.Call(response);
            }
            catch (Exception ex)
            {
                callback.Call(null, ex.Message);
            }
        });
    }

    private static readonly List<(double FireAt, Closure Callback)> s_scheduled = new();

    public static void Schedule(double delaySeconds, Closure callback)
    {
        var fireAt = DateTime.UtcNow.AddSeconds(delaySeconds).Ticks;
        s_scheduled.Add((fireAt, callback));
    }

    internal static void TickScheduled()
    {
        var now = DateTime.UtcNow.Ticks;
        for (int i = s_scheduled.Count - 1; i >= 0; i--)
        {
            if (now >= s_scheduled[i].FireAt)
            {
                try { s_scheduled[i].Callback.Call(); }
                catch { /* swallow */ }
                s_scheduled.RemoveAt(i);
            }
        }
    }
}
