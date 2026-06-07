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
| `RadegastVeles/Core/RadegastInstanceAvalonia.cs` | RLV auto-accept/deny (ScriptQuestion, RequestTeleport, RequestLure) |
| `RadegastVeles/ViewModels/NearbyViewModel.cs` | `ProcessChatInput()` – redirchat, rediremote, chatchtype, sendchat |
| `RadegastVeles/ViewModels/IMViewModel.cs` | `SendMessage()` – sendim; `HandlePersonalIM/GroupIM/ConferenceIM` – recvim |
| `RadegastVeles/ViewModels/PreferencesViewModel.cs` | `RlvEnabled` / `RlvDebugEnabled` properties |
| `RadegastVeles/Views/PreferencesWindow.axaml` | RLV TabItem in Preferences |

### WireGuard VPN
| File | Purpose |
|---|---|
| `RadegastVeles/VPN/helper/main.go` | Go helper binary (WireGuard via wgctrl + netlink). Build: `go build -o vpn-helper .` |
| `RadegastVeles/VPN/VpnManager.cs` | C# manager – starts/stops helper, JSON line IPC |
| `RadegastVeles/VPN/VpnProfile.cs` | Profile/Peer models (JSON serialisable) |
| `RadegastVeles/ViewModels/VpnViewModel.cs` | Profile list, add/edit/remove/connect/disconnect commands |
| `RadegastVeles/Core/RadegastInstanceAvalonia.cs` | VpnManager init (line 80-81) |

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

## Important Context

### RLV
- `GlobalSettings["rlv_enabled"]` / `GlobalSettings["rlv_debug"]` control RLV state
- RLV engine is in `Radegast.Core/RLV/` – no modifications needed there
- All enforcement is in the ViewModel layer (chat input, IM send, IM receive)

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
