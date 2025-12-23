#!/bin/bash
set -e

echo "Building server..."

cd "$(dirname "$0")/.."

dotnet publish src/Game.Server/Game.Server.csproj \
    -c Release \
    -r linux-x64 \
    -o dist/server \
    --self-contained true \
    -p:PublishSingleFile=true

chmod +x dist/server/MMOGame.Server

echo "Server build complete: dist/server/"
ls -lh dist/server/
