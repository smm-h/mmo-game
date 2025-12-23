#!/bin/bash
set -e

cd "$(dirname "$0")"

echo "=========================================="
echo "Building all platforms..."
echo "=========================================="

./build-server.sh
./build-client-linux.sh
./build-client-windows.sh

# macOS builds require macOS host for full compatibility
if [[ "$OSTYPE" == "darwin"* ]]; then
    ./build-client-macos.sh
else
    echo "Skipping macOS build (requires macOS host)"
fi

echo "=========================================="
echo "All builds complete!"
echo "=========================================="
ls -la ../dist/
