# Changelog – Radegast Veles

## 0.1.3 – 2026-06-09

### Added

- **Keyboard shortcuts** – Global shortcuts for tab navigation (Ctrl+1-8), teleport home (Ctrl+H), preferences (Ctrl+,), logout (Ctrl+Q), hide window (Ctrl+W), log viewer (Ctrl+Shift+L). Shortcuts displayed in menu items.
- **Hardware ID spoofing** – Spoofs `id0` and `mac` login parameters using deterministic MD5 hashes (seed + username). Configurable in Preferences → Spoof tab with reroll button. Prevents hardware-based tracking/banning.
- **Emoticon support** – Automatic conversion of text emoticons to Unicode emojis in chat (e.g., `:)` → 😊, `<3` → ❤️, `XD` → 😆). Includes emoticon picker button next to chat input for quick emoji insertion.
- **Map tile disk cache** – World map tiles cached to disk (`~/.local/share/RadegastVeles/mapcache/`) for faster loading on subsequent visits. Configurable cache size (100-2000 MB) and TTL (1-90 days) in Preferences → General.
- **Mute list filtering** – Chat messages and instant messages from muted avatars/objects are now filtered and no longer displayed. Mute list accessible via World → Mute List menu.
- **Friends status in tray** – Tray icon tooltip now shows online friends count (e.g., "Radegast Veles - 5/12 friends online"). Updates automatically when friends come online/offline.
- **Multi-grid dashboard** – New Dashboard window accessible from tray menu shows overview of all active sessions with agent name, grid, region, connection status, and friends online count.
- **RLV debug panel** – RLV panel now has three tabs: Overview (restrictions, sources, auto-response settings), Live Log (real-time RLV command log), and Debug (engine status, permission checks, command log).
- **Inventory batch operations** – Multi-select support in inventory tree (Ctrl+Click, Shift+Click). Batch delete, cut, and copy operations on multiple selected items.
- **Map friend markers** – Friends found via map search are now displayed as gold markers on the world map with name labels. Friend positions are tracked and updated in real-time.
- **Lua scripting plugin interface** – MoonSharp-based engine with lifecycle hooks (`on_start`, `on_stop`, `on_connected`, `on_disconnected`) and event hooks (`on_chat`, `on_im`, `on_teleport`)
- **Lua API functions** – `send_chat`, `send_im`, `teleport`, `log`/`log_info`/`log_warn`/`log_error`, `get_setting`/`set_setting`, `http_get`, `schedule`
- **Lua Scripting Preferences tab** – Script list with running-status indicator, Reload All / Reload Selected / Open Scripts Folder buttons, real-time console output
- **Lua API documentation** – `RadegastVeles/Scripting/API.md` with full reference for script authors
- **Script auto-discovery** – `.lua` files in `~/.local/share/RadegastVeles/scripts/` are detected and loaded on startup; scripts start/stop on connect/disconnect
- **LSL script import/export** – Export individual scripts or batch-export all scripts in a folder to `.lsl` files; import one or more `.lsl` files as new inventory scripts. Accessible via right-click context menus on Script items and folders.
- **New Script in Object Contents** – Create new empty scripts inside an object's task inventory from the Contents panel toolbar or context menu. Creates a "New Script" in your inventory and copies it into the object.

### Changed

- **LuaPlugin callback mechanism** – Replaced broken `RegisterCallback` dictionary with globals lookup (`GetHook`); scripts now use natural assignment (`on_chat = function(...) end`) instead of function-call registration
- **Preferences window** – Now resizable (`CanResize="True"`, `MinWidth="480"`, `MinHeight="400"`); increased default size from 540×500 to 640×540
- **Objects search radius** – NumericUpDown width increased from 100 to 140 for easier editing

### Fixed

- **RLV enabled state not persisting across restarts** – `RlvViewModel._enabled` relied on `instance.RLV?.Enabled` which is `null` before login; toggle before login was silently lost. Now reads/writes `GlobalSettings["rlv_enabled"]` directly via `LoadEnabled()`/`SaveEnabled()`, independent of `RlvManager` lifecycle.
- **LuaPlugin hooks never firing** – `on_start`/`on_stop`/`on_chat`/`on_im` etc. were looked up in an empty `_callbacks` dictionary (the `RegisterCallback` delegate was overwritten by Lua assignment). Now hooks are read directly from `_script.Globals` via `GetHook()`, matching the example script pattern.
- **Lua build errors** – `Logger.Log`→`Logger.Warn` (no 1-arg overload), `FileScriptLoader`→`FileSystemScriptLoader` (MoonSharp 2.0 rename), `ChatEventArgs.Channel/SourceName`→`e.Type`/`e.FromName`

## 0.1.2 – 2026-06-09

Initial stable release of Radegast Veles.

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
