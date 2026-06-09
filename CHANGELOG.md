# Changelog – Radegast Veles

## 0.1.1 – 2026-06-09

### Added

- **MQTT teleport command** – `cmd/teleport` accepts JSON `{"region","x","y","z"}` to teleport remotely
- **MQTT avatar list command** – `cmd/avatars` publishes nearby avatars to `location/avatars`; auto-publishes on teleport finish when `PublishLocation` is enabled
- **Display name mode** – Preferences General tab: Standard / Smart / Display name only / Display name + username
- **RLV incoming IM auto-reply** – Sends `BusyAutoResponse` when an incoming IM is blocked by RLV restrictions (matching WinForms behavior)
- **Repository link** – `https://github.com/DDynamic-Evolution/radegastLinux` button in About window
- **Linux Maintainer credit** – "Miko Astral" added to Credits tab in About window

### Changed

- **AVX → SSE2** – EnableProfileOptimization (AVX) replaced with SSE2 in csproj for broader CPU compatibility
- **About window** – Credits tab lists "Miko Astral" as Linux Maintainer; About tab links to the DDynamic-Evolution GitHub repository

### Removed

- **Undock/detach feature** – Removed undock buttons (⏏) from all MainWindow tab headers (Nearby, IMs, Map, Objects, Inventory, Friends, Groups, Media); removed `UndockPanel`/`DockPanel` methods and `_detachedPanels`/`_panelTitles` fields. `PanelHostWindow` kept for inventory-item detach in `InventoryPanel`

### Fixed

- **RLV settings not saving** – `RlvManager.Enabled` setter compared `Enabled` getter (reads `GlobalSettings["rlv_enabled"]`) against the same `GlobalSettings` key, making the condition always `false`. The new value was never persisted
- **RLV commands showing in chat** – Added `RlvManager.ProcessCMD()` call for `OwnerSay` `@`-prefixed messages (was missing in Veles; WinForms client had it). Commands are now processed and hidden from chat
- **RLV chat censorship** – Added `CanReceiveChat` check in `NetCom_ChatReceived` to censor messages when RLV blocks receiving (matching WinForms `ChatTextManager.cs` behavior)
- **FMOD audio on Linux** – Replaced 0-byte `libfmod.so` symlinks; added `DllImportResolver` in `NativeMethods.cs`; added PostBuild copy of `libfmod.so.12.10` to output; fixed `FMOD5_System_Create` DllImport to include `headerversion` parameter
- **Display name in chat** – `NearbyViewModel` now uses `_instance.Names.Get(e.SourceID, e.FromName)` instead of raw `e.FromName`

## 0.1 – 2026-06-08

Initial release of Radegast Veles – the Avalonia/.NET 8 next-generation client.

### Added

- **Avalonia UI** – Cross-platform desktop UI (Linux, Windows, macOS) replacing WinForms
- **MVVM architecture** – CommunityToolkit.Mvvm with source generators (`[ObservableProperty]`, `[RelayCommand]`)
- **RLV (Restrained Love Viewer)** – Full RLV engine with chat, IM, teleport, and permissions enforcement (enabled by default)
- **WireGuard VPN** – Integrated VPN tunnel manager with a Go helper binary and JSON-line IPC
- **MQTT integration** – Publish chat/IM events to an MQTT broker and receive remote commands (`cmd/chat`)
  - Configurable broker, credentials, TLS, root topic, QoS, and publish options
- **Chat** – Nearby chat with whisper/normal/shout, RLV redirection, gesture support, channel commands
- **Instant Messaging** – Personal, group, and conference IM sessions with RLV receive-block
- **Script dialogs** – Permissions, load URL, teleport offers, inventory offers, friendship offers
- **Group notices & invitations**
- **Region restart notifications**
- **Profile windows** – Agent profile, group profile, land info, land holdings, estate info, directory search
- **Object inspector** – Properties, contents, metadata, take/copy, buy, pay, mute, delete, return
- **Inventory** – Folders, items, wearables, textures, sounds, animations, notecards, landmarks, gestures, scripts, materials
- **Texture viewer** – Display in-world textures
- **Map** – World map with teleport
- **Media** – Audio playback, streaming, object sounds with configurable volumes and profiles
- **Chat logging** – Configurable log directory and enable/disable
- **Preferences** – Image cache, audio, voice, grids, RLV, VPN, MQTT settings
- **Grid manager** – Add/remove/edit custom grids (agni/aditi built-in)
- **Linux install script** – `Install/linux/install.sh` with `.desktop` integration
- **Notification queue** – Centralized in-world notifications (script dialogs, offers, alerts)
- **`AGENTS.md`** – Project context for AI-assisted development

### Changed

- **3D View removed** – Legacy OpenTK 3.x and Veles OpenGL rendering engine stripped from Linux build
- **RLV defaults to enabled** – `rlv_enabled` now defaults to `true` in `RlvManager`
- **Dependency updates**
  - LibreMetaverse v2.6.4
  - Avalonia 11.3.13
  - CommunityToolkit.Mvvm 8.4.2
  - SkiaSharp 3.119.2
  - MQTTnet 5.1.0

### Removed

- All files under `RadegastVeles/Rendering/` (OpenGL 3D engine: `GlViewportControl`, shaders, mesh builders, avatar/HUD viewers)
- `PrimViewerViewModel`, `AvatarViewerViewModel`, `HudViewerViewModel`
- `PrimViewerPanel`, `AvatarViewerPanel`, `HudViewerPanel` (AXAML + code-behind)
- OpenTK.Graphics and OpenTK.Mathematics package references
- Legacy WinForms rendering (`Radegast/GUI/Rendering/`) – not part of Veles
- **Voice chat removed** – `VoiceViewModel.cs`, `LibreMetaverse.Voice.WebRTC` package, voice toolbar/PTT in MainWindow, Voice preferences tab, voice indicator in ChatPanel, Voice button in GroupProfilePanel, voice permission displays in Land/Estate profiles, voice properties from PreferencesViewModel, `Voice` property from RadegastInstanceAvalonia and NearbyViewModel

### Fixed

- OpenGL ES vs desktop detection in GL initialization
- COF (Current Outfit Folder) robustness fixes
- Nullable warnings in Radegast.Core
