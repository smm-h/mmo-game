#!/bin/bash
set -e

cd "$(dirname "$0")/.."

dotnet run --project src/Game.Client.DesktopGL/Game.Client.DesktopGL.csproj
