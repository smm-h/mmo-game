#!/bin/bash
set -e

echo "Building Windows client..."

cd "$(dirname "$0")/.."

dotnet publish src/Game.Client.DesktopGL/Game.Client.DesktopGL.csproj \
    -c Release \
    -r win-x64 \
    -o dist/windows \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:PublishTrimmed=true \
    -p:TrimMode=partial

echo "Windows build complete: dist/windows/"
ls -lh dist/windows/
