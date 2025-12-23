#!/bin/bash
set -e

echo "Building macOS client..."

cd "$(dirname "$0")/.."

# Build for both Intel and Apple Silicon
dotnet publish src/Game.Client.DesktopGL/Game.Client.DesktopGL.csproj \
    -c Release \
    -r osx-x64 \
    -o dist/macos-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:PublishTrimmed=true \
    -p:TrimMode=partial

dotnet publish src/Game.Client.DesktopGL/Game.Client.DesktopGL.csproj \
    -c Release \
    -r osx-arm64 \
    -o dist/macos-arm64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:PublishTrimmed=true \
    -p:TrimMode=partial

chmod +x dist/macos-x64/MMOGame
chmod +x dist/macos-arm64/MMOGame

echo "macOS builds complete:"
echo "  Intel: dist/macos-x64/"
echo "  Apple Silicon: dist/macos-arm64/"
