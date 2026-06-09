using System;
using System.Security.Cryptography;
using System.Text;
using OpenMetaverse.StructuredData;

namespace Radegast;

public static class HWSpoof
{
    private static string? _seed;
    private static string? _username;
    private static string? _fakeId0;
    private static string? _fakeMac;

    public static string GetSeed()
    {
        if (_seed == null)
            _seed = Guid.NewGuid().ToString("N")[..16];
        return _seed;
    }

    public static void SetSeed(string seed)
    {
        _seed = seed;
        _fakeId0 = null;
        _fakeMac = null;
    }

    public static void RerollSeed()
    {
        _seed = Guid.NewGuid().ToString("N")[..16];
        _fakeId0 = null;
        _fakeMac = null;
    }

    public static string GetUsername() => _username ?? string.Empty;

    public static void SetUsername(string username)
    {
        _username = username.ToLowerInvariant();
        _fakeId0 = null;
        _fakeMac = null;
    }

    public static string GetId0()
    {
        if (_fakeId0 == null)
            _fakeId0 = HashString("id0", GetSeed(), GetUsername());
        return _fakeId0;
    }

    public static string GetMac()
    {
        if (_fakeMac == null)
            _fakeMac = HashString("mac", GetSeed(), GetUsername());
        return _fakeMac;
    }

    public static void LoadFromSettings(Settings settings)
    {
        var saved = settings["hwspoof_seed"]?.AsString();
        if (!string.IsNullOrEmpty(saved))
            SetSeed(saved);
    }

    public static void SaveToSettings(Settings settings)
    {
        settings["hwspoof_seed"] = OSD.FromString(GetSeed());
    }

    private static string HashString(string prefix, string seed, string username)
    {
        var input = prefix + seed + username;
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
