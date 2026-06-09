# Radegast Veles – Agent Context

## Overview
Radegast Metaverse Client (Second Life / OpenSimulator). This fork (`DDynamic-Evolution/radegastLinux`) focusses on **RadegastVeles** – the Avalonia/.NET 8 NG client – with full **RLV** support and optional **WireGuard VPN**.

## Key Files & Locations

### Build & Install
| File | Purpose |
|---|---|
| `RadegastVeles/RadegastVeles.csproj` | Main Avalonia project |
| `Radegast.Core/Radegast.Core.csproj` | Core library (multi-targets netstandard2.0;net8.0) |
| `Install/linux/install.sh` | Linux install script (no sudo) |
| `Install/linux/radegast-veles.desktop` | Desktop shortcut |
| `.gitignore` | Excludes bin/obj/packages/*.user etc. |

### RLV (Restrained Love Viewer)
| File | Purpose |
|---|---|
| `Radegast.Core/RLV/` | Full RLV engine (RlvManager, RlvActionCallbacks, RlvQueryCallbacks, RlvCOFPolicy). No changes needed. |
| `Radegast.Core/RLV/RlvManager.cs` | `Enabled` setter at line 48: always persists to `GlobalSettings["rlv_enabled"]` (was broken – compared getter vs same key, never saving) |
| `RadegastVeles/Core/RadegastInstanceAvalonia.cs` | RLV auto-accept/deny (ScriptQuestion, RequestTeleport, RequestLure) |
| `RadegastVeles/ViewModels/NearbyViewModel.cs` | `ProcessChatInput()` – redirchat, rediremote, chatchtype, sendchat. `NetCom_ChatReceived()` – `ProcessCMD()` for OwnerSay `@`, `CanReceiveChat()` censorship |
| `RadegastVeles/ViewModels/IMViewModel.cs` | `SendMessage()` – sendim; `HandlePersonalIM/GroupIM/ConferenceIM` – recvim with `CanReceiveIM` + auto-reply on personal block |
| `RadegastVeles/ViewModels/PreferencesViewModel.cs` | `RlvEnabled` property |
| `RadegastVeles/Views/PreferencesWindow.axaml` | RLV TabItem in Preferences |
| `RadegastVeles/ViewModels/RlvViewModel.cs` | RLV UI ViewModel (Live-Log, Restrictions, Auto-Response, Trust List) |
| `RadegastVeles/Views/RlvPanel.axaml` | RLV Tab in MainWindow |
| `RadegastVeles/Views/AddTrustedAvatarWindow.axaml` | Dialog for adding trusted avatars |

### WireGuard VPN
| File | Purpose |
|---|---|
| `RadegastVeles/VPN/helper/main.go` | Go helper binary (WireGuard via wgctrl + netlink). Build: `go build -o vpn-helper .` |
| `RadegastVeles/VPN/VpnManager.cs` | C# manager – starts/stops helper, JSON line IPC |
| `RadegastVeles/VPN/VpnProfile.cs` | Profile/Peer models (JSON serialisable) |
| `RadegastVeles/ViewModels/VpnViewModel.cs` | Profile list, add/edit/remove/connect/disconnect commands |
| `RadegastVeles/Core/RadegastInstanceAvalonia.cs` | VpnManager init (line 80-81) |

### MQTT
| File | Purpose |
|---|---|
| `RadegastVeles/MQTT/MqttManager.cs` | MQTT client manager (connect, disconnect, publish, subscribe) |
| `RadegastVeles/MQTT/MqttConfig.cs` | MQTT configuration model (host, port, credentials, topics, auto-connect) |
| `RadegastVeles/ViewModels/MqttViewModel.cs` | MQTT UI ViewModel (settings, auto-connect after login) |
| `RadegastVeles/Views/PreferencesWindow.axaml` | MQTT TabItem in Preferences |

### Teleport Home
| File | Purpose |
|---|---|
| `RadegastVeles/Views/MainWindow.axaml` | Toolbar with "Teleport Home" button |
| `RadegastVeles/ViewModels/MainViewModel.cs` | `TeleportHomeCommand` – calls `Client.Self.RequestTeleport(UUID.Zero)` |

### Build Commands
```bash
# Full build
dotnet build RadegastVeles/RadegastVeles.csproj

# Publish self-contained
dotnet publish RadegastVeles/RadegastVeles.csproj -c Release -r linux-x64 --self-contained -o dist
cp ~/.nuget/packages/skiasharp.nativeassets.linux.nodependencies/3.119.2/runtimes/linux-x64/native/libSkiaSharp.so dist/

# VPN helper
cd RadegastVeles/VPN/helper && go build -o vpn-helper . && sudo setcap cap_net_admin+ep vpn-helper
```

### Audio (FMOD)
| File | Purpose |
|---|---|
| `Radegast.Core/FMOD/NativeMethods.cs` | DllImportResolver for `libfmod.so` on Linux, static pre-load in cctor |
| `Radegast.Core/FMOD/fmod.cs` | FMOD Ex C# wrapper (fmod.cs) – `FMOD5_System_Create` takes 2 params |
| `RadegastVeles/RadegastVeles.csproj` | `<None Include="...libfmod.so.12.10">` copies as `libfmod.so` to build/publish |
| `Radegast.Core/Radegast.Core.csproj` | PostBuild target copies `libfmod.so.12.10` to output |

- `libfmod.so.12.10` is FMOD Studio 2.01.10 (API version 0x00020110)
- The `DllImport` for `FMOD5_System_Create` must include `headerversion` parameter (FMOD Studio 2.x API)
- `FMOD5_System_Create(out IntPtr system, uint headerversion)` – not the 1-param version from fmod.cs
- System has `/usr/lib/x86_64-linux-gnu/libfmod.so.13` (different version, incompatible)
- Deployment dir `~/.local/share/radegast-veles/` needs `libfmod.so` copied from build

### Voice (removed)
- `VoiceViewModel.cs` deleted, `LibreMetaverse.Voice.WebRTC` package removed from csproj
- All voice UI removed: toolbar/PTT from MainWindow, Voice tab from Preferences, voice indicator from ChatPanel, Voice button from GroupProfilePanel, voice permission displays from Land/Estate profiles
- Voice properties and references removed from MainViewModel, RadegastInstanceAvalonia, NearbyViewModel, PreferencesViewModel, GroupProfileViewModel, LandProfileViewModel, EstateProfileViewModel

### Undock (detached panels)
- **Removed** from MainWindow tabs (Nearby, IMs, Map, Objects, Inventory, Friends, Groups, Media) — no more undock buttons, no more UndockPanel/DockPanel methods.
- `PanelHostWindow` still exists and is used by `InventoryPanel.axaml.cs` for its own inventory-item detach feature.
- Tab headers simplified from `StackPanel` wrapping to direct `TextBlock` where the undock button was the only extra child.

## Important Context

### RLV
- `GlobalSettings["rlv_enabled"]` controls RLV state
- RLV engine is in `Radegast.Core/RLV/` – no modifications needed there
- All enforcement is in the ViewModel layer (chat input, IM send, IM receive)
- RLV Tab in MainWindow shows: Live-Log, active Restrictions, Auto-Response settings, Trust List
- RLV debug commands removed from chat output (EnabledDebugCommands property removed)
- Trust List stored in `GlobalSettings["rlv_trusted_avatars"]` as JSON array

### MQTT
- Auto-connect after login if `AutoConnect` is enabled and host is configured (not "localhost")
- Settings stored in `GlobalSettings["mqtt_config"]` as JSON string
- Publish options: chat send/receive, IM send/receive, location
- Subscribe to commands via `{rootTopic}/cmd/+` topic
- `cmd/teleport` accepts JSON `{"region","x","y","z"}` or plain text fallback
- `cmd/avatars` publishes nearby avatar list to `location/avatars` (one-shot); also published on teleport finish

### Chat
- `ShowAllChannels` option in ChatPanel shows messages from all channels (not just channel 0)
- RLV Live-Log can optionally log all chat channels via `LogAllChannels` option

### VPN
- `VpnManager.IsAvailable` checks if `vpn-helper` binary exists next to the executable
- VPN Tab is **always visible** in Preferences; shows hint if helper not found
- Helper needs `CAP_NET_ADMIN` via `sudo setcap cap_net_admin+ep`
- Default-route (0.0.0.0/0) routing not yet implemented – only explicit subnets work
- Profiles stored in `GlobalSettings["vpn_profiles"]` as JSON string

### XAML / Avalonia
- `BoolConverters` (Not, And, Or) and `ObjectConverters` (IsNull, IsNotNull) are built-in `Avalonia.Data.Converters` – no custom converter code needed
- `ObjectConverters.IsNotNull` is the correct converter for null checks, **not** `BoolConverters.NotNull` (which doesn't exist)

### .NET / Dependencies
- .NET 8 SDK required. Install via `dotnet-install.sh --channel 8.0`
- SkiaSharp pinned to 3.119.2 via direct PackageReference in both csproj files
- SkiaSharp native lib must be copied manually after publish (see build commands)
- `Radegast.Core` multi-targets `netstandard2.0;net8.0` – the WinForms test project (`net48`) may have cross-targeting issues (pre-existing)

### Git Remotes
- `origin` → `https://github.com/DDynamic-Evolution/radegastLinux.git`
- `upstream` → `https://github.com/cinderblocks/radegast.git`

## Next Steps
1. Update `install.sh` to build the Go VPN helper and run `sudo setcap cap_net_admin+ep`
2. Implement fwmark + routing table logic for default-route (0.0.0.0/0) support in the helper
3. End-to-end VPN connect/disconnect testing
