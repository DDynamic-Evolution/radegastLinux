```
██████╗  █████╗ ██████╗ ███████╗ ██████╗  █████╗ ███████╗████████╗    
██╔══██╗██╔══██╗██╔══██╗██╔════╝██╔════╝ ██╔══██╗██╔════╝╚══██╔══╝    
██████╔╝███████║██║  ██║█████╗  ██║  ███╗███████║███████╗   ██║       
██╔══██╗██╔══██║██║  ██║██╔══╝  ██║   ██║██╔══██║╚════██║   ██║       
██║  ██║██║  ██║██████╔╝███████╗╚██████╔╝██║  ██║███████║   ██║       
╚═╝  ╚═╝╚═╝  ╚═╝╚═════╝ ╚══════╝ ╚═════╝ ╚═╝  ╚═╝╚══════╝   ╚═╝     
```
Radegast Metaverse Client

[![License: LGPL v3](https://img.shields.io/badge/License-LGPL%20v3-blue.svg)](https://github.com/cinderblocks/radegast/blob/master/LICENSE.txt)
[![Download .deb](https://img.shields.io/badge/Download-.deb-brightgreen)](https://github.com/DDynamic-Evolution/radegastLinux/releases/download/v2.54/radegast-veles_2.53-15-gb1da94f8_amd64.deb)

## Getting started

Radegast is a virtual world client compatible with Second Life and OpenSimulator.
Its main purpose is to provide an alternative client to Linden Lab derived virtual world viewers.
There is a strong focus on accessability and non-3D interaction.

### Prerequisites

- **Radegast (legacy):** .NET Framework 4.8 or compatible Mono version
- **RadegastVeles (Avalonia/NG):** [.NET 8.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)

### Building for Linux (native)

#### Prerequisites

- **.NET 8.0 SDK** – install with:
  ```bash
  curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --channel 8.0
  export PATH="$HOME/.dotnet:$PATH"
  ```
- **Go** (optional, for WireGuard VPN) – available at `/usr/bin/go` or install via your package manager.

#### Quick install (recommended)

```bash
./Install/linux/install.sh
```

This builds RadegastVeles, copies the `libSkiaSharp.so` native library, and installs everything to `~/.local/share/radegast-veles` with a desktop shortcut.

#### Manual build

```bash
# 1. Publish the main application
dotnet publish RadegastVeles/RadegastVeles.csproj \
    -c Release -r linux-x64 --self-contained \
    -o dist

# 2. Fix SkiaSharp native library (required for compatibility)
cp ~/.nuget/packages/skiasharp.nativeassets.linux.nodependencies/3.119.2/runtimes/linux-x64/native/libSkiaSharp.so dist/libSkiaSharp.so

# 3. (Optional) Build the WireGuard VPN helper
cd RadegastVeles/VPN/helper
go build -o vpn-helper .
sudo setcap cap_net_admin+ep vpn-helper
cp vpn-helper ../../dist/

# 4. Run
./dist/RadegastVeles
```

> **Note:** If `libSkiaSharp.so` is missing, the application will crash at startup. The version path above assumes SkiaSharp 3.119.2 – check `~/.nuget/packages/skiasharp.nativeassets.linux.nodependencies/` if a different version was resolved.

### WireGuard VPN (optional)

RadegastVeles supports tunneled connections via a small Go helper binary.

#### Building the VPN helper

```bash
cd RadegastVeles/VPN/helper
go build -o vpn-helper .
sudo setcap cap_net_admin+ep vpn-helper
# Copy the binary next to the RadegastVeles executable
cp vpn-helper ../../../dist/
```

The helper needs `CAP_NET_ADMIN` (set via `setcap`) to create WireGuard tunnel interfaces. This is a one-time step.

#### Managing profiles

Open Preferences → VPN to add, edit, and remove tunnel profiles. Profiles are stored in `GlobalSettings` and persist across sessions.

Each profile contains:
- **Private Key** – the WireGuard private key for the client
- **Address** – the tunnel IP (e.g. `10.0.0.2/24`)
- **Peer Public Key** – the server's public key
- **Endpoint** – the server's public address and port (e.g. `1.2.3.4:51820`)
- **Allowed IPs** – comma-separated subnets to route through the tunnel (e.g. `0.0.0.0/0` for all traffic)
- **Keepalive (s)** – persistent keepalive interval in seconds (0 = off)

> **Note:** Default-route (`0.0.0.0/0`) support requires fwmark + routing table logic which is not yet implemented. Currently only explicit subnets are routed through the tunnel.

The VPN tab is always visible. If the `vpn-helper` binary is not found, a setup hint is shown.

### RLV (Restrained Love Viewer)

RLV support is built into RadegastVeles and can be enabled in Preferences → RLV.

#### Chat enforcement
- **`@redirchat` / `@rediremote`** – incoming chat/emotes are redirected to a specified channel
- **`@chatchtype`** – outgoing chat is demoted (shout → normal → whisper) based on RLV rules
- **`@sendchat:`** – when `CanSendChat` is denied, outgoing text is replaced with `"..."`
- **`@sendim:`** – when `CanSendIM` is denied, IM sending is blocked (personal, group, conference)
- **`@recvim:`** – when `CanReceiveIM` is denied, incoming messages are replaced with `"*** IM blocked by your viewer"`

#### Auto-accept / auto-deny
- **`@acceptpermission`** – script permission dialogs are automatically accepted
- **`@denypermission`** – script permission dialogs are automatically denied
- **`@accepttp` / `@accepttprequest`** – teleport offers/requests are automatically accepted

### Building for Windows (legacy)
Open `Radegast.sln` in Visual Studio 2022+ and build for ReleaseWindows.

### Contributing

Pull requests are nice. Try not to be a dick, and we will all get along just fine.

## Authors

### Project founder:

* **Latif Khalifa**

### Windows maintainer:

* **Cinder Roxley** (email cinder sdf.org)

### Linux maintainer:

* **Miko Astral**

### Contributors:

* **nooperation**
* **Luminous Luminos**

### Alumni Contributors:

* **Douglas R. Miles**
* **Mojito Sorbet**
* **Robin Cornelius**
* **Revolution Smythe**

## License

**Radegast Metaverse Client**
* Copyright © 2009–2014, Radegast Development Team
* Copyright © 2016–2026, Sjofn LLC
* Copyright © 2025–2026, Disvail Dynamic
* All rights reserved.

 Radegast is free software: you can redistribute it and/or modify it under the terms of the GNU Lesser General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.

 This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.

## Contributors

<a href="https://github.com/cinderblocks/Radegast/graphs/contributors">
  <img src="https://contrib.rocks/image?repo=cinderblocks/Radegast" />
</a>

## Acknowledgments

### Libraries

Based on SLeek
Copyright © 2006-2008, Paul Clement (a.k.a. Delta)
All rights reserved.

LibreMetaverse (including LslTools, PrimMesher, Meshmerizer, RLV, Voice.Vivox, Voice.WebRTC)
Copyright © 2024-2026 Sjofn LLC
https://github.com/cinderblocks/libremetaverse

Avalonia UI Framework (Avalonia, AvaloniaEdit, Avalonia.Desktop, Themes.Fluent, Fonts.Inter)
Copyright © 2024 Avalonia Contributors
https://github.com/AvaloniaUI/Avalonia — MIT License

CommunityToolkit.Mvvm
Copyright © 2024 Microsoft Corporation
https://github.com/CommunityToolkit/dotnet — MIT License

Newtonsoft.Json
Copyright © 2024 Newtonsoft
https://www.newtonsoft.com/json — MIT License

Microsoft.Extensions.Logging
Copyright © 2024 Microsoft Corporation
https://github.com/dotnet/extensions — MIT License

Microsoft.Bcl.AsyncInterfaces
Copyright © 2024 Microsoft Corporation
https://github.com/dotnet/corefx — MIT License

BugSplatDotNetStandard
Copyright © 2024 BugSplat
https://bugsplat.com — MIT License

NetSparkleUpdater
Copyright © 2024 NetSparkleUpdater Contributors
https://github.com/NetSparkleUpdater/NetSparkleUpdater — MIT License

PrimMesher
Copyright © 2010 Dahlia Trimble
http://forge.opensimulator.org/gf/project/primmesher/

CoreJ2K
Copyright © 2024-2025 Sjofn LLC

AIMLBot
Copyright © 2006 Nicholas H.Tollervey (http://ntoll.org)

CommandLineParser
Copyright © 2005 - 2015 Giacomo Stelluti Scala & Contributors

CSAT Library
Copyright © 2011 mjt

log4net
Copyright © 2004-2024 Apache Software Foundation

Ogg Vorbis
Copyright © 2002, Xiph.org Foundation

OpenTK 3D
Copyright © 2006-2014 Stefanos Apostolopoulos <stapostol@gmail.com> for the Open Toolkit library.

OpenTK.Graphics / OpenTK.Mathematics
Copyright © 2024 OpenTK Contributors
https://github.com/opentk/opentk — MIT License

MemoryPack
Copyright © 2022 Cysharp, Inc.

SkiaSharp
Copyright © 2015-2016 Xamarin, Inc.

SmartIrc4net
Copyright © 2003-2005 Mirco Bauer
http://www.meebey.net/projects/smartirc4net/

XmlRpcCore
Copyright © 2020-2024, Sjofn LLC.

zlib.net
Copyright © 1996-2017 Greg Roelofs, Jean-loup Gailly and Mark Adler.

### Testing

NUnit / NUnit3TestAdapter / NUnit.Analyzers
Copyright © 2024 Charlie Poole, Rob Prouse
https://github.com/nunit/nunit — MIT License

### WireGuard VPN

WireGuard Go Bindings (golang.zx2c4.com/wireguard/wgctrl)
Copyright © 2024 Jason A. Donenfeld
https://www.wireguard.com — MIT License

netlink (github.com/vishvananda/netlink)
Copyright © 2014 Vishvananda Ishaya
https://github.com/vishvananda/netlink — Apache License 2.0

### Artwork

Artwork files are licenced under the
Creative Commons Share and Share Alike 3.0
Copyright © 2002-2009 Linden Research Inc.
