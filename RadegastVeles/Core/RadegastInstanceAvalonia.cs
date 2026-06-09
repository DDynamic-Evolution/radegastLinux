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
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Threading;
using LibreMetaverse;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using Radegast.Veles.MQTT;
using Radegast.Veles.Scripting;
using Radegast.Veles.VPN;
using Radegast.Veles.ViewModels;
using Radegast.Veles.Views;

namespace Radegast.Veles.Core;

public sealed class RadegastInstanceAvalonia : RadegastInstance
{
    public event EventHandler<NotificationChatEventArgs>? NotificationInChat;

    /// <summary>Raised when any part of the UI requests opening a P2P IM session.</summary>
    public event EventHandler<IMRequestedEventArgs>? IMRequested;

    /// <summary>Ask the IM system to open (or focus) a session with the given agent.</summary>
    public void RequestIM(UUID agentId, string agentName)
        => IMRequested?.Invoke(this, new IMRequestedEventArgs(agentId, agentName));

    /// <summary>Raised when any part of the UI requests opening a group IM session.</summary>
    public event EventHandler<GroupIMRequestedEventArgs>? GroupIMRequested;

    /// <summary>Ask the IM system to open (or focus) a group chat session.</summary>
    public void RequestGroupIM(UUID groupId, string groupName)
        => GroupIMRequested?.Invoke(this, new GroupIMRequestedEventArgs(groupId, groupName));

    /// <summary>Open the Pay dialog for an avatar or an in-world object.</summary>
    public void OpenPayWindow(UUID targetId, string name, bool isObject = false, Simulator? sim = null)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var vm = new PayViewModel(this, targetId, name, isObject, sim);
            var window = new PayWindow { DataContext = vm };
            vm.CloseRequested += (_, _) => window.Close();
            window.Show();
        });
    }

    public ChatLogger ChatLog { get; } = new ChatLogger();

    /// <summary>Raised when any in-world notification should be shown to the user.</summary>
    public event EventHandler<NotificationViewModel>? NotificationReceived;

    /// <summary>Raises a notification through the notification queue.</summary>
    public void RaiseNotification(NotificationViewModel vm) =>
        NotificationReceived?.Invoke(this, vm);

    /// <summary>VPN tunnel manager (WireGuard helper).</summary>
    public VpnManager Vpn { get; }

    /// <summary>MQTT client manager.</summary>
    public MqttManager Mqtt { get; } = new();

    /// <summary>Lua script plugin manager.</summary>
    public LuaPluginManager LuaPlugins { get; }

    internal RadegastInstanceAvalonia(string appName, GridClient client)
        : base(appName, client, new NetComAvalonia(client))
    {
        LuaPlugins = new LuaPluginManager(this);
        // Initialize VPN helper
        var helperPath = Path.Combine(AppContext.BaseDirectory, "vpn-helper");
        Vpn = new VpnManager(helperPath);

        // Honour a user-configured texture-cache path stored by Preferences.
        var customCacheDir = GlobalSettings["texture_cache_dir"]?.AsString();
        if (!string.IsNullOrWhiteSpace(customCacheDir))
            Client.Settings.ASSET_CACHE_DIR = customCacheDir;

        // Apply chat logging preferences
        var chatLogDir = GlobalSettings["chat_log_dir"]?.AsString();
        if (!string.IsNullOrWhiteSpace(chatLogDir))
            ChatLog.BaseDirectory = chatLogDir;

        if (GlobalSettings["chat_logging_enabled"].Type != OpenMetaverse.StructuredData.OSDType.Unknown)
            ChatLog.IsEnabled = GlobalSettings["chat_logging_enabled"].AsBoolean();

        // Load HW spoof seed
        HWSpoof.LoadFromSettings(GlobalSettings);

        client.Self.ScriptDialog += Self_ScriptDialog;
        client.Self.ScriptQuestion += Self_ScriptQuestion;
        client.Self.LoadURL += Self_LoadURL;
        client.Self.TeleportProgress += Self_TeleportProgress;
        NetCom.InstantMessageReceived += NetCom_InstantMessageReceived;
        NetCom.AlertMessageReceived += NetCom_AlertMessageReceived;

        Mqtt.CommandReceived += OnMqttCommand;
    }

    private void Self_ScriptDialog(object? sender, ScriptDialogEventArgs e)
    {
        // Check mute list
        if (null != Client.Self.MuteList.Find(m => m.Type == MuteType.Object && m.ID == e.ObjectID)) return;
        if (null != Client.Self.MuteList.Find(m => m.Type == MuteType.ByName && m.Name == e.ObjectName)) return;

        var vm = NotificationViewModel.ForScriptDialog(
            Client, e.ObjectName, $"{e.FirstName} {e.LastName}",
            e.Message, e.ButtonLabels, e.ObjectID, e.Channel);
        NotificationReceived?.Invoke(this, vm);
        MediaManager.PlayUISound(UISounds.PieAppear);
    }

    private void Self_ScriptQuestion(object? sender, ScriptQuestionEventArgs e)
    {
        // Check mute list by object name
        if (null != Client.Self.MuteList.Find(m => m.Type == MuteType.ByName && m.Name == e.ObjectName)) return;
        if (null != Client.Self.MuteList.Find(m => m.Type == MuteType.Object && m.ID == e.TaskID)) return;

        // RLV auto-accept / auto-deny
        if (RLV.Enabled)
        {
            if (RLV.Permissions.IsAutoDenyPermissions())
            {
                Client.Self.ScriptQuestionReply(e.Simulator, e.ItemID, e.TaskID, 0);
                return;
            }
            if (RLV.Permissions.IsAutoAcceptPermissions())
            {
                Client.Self.ScriptQuestionReply(e.Simulator, e.ItemID, e.TaskID, e.Questions);
                return;
            }
        }

        var vm = NotificationViewModel.ForPermissions(
            Client, e.Simulator, e.TaskID, e.ItemID,
            e.ObjectName, e.ObjectOwnerName, e.Questions);
        NotificationReceived?.Invoke(this, vm);
    }

    private void Self_LoadURL(object? sender, LoadUrlEventArgs e)
    {
        if (null != Client.Self.MuteList.Find(m =>
                (m.Type == MuteType.Object && m.ID == e.ObjectID) ||
                (m.Type == MuteType.ByName && m.Name == e.ObjectName) ||
                (m.Type == MuteType.Resident && m.ID == e.OwnerID))) return;

        string ownerName = Names.Get(e.OwnerID);
        var vm = NotificationViewModel.ForLoadUrl(e.ObjectName, ownerName, e.URL, e.Message);
        NotificationReceived?.Invoke(this, vm);
    }

    private void NetCom_InstantMessageReceived(object? sender, InstantMessageEventArgs e)
    {
        InstantMessage msg = e.IM;
        switch (msg.Dialog)
        {
            case InstantMessageDialog.FriendshipOffered:
                if (msg.FromAgentName == "Second Life") return;
                NotificationReceived?.Invoke(this, NotificationViewModel.ForFriendshipOffer(Client, msg));
                MediaManager.PlayUISound(UISounds.Alert);
                break;

            case InstantMessageDialog.GroupNotice:
                if (null != Client.Self.MuteList.Find(m => m.Type == MuteType.Group && m.ID == msg.FromAgentID)) return;
                NotificationReceived?.Invoke(this, NotificationViewModel.ForGroupNotice(Client, msg));
                MediaManager.PlayUISound(UISounds.Alert);
                break;

            case InstantMessageDialog.GroupInvitation:
                NotificationReceived?.Invoke(this, NotificationViewModel.ForGroupInvitation(Client, msg));
                MediaManager.PlayUISound(UISounds.Alert);
                break;

            case InstantMessageDialog.InventoryOffered:
                NotificationReceived?.Invoke(this, NotificationViewModel.ForInventoryOffer(Client, msg));
                MediaManager.PlayUISound(UISounds.Alert);
                break;

            case InstantMessageDialog.TaskInventoryOffered:
                if (null != Client.Self.MuteList.Find(m => m.Type == MuteType.ByName && m.Name == msg.FromAgentName)) return;
                NotificationReceived?.Invoke(this, NotificationViewModel.ForInventoryOffer(Client, msg));
                MediaManager.PlayUISound(UISounds.Alert);
                break;

            case InstantMessageDialog.RequestTeleport:
                if (RLV.Enabled && RLV.Permissions.IsAutoAcceptTp(msg.FromAgentID.Guid))
                {
                    Client.Self.TeleportLureRespond(msg.FromAgentID, msg.IMSessionID, true);
                }
                else
                {
                    NotificationReceived?.Invoke(this, NotificationViewModel.ForTeleportOffer(Client, msg));
                    MediaManager.PlayUISound(UISounds.Alert);
                }
                break;

            case InstantMessageDialog.RequestLure:
                if (RLV.Enabled && RLV.Permissions.IsAutoAcceptTpRequest(msg.FromAgentID.Guid))
                {
                    Client.Self.TeleportLureRespond(msg.FromAgentID, msg.IMSessionID, true);
                }
                else
                {
                    NotificationReceived?.Invoke(this, NotificationViewModel.ForTeleportRequest(Client, msg));
                    MediaManager.PlayUISound(UISounds.Alert);
                }
                break;

            case InstantMessageDialog.MessageBox:
                NotificationReceived?.Invoke(this, NotificationViewModel.ForGenericMessage("Message", msg.Message));
                MediaManager.PlayUISound(UISounds.Alert);
                break;
        }
    }

    private void NetCom_AlertMessageReceived(object? sender, AlertMessageEventArgs e)
    {
        if (e.NotificationId == "RegionRestartMinutes")
        {
            int minutes = e.ExtraParams?["MINUTES"].AsInteger() ?? 0;
            string regionName = e.ExtraParams?["NAME"].AsString() ?? string.Empty;
            var vm = NotificationViewModel.ForRegionRestart(Client, regionName, minutes * 60);
            NotificationReceived?.Invoke(this, vm);
        }
        else if (e.NotificationId == "RegionRestartSeconds")
        {
            int seconds = e.ExtraParams?["SECONDS"].AsInteger() ?? 0;
            string regionName = e.ExtraParams?["NAME"].AsString() ?? string.Empty;
            var vm = NotificationViewModel.ForRegionRestart(Client, regionName, seconds);
            NotificationReceived?.Invoke(this, vm);
        }
    }

    public override void ShowNotificationInChat(string message, ChatBufferTextStyle style = ChatBufferTextStyle.ObjectChat, bool highlight = false)
    {
        NotificationInChat?.Invoke(this, new NotificationChatEventArgs(message, style, highlight));
    }

    public override void AddNotification(INotification notification) { }
    public override void RemoveNotification(INotification notification) { }
    public override void ShowAgentProfile(string agentName, UUID agentID)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var vm = new AvatarProfileViewModel(this, agentName, agentID);
            var panel = new AvatarProfilePanel { DataContext = vm };
            var window = new ProfileWindow($"Profile - {agentName}", panel);
            window.Show();
        });
    }

    public override void ShowGroupProfile(UUID groupId)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var vm = new GroupProfileViewModel(this, groupId);
            var panel = new GroupProfilePanel { DataContext = vm };
            var window = new ProfileWindow($"Group Profile", panel);
            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(GroupProfileViewModel.GroupName))
                    window.Title = $"Group - {vm.GroupName}";
            };
            window.Show();
        });
    }
    public void ShowLandProfile()
    {
        Dispatcher.UIThread.Post(() =>
        {
            var vm = new LandProfileViewModel(this);
            var panel = new LandProfilePanel { DataContext = vm };
            var window = new ProfileWindow("Land Info", panel);
            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(LandProfileViewModel.ParcelName))
                    window.Title = $"Land - {vm.ParcelName}";
            };
            window.Show();
        });
    }

    public void ShowLandHoldings()
    {
        Dispatcher.UIThread.Post(() =>
        {
            var vm = new LandHoldingsViewModel(this);
            var panel = new LandHoldingsPanel { DataContext = vm };
            var window = new ProfileWindow("Land Holdings", panel);
            window.Show();
        });
    }

    public void ShowDirectorySearch()
    {
        Dispatcher.UIThread.Post(() =>
        {
            var vm = new DirectorySearchViewModel(this);
            var panel = new DirectorySearchPanel { DataContext = vm };
            var window = new ProfileWindow("Search", panel);
            window.Width = 820;
            window.Height = 650;
            window.Closed += (_, _) => vm.Dispose();
            window.Show();
        });
    }

    public void ShowEstateProfile()
    {
        Dispatcher.UIThread.Post(() =>
        {
            var vm = new EstateProfileViewModel(this);
            var panel = new EstateProfilePanel { DataContext = vm };
            var window = new ProfileWindow($"Region - {vm.RegionName}", panel);
            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(EstateProfileViewModel.RegionName))
                    window.Title = $"Region - {vm.RegionName}";
            };
            window.Show();
        });
    }

    public void ShowObjectContents(UUID objectId, uint localId, string objectName)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var vm = new ObjectContentsViewModel(this, objectId, localId, objectName);
            var panel = new Views.ObjectContentsPanel { DataContext = vm };
            var window = new Views.ProfileWindow($"Contents - {objectName}", panel);
            window.Show();
        });
    }

    public override void ShowLocation(string region, int x, int y, int z) { }

    public override void RegisterContextAction(Type omvType, string label, EventHandler handler) { }
    public override void DeregisterContextAction(Type omvType, string label) { }

    public void ShowMuteList()
    {
        Dispatcher.UIThread.Post(() =>
        {
            var vm = new MuteListViewModel(this);
            var panel = new Views.MuteListPanel { DataContext = vm };
            var window = new Views.ProfileWindow("Mute List", panel);
            window.Closed += (_, _) => vm.Dispose();
            window.Show();
        });
    }

    public void ShowTextureViewer(UUID textureId, string name)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var vm = new TextureViewerViewModel(this, textureId, name);
            var panel = new Views.TextureViewerPanel { DataContext = vm };
            var window = new Views.ProfileWindow($"Texture - {name}", panel);
            window.Show();
        });
    }

    private void OnMqttCommand(object? sender, MqttCommandEventArgs e)
    {
        var topic = e.Topic;
        var payload = e.Payload;

        // Topic format: {rootTopic}/cmd/{command}
        var parts = topic.Split('/');
        if (parts.Length < 2) return;
        var command = parts[^1];

        switch (command)
        {
            case "chat":
                NetCom.ChatOut(payload, ChatType.Normal, 0);
                break;

            case "teleport":
                HandleMqttTeleport(payload);
                break;

            case "avatars":
                _ = PublishAvatarsAsync();
                break;
        }
    }

    private void HandleMqttTeleport(string payload)
    {
        string? region = null;
        float x = 128, y = 128, z = 25;

        if (!string.IsNullOrWhiteSpace(payload))
        {
            try
            {
                using var doc = JsonDocument.Parse(payload);
                var root = doc.RootElement;
                if (root.TryGetProperty("region", out var r))
                    region = r.GetString();
                if (root.TryGetProperty("x", out var px))
                    x = px.GetSingle();
                if (root.TryGetProperty("y", out var py))
                    y = py.GetSingle();
                if (root.TryGetProperty("z", out var pz))
                    z = pz.GetSingle();
            }
            catch
            {
                // treat raw string as region name
                region = payload;
            }
        }

        var sim = Client.Network.CurrentSim;
        if (string.IsNullOrWhiteSpace(region) && sim != null)
            region = sim.Name;

        if (string.IsNullOrWhiteSpace(region)) return;

        Task.Run(() => Client.Self.Teleport(region, new Vector3(x, y, z)));
    }

    private async Task PublishAvatarsAsync()
    {
        if (!Mqtt.IsConnected || !Mqtt.Config.SubscribeCommands) return;

        var sim = Client.Network.CurrentSim;
        if (sim == null) return;

        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);
        writer.WriteStartArray();

        foreach (var kv in sim.ObjectsAvatars)
        {
            var avatar = kv.Value;
            if (avatar == null || avatar.ID == UUID.Zero) continue;

            sim.AvatarPositions.TryGetValue(avatar.ID, out var localPos);
            var globalPos = PositionHelper.ToGlobalPosition(sim.Handle, localPos);

            writer.WriteStartObject();
            writer.WriteString("id", avatar.ID.ToString());
            writer.WriteString("name", Names.Get(avatar.ID));
            writer.WriteNumber("x", Math.Round(globalPos.X, 1));
            writer.WriteNumber("y", Math.Round(globalPos.Y, 1));
            writer.WriteNumber("z", Math.Round(globalPos.Z, 1));
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.Flush();

        var json = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        await Mqtt.PublishAsync("location/avatars", json);
    }

    public override void CleanUp()
    {
        Client.Self.ScriptDialog -= Self_ScriptDialog;
        Client.Self.ScriptQuestion -= Self_ScriptQuestion;
        Client.Self.LoadURL -= Self_LoadURL;
        Client.Self.TeleportProgress -= Self_TeleportProgress;
        NetCom.InstantMessageReceived -= NetCom_InstantMessageReceived;
        NetCom.AlertMessageReceived -= NetCom_AlertMessageReceived;
        Mqtt.CommandReceived -= OnMqttCommand;
        Mqtt.Dispose();
        LuaPlugins.Dispose();
        Vpn.Dispose();
        ChatLog.Dispose();
        base.CleanUp();
    }

    private void Self_TeleportProgress(object? sender, TeleportEventArgs e)
    {
        if (e.Status == TeleportStatus.Finished)
        {
            MediaManager.PlayUISound(UISounds.Teleport);

            if (Mqtt.IsConnected && Mqtt.Config.PublishLocation)
                _ = PublishAvatarsAsync();
        }
    }
}

public class NotificationChatEventArgs : EventArgs
{
    public string Message { get; }
    public ChatBufferTextStyle Style { get; }
    public bool Highlight { get; }

    public NotificationChatEventArgs(string message, ChatBufferTextStyle style, bool highlight)
    {
        Message = message;
        Style = style;
        Highlight = highlight;
    }
}

public class IMRequestedEventArgs : EventArgs
{
    public UUID AgentId { get; }
    public string AgentName { get; }

    public IMRequestedEventArgs(UUID agentId, string agentName)
    {
        AgentId = agentId;
        AgentName = agentName;
    }
}

public class GroupIMRequestedEventArgs : EventArgs
{
    public UUID GroupId { get; }
    public string GroupName { get; }

    public GroupIMRequestedEventArgs(UUID groupId, string groupName)
    {
        GroupId   = groupId;
        GroupName = groupName;
    }
}
