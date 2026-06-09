/*
 * Radegast Metaverse Client
 * Copyright (c) 2026, Sjofn LLC
 * All rights reserved.
 *
 * Radegast is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Lesser General Public License as published
 * by the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program. If not, see <https://www.gnu.org/licenses/>.
 */

using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LibreMetaverse.RLV;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using Radegast.Veles.Core;

namespace Radegast.Veles.ViewModels;

public partial class RlvViewModel : ObservableObject, IDisposable
{
    private readonly RadegastInstanceAvalonia _instance;
    private const int MaxLogEntries = 500;

    [ObservableProperty]
    private bool _enabled;

    [ObservableProperty]
    private string _statusText = "Disabled";

    [ObservableProperty]
    private bool _showLiveLog;

    [ObservableProperty]
    private bool _logAllChannels;

    [ObservableProperty]
    private string _restrictionCountText = "0 restrictions";

    [ObservableProperty]
    private string _sourceCountText = "0 sources";

    // Auto-Response Settings (0 = Ask, 1 = Always Accept, 2 = Always Deny)
    [ObservableProperty]
    private int _teleportLureMode;

    [ObservableProperty]
    private int _teleportRequestMode;

    [ObservableProperty]
    private int _scriptPermissionMode;

    [ObservableProperty]
    private int _groupInviteMode;

    [ObservableProperty]
    private int _friendRequestMode;

    public ObservableCollection<RlvRestrictionItem> Restrictions { get; } = new();
    public ObservableCollection<RlvSourceItem> Sources { get; } = new();
    public ObservableCollection<RlvLogEntry> LogEntries { get; } = new();
    public ObservableCollection<TrustedAvatarItem> TrustedAvatars { get; } = new();
    public ObservableCollection<RlvDebugPermissionCheck> DebugPermissionChecks { get; } = new();
    public ObservableCollection<RlvDebugCommand> DebugCommandLog { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveTrustedCommand))]
    private TrustedAvatarItem? _selectedTrustedAvatar;

    public RlvViewModel(RadegastInstanceAvalonia instance)
    {
        _instance = instance;
        _enabled = LoadEnabled();
        UpdateStatusText();

        if (instance.RLV != null)
        {
            instance.RLV.Restrictions.RestrictionUpdated += Restrictions_RestrictionUpdated;
        }
        instance.NetCom.ChatReceived += NetCom_ChatReceived;
        instance.NetCom.ClientLoginStatus += NetCom_ClientLoginStatus;
        LoadSettings();
        RefreshRestrictions();
        LoadTrustedAvatars();
    }

    private void NetCom_ClientLoginStatus(object? sender, LoginProgressEventArgs e)
    {
        if (e.Status != LoginStatus.Success) return;
        
        if (_instance.RLV != null)
        {
            _enabled = _instance.RLV.Enabled;
            UpdateStatusText();
            RefreshRestrictions();
        }
    }

    private void NetCom_ChatReceived(object? sender, OpenMetaverse.ChatEventArgs e)
    {
        if (!ShowLiveLog || !LogAllChannels) return;

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            var source = e.SourceType == OpenMetaverse.ChatSourceType.Agent ? e.FromName : $"[{e.FromName}]";
            var entry = new RlvLogEntry($"[{DateTime.Now:HH:mm:ss}] {source}: {e.Message}");
            LogEntries.Add(entry);
            while (LogEntries.Count > MaxLogEntries)
                LogEntries.RemoveAt(0);
        });
    }

    partial void OnEnabledChanged(bool value)
    {
        SaveEnabled(value);
        if (_instance.RLV != null)
        {
            _instance.RLV.Enabled = value;
        }
        UpdateStatusText();
    }

    private bool LoadEnabled()
    {
        var s = _instance.GlobalSettings;
        if (s["rlv_enabled"].Type == OSDType.Unknown)
            s["rlv_enabled"] = new OSDBoolean(true);
        return s["rlv_enabled"].AsBoolean();
    }

    private void SaveEnabled(bool value)
    {
        _instance.GlobalSettings["rlv_enabled"] = new OSDBoolean(value);
    }

    private void UpdateStatusText()
    {
        StatusText = Enabled ? "Active" : "Disabled";
    }

    private void Restrictions_RestrictionUpdated(object? sender, LibreMetaverse.RLV.EventArguments.RestrictionUpdatedEventArgs e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (ShowLiveLog)
            {
                var entry = new RlvLogEntry($"[{DateTime.Now:HH:mm:ss}] {e.Restriction}");
                LogEntries.Add(entry);
                while (LogEntries.Count > MaxLogEntries)
                    LogEntries.RemoveAt(0);
            }

            RefreshRestrictions();
        });
    }

    private void RefreshRestrictions()
    {
        Restrictions.Clear();
        Sources.Clear();

        if (!Enabled || _instance.RLV == null) return;

        var restrictions = _instance.RLV.Restrictions.FindRestrictions("");
        var sourceMap = new System.Collections.Generic.Dictionary<string, int>();

        foreach (var r in restrictions)
        {
            var restrictionName = r.ToString();
            Restrictions.Add(new RlvRestrictionItem(restrictionName, "Object"));
            
            var sourceName = "Object";
            if (sourceMap.ContainsKey(sourceName))
                sourceMap[sourceName]++;
            else
                sourceMap[sourceName] = 1;
        }

        foreach (var kv in sourceMap.OrderByDescending(x => x.Value))
        {
            Sources.Add(new RlvSourceItem(kv.Key, kv.Value));
        }

        RestrictionCountText = $"{Restrictions.Count} restriction{(Restrictions.Count != 1 ? "s" : "")}";
        SourceCountText = $"{Sources.Count} source{(Sources.Count != 1 ? "s" : "")}";
    }

    [RelayCommand]
    private void ClearLog()
    {
        LogEntries.Clear();
    }

    [RelayCommand]
    private void AddTrusted()
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
        {
            var window = new Views.AddTrustedAvatarWindow();
            
            Avalonia.Controls.Window? parentWindow = null;
            if (Avalonia.Application.Current?.ApplicationLifetime 
                is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                parentWindow = desktop.Windows.OfType<Avalonia.Controls.Window>().LastOrDefault();
            }
            
            if (parentWindow == null) return;
            
            await window.ShowDialog(parentWindow);
            
            if (window.Result && window.AvatarName != null)
            {
                TrustedAvatars.Add(new TrustedAvatarItem(window.AvatarId, window.AvatarName, window.Mode));
                SaveTrustedAvatars();
            }
        });
    }

    [RelayCommand(CanExecute = nameof(CanRemoveTrusted))]
    private void RemoveTrusted()
    {
        if (SelectedTrustedAvatar == null) return;
        TrustedAvatars.Remove(SelectedTrustedAvatar);
        SelectedTrustedAvatar = null;
        SaveTrustedAvatars();
    }

    private bool CanRemoveTrusted() => SelectedTrustedAvatar != null;

    private void LoadSettings()
    {
        var s = _instance.GlobalSettings;
        TeleportLureMode = s["rlv_tp_lure_mode"].Type != OSDType.Unknown ? s["rlv_tp_lure_mode"].AsInteger() : 0;
        TeleportRequestMode = s["rlv_tp_request_mode"].Type != OSDType.Unknown ? s["rlv_tp_request_mode"].AsInteger() : 0;
        ScriptPermissionMode = s["rlv_script_perm_mode"].Type != OSDType.Unknown ? s["rlv_script_perm_mode"].AsInteger() : 0;
        GroupInviteMode = s["rlv_group_invite_mode"].Type != OSDType.Unknown ? s["rlv_group_invite_mode"].AsInteger() : 0;
        FriendRequestMode = s["rlv_friend_request_mode"].Type != OSDType.Unknown ? s["rlv_friend_request_mode"].AsInteger() : 0;
    }

    public void SaveSettings()
    {
        var s = _instance.GlobalSettings;
        s["rlv_tp_lure_mode"] = OSD.FromInteger(TeleportLureMode);
        s["rlv_tp_request_mode"] = OSD.FromInteger(TeleportRequestMode);
        s["rlv_script_perm_mode"] = OSD.FromInteger(ScriptPermissionMode);
        s["rlv_group_invite_mode"] = OSD.FromInteger(GroupInviteMode);
        s["rlv_friend_request_mode"] = OSD.FromInteger(FriendRequestMode);
        SaveTrustedAvatars();
    }

    private void LoadTrustedAvatars()
    {
        var s = _instance.GlobalSettings;
        if (s["rlv_trusted_avatars"] is OSDArray arr)
        {
            foreach (var item in arr)
            {
                if (item is OSDMap map)
                {
                    var id = map["id"].AsString();
                    var name = map["name"].AsString();
                    var mode = map["mode"].AsInteger();
                    if (!string.IsNullOrEmpty(id) && UUID.TryParse(id, out var uuid))
                    {
                        TrustedAvatars.Add(new TrustedAvatarItem(uuid, name, mode));
                    }
                }
            }
        }
    }

    private void SaveTrustedAvatars()
    {
        var arr = new OSDArray();
        foreach (var item in TrustedAvatars)
        {
            arr.Add(new OSDMap
            {
                ["id"] = OSD.FromString(item.Id.ToString()),
                ["name"] = OSD.FromString(item.Name),
                ["mode"] = OSD.FromInteger(item.Mode)
            });
        }
        _instance.GlobalSettings["rlv_trusted_avatars"] = arr;
    }

    public void Dispose()
    {
        if (_instance.RLV != null)
        {
            _instance.RLV.Restrictions.RestrictionUpdated -= Restrictions_RestrictionUpdated;
        }
        _instance.NetCom.ChatReceived -= NetCom_ChatReceived;
        _instance.NetCom.ClientLoginStatus -= NetCom_ClientLoginStatus;
    }
}

public class RlvRestrictionItem
{
    public string Name { get; }
    public string Source { get; }

    public RlvRestrictionItem(string name, string source)
    {
        Name = name;
        Source = source;
    }
}

public class RlvSourceItem
{
    public string Name { get; }
    public int RestrictionCount { get; }

    public RlvSourceItem(string name, int restrictionCount)
    {
        Name = name;
        RestrictionCount = restrictionCount;
    }
}

public class RlvLogEntry
{
    public string Text { get; }

    public RlvLogEntry(string text)
    {
        Text = text;
    }
}

public class TrustedAvatarItem
{
    public UUID Id { get; }
    public string Name { get; }
    public int Mode { get; }
    public string ModeText => Mode switch
    {
        0 => "Ask",
        1 => "Always Accept",
        2 => "Always Deny",
        _ => "Unknown"
    };

    public TrustedAvatarItem(UUID id, string name, int mode)
    {
        Id = id;
        Name = name;
        Mode = mode;
    }
}

public class RlvDebugPermissionCheck
{
    public DateTime Timestamp { get; }
    public string Check { get; }
    public string Result { get; }
    public Avalonia.Media.IBrush ResultColor { get; }

    public RlvDebugPermissionCheck(DateTime timestamp, string check, bool result)
    {
        Timestamp = timestamp;
        Check = check;
        Result = result ? "Allowed" : "Denied";
        ResultColor = result 
            ? new Avalonia.Media.SolidColorBrush(Avalonia.Media.Colors.Green)
            : new Avalonia.Media.SolidColorBrush(Avalonia.Media.Colors.Red);
    }
}

public class RlvDebugCommand
{
    public DateTime Timestamp { get; }
    public string Command { get; }

    public RlvDebugCommand(DateTime timestamp, string command)
    {
        Timestamp = timestamp;
        Command = command;
    }
}
