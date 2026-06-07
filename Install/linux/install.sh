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
INSTALL_DIR="${INSTALL_DIR:-$HOME/.local/share/radegast-veles}"
DESKTOP_DIR="${XDG_DATA_HOME:-$HOME/.local/share}/applications"
ICON_DIR="${XDG_DATA_HOME:-$HOME/.local/share}/icons/hicolor/256x256/apps"

echo "=== Radegast Veles Linux Installer ==="
echo ""

# Build
echo "[1/4] Building RadegastVeles..."
cd "$REPO_DIR"
dotnet publish RadegastVeles/RadegastVeles.csproj \
    -c Release -r linux-x64 --self-contained \
    -o /tmp/radegast-veles-build 2>&1

# Fix SkiaSharp native library (use correct version from NoDependencies)
echo "[2/4] Fixing native library compatibility..."
NATIVE_SRC="$HOME/.nuget/packages/skiasharp.nativeassets.linux.nodependencies"
NATIVE_VER=$(ls -1 "$NATIVE_SRC" 2>/dev/null | sort -V | tail -1)
if [ -n "$NATIVE_VER" ]; then
    cp "$NATIVE_SRC/$NATIVE_VER/runtimes/linux-x64/native/libSkiaSharp.so" \
       /tmp/radegast-veles-build/libSkiaSharp.so
fi

# Install
echo "[3/4] Installing to $INSTALL_DIR..."
rm -rf "$INSTALL_DIR"
mkdir -p "$(dirname "$INSTALL_DIR")"
cp -r /tmp/radegast-veles-build "$INSTALL_DIR"

# Desktop integration
echo "[4/4] Adding desktop shortcut..."
mkdir -p "$DESKTOP_DIR" "$ICON_DIR"
sed "s|__INSTALL_DIR__|$INSTALL_DIR|g" "$SCRIPT_DIR/radegast-veles.desktop" > "$DESKTOP_DIR/radegast-veles.desktop"
if [ -f "$REPO_DIR/Radegast/radegast.png" ]; then
    cp "$REPO_DIR/Radegast/radegast.png" "$ICON_DIR/radegast-veles.png"
fi
# Use app icon if png doesn't exist
if [ ! -f "$ICON_DIR/radegast-veles.png" ] && [ -f "$REPO_DIR/Install/RadegastSetup/RadegastSetup.wixproj" ]; then
    # Try to find any icon
    find "$REPO_DIR" -name "*.png" -path "*/Assets/*" -exec cp {} "$ICON_DIR/radegast-veles.png" \; 2>/dev/null || true
fi

echo ""
echo "=== Done! ==="
echo "Radegast Veles installed to $INSTALL_DIR"
echo "Launch from application menu or run: $INSTALL_DIR/RadegastVeles"
