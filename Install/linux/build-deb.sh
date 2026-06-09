#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"

# Find dotnet
if ! command -v dotnet &>/dev/null; then
    if [ -f "$HOME/.dotnet/dotnet" ]; then
        export PATH="$HOME/.dotnet:$PATH"
    elif [ -f "/usr/share/dotnet/dotnet" ]; then
        export PATH="/usr/share/dotnet:$PATH"
    else
        echo "ERROR: dotnet not found. Install .NET 8 SDK first:"
        echo "  curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --channel 8.0"
        exit 1
    fi
fi

ARCH="amd64"
PKG_NAME="radegast-veles"
VERSION="${VERSION:-$(git -C "$REPO_DIR" describe --tags --always --dirty 2>/dev/null || echo "0.1.0")}"
VERSION="${VERSION#v}"
DEB_DIR="/tmp/$PKG_NAME-deb"
BUILD_DIR="/tmp/$PKG_NAME-build"
PKG_DIR="$DEB_DIR/${PKG_NAME}_${VERSION}_${ARCH}"

echo "=== Radegast Veles .deb Builder ==="
echo "Version: $VERSION"
echo "Arch:    $ARCH"
echo ""

# Step 1: Build
echo "[1/5] Publishing RadegastVeles (self-contained)..."
rm -rf "$BUILD_DIR"
dotnet publish "$REPO_DIR/RadegastVeles/RadegastVeles.csproj" \
    -c Release -r linux-x64 --self-contained \
    -o "$BUILD_DIR" 2>&1

# Step 2: Fix native libraries
echo "[2/5] Copying native libraries..."

NATIVE_SRC="$HOME/.nuget/packages/skiasharp.nativeassets.linux.nodependencies"
NATIVE_VER=$(ls -1 "$NATIVE_SRC" 2>/dev/null | sort -V | tail -1)
if [ -n "$NATIVE_VER" ]; then
    cp "$NATIVE_SRC/$NATIVE_VER/runtimes/linux-x64/native/libSkiaSharp.so" \
       "$BUILD_DIR/libSkiaSharp.so"
fi

# Copy FMOD if bundled (csproj should handle this, but ensure it's there)
if [ ! -f "$BUILD_DIR/libfmod.so" ] && [ -f "$REPO_DIR/Radegast.Core/assemblies/libfmod.so.12.10" ]; then
    cp "$REPO_DIR/Radegast.Core/assemblies/libfmod.so.12.10" "$BUILD_DIR/libfmod.so"
fi

# Step 3: Build VPN helper (optional)
echo "[3/5] Building VPN helper..."
VPN_HELPER_SRC="$REPO_DIR/RadegastVeles/VPN/helper"
if [ -d "$VPN_HELPER_SRC" ] && [ -f "$VPN_HELPER_SRC/go.mod" ] && command -v go &>/dev/null; then
    (cd "$VPN_HELPER_SRC" && go build -o "$BUILD_DIR/vpn-helper" .) 2>&1 || true
fi

# Step 4: Create package structure
echo "[4/5] Assembling package..."

rm -rf "$DEB_DIR"
mkdir -p "$PKG_DIR/DEBIAN"
mkdir -p "$PKG_DIR/usr/lib/$PKG_NAME"
mkdir -p "$PKG_DIR/usr/bin"
mkdir -p "$PKG_DIR/usr/share/applications"
mkdir -p "$PKG_DIR/usr/share/icons/hicolor/256x256/apps"
mkdir -p "$PKG_DIR/usr/share/doc/$PKG_NAME"

# Copy binaries
cp -r "$BUILD_DIR"/* "$PKG_DIR/usr/lib/$PKG_NAME/"

# Symlink in /usr/bin
ln -s "/usr/lib/$PKG_NAME/RadegastVeles" "$PKG_DIR/usr/bin/$PKG_NAME"

# Desktop file
sed "s|__INSTALL_DIR__|/usr/lib/$PKG_NAME|g" "$SCRIPT_DIR/radegast-veles.desktop" \
    > "$PKG_DIR/usr/share/applications/$PKG_NAME.desktop"

# Icon
if [ -f "$REPO_DIR/Radegast/radegast.png" ]; then
    cp "$REPO_DIR/Radegast/radegast.png" "$PKG_DIR/usr/share/icons/hicolor/256x256/apps/$PKG_NAME.png"
fi

# Copyright
cat > "$PKG_DIR/usr/share/doc/$PKG_NAME/copyright" << 'EOF'
Format: https://www.debian.org/doc/packaging-manuals/copyright-format/1.0/
Upstream-Name: Radegast Veles
Upstream-Contact: https://github.com/DDynamic-Evolution/radegastLinux

Files: *
Copyright: 2009-2014, Radegast Development Team
           2016-2026, Sjofn LLC
           2025-2026, Disvail Dynamic
License: LGPL-3.0+

Files: Install/*
Copyright: 2025-2026, Disvail Dynamic, Miko Astral
License: LGPL-3.0+

License: LGPL-3.0+
 This program is free software: you can redistribute it and/or modify
 it under the terms of the GNU Lesser General Public License as published
 by the Free Software Foundation, either version 3 of the License, or
 (at your option) any later version.
 .
 This program is distributed in the hope that it will be useful,
 but WITHOUT ANY WARRANTY; without even the implied warranty of
 MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 GNU General Public License for more details.
 .
 You should have received a copy of the GNU Lesser General Public License
 along with this program.  If not, see <https://www.gnu.org/licenses/>.
EOF

# Control file
cat > "$PKG_DIR/DEBIAN/control" << EOF
Package: $PKG_NAME
Version: $VERSION
Architecture: $ARCH
Maintainer: Miko Astral <miko@disvail.com>
Installed-Size: $(du -sk "$PKG_DIR/usr" | cut -f1)
Section: games
Priority: optional
Homepage: https://github.com/DDynamic-Evolution/radegastLinux
Description: Lightweight Second Life and OpenSimulator client (NG)
 Radegast Veles is a virtual world client compatible with Second Life
 and OpenSimulator. It provides an alternative to Linden Lab derived
 virtual world viewers with a strong focus on accessibility and
 non-3D interaction.
 .
 This is the next-generation Avalonia/.NET 8 client (RadegastVeles)
 with full RLV support, MQTT, optional WireGuard VPN, and FMOD audio.
EOF

# postinst - register icon cache, setcap for VPN helper, etc.
cat > "$PKG_DIR/DEBIAN/postinst" << 'POSTINST'
#!/bin/sh
set -e

PKG_NAME=radegast-veles

# Update icon cache
if command -v gtk-update-icon-cache >/dev/null 2>&1; then
    gtk-update-icon-cache -f -t /usr/share/icons/hicolor 2>/dev/null || true
fi

# Update desktop database
if command -v update-desktop-database >/dev/null 2>&1; then
    update-desktop-database 2>/dev/null || true
fi

# Set CAP_NET_ADMIN on VPN helper if it exists
VPN_HELPER="/usr/lib/$PKG_NAME/vpn-helper"
if [ -f "$VPN_HELPER" ] && command -v setcap >/dev/null 2>&1; then
    setcap cap_net_admin+ep "$VPN_HELPER" 2>/dev/null || true
fi

exit 0
POSTINST
chmod 755 "$PKG_DIR/DEBIAN/postinst"

# prerm
cat > "$PKG_DIR/DEBIAN/prerm" << 'PRERM'
#!/bin/sh
set -e
# Nothing special needed yet
exit 0
PRERM
chmod 755 "$PKG_DIR/DEBIAN/prerm"

# Step 5: Build .deb
echo "[5/5] Building .deb package..."
fakeroot dpkg-deb --build "$PKG_DIR" 2>&1

# Copy result
mv "$DEB_DIR/${PKG_NAME}_${VERSION}_${ARCH}.deb" "$REPO_DIR/"

echo ""
echo "=== Done! ==="
echo "Package: $REPO_DIR/${PKG_NAME}_${VERSION}_${ARCH}.deb"
echo ""
echo "Install with:"
echo "  sudo dpkg -i ${PKG_NAME}_${VERSION}_${ARCH}.deb"
echo ""
echo "Remove with:"
echo "  sudo apt remove $PKG_NAME"

# Cleanup
rm -rf "$BUILD_DIR" "$DEB_DIR"
