#!/bin/bash
set -e

cd "$(dirname "$0")/.."

export ASPNETCORE_ENVIRONMENT=Development
export ASPNETCORE_URLS="http://localhost:5000"

dotnet watch run --project src/Game.Server/Game.Server.csproj
