#!/bin/bash
set -e

echo "Building Linux client..."

cd "$(dirname "$0")/.."

dotnet publish src/Game.Client.DesktopGL/Game.Client.DesktopGL.csproj \
    -c Release \
    -r linux-x64 \
    -o dist/linux \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:PublishTrimmed=true \
    -p:TrimMode=partial

chmod +x dist/linux/MMOGame

echo "Linux build complete: dist/linux/"
ls -lh dist/linux/
