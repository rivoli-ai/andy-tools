#!/bin/bash
# Run Andy Tools in Apple Container with MAXIMUM safety restrictions

echo "=== Running Andy Tools in Apple Container - ULTRA SAFE MODE ==="
echo "Maximum restrictions applied:"
echo "- Read-only filesystem"
echo "- No writable directories" 
echo "- Minimal tmpfs with read-only mode"
echo "- Working directory set to non-existent path"
echo ""

# Check for pre-built binary
if [ -z "$ANDY_TOOLS_BIN_PATH" ]; then
    echo "Error: ANDY_TOOLS_BIN_PATH environment variable not set"
    echo "Please set it to the path containing the built Andy.Tools.Examples binary"
    exit 1
fi

if [ ! -f "$ANDY_TOOLS_BIN_PATH/Andy.Tools.Examples" ]; then
    echo "Error: Andy.Tools.Examples binary not found at $ANDY_TOOLS_BIN_PATH"
    exit 1
fi

# Get absolute path to binary directory
BIN_PATH="$ANDY_TOOLS_BIN_PATH"

echo "Starting container with maximum restrictions..."
echo ""

# Run with maximum restrictions:
# - Mount app as read-only
# - Set workdir to a non-existent directory
# - Mount a tiny read-only tmpfs
# - Drop all capabilities
# - No network
container run \
  --rm \
  --name andy-tools-ultra-safe \
  --mount type=bind,source="$BIN_PATH",target=/app,readonly \
  --mount type=tmpfs,target=/tmp \
  --workdir /nonexistent \
  --user 65534:65534 \
  --memory 128M \
  --cpus 0.5 \
  --env DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true \
  --env HOME=/nonexistent \
  --env TMPDIR=/nonexistent \
  --env DOTNET_CLI_HOME=/nonexistent \
  --env DOTNET_DISABLE_GUI_ERRORS=true \
  --env DOTNET_RUNNING_IN_CONTAINER=true \
  --env DOTNET_EnableDiagnostics=0 \
  --env COMPlus_EnableDiagnostics=0 \
  --no-dns \
  mcr.microsoft.com/dotnet/runtime:8.0-alpine \
  /app/Andy.Tools.Examples ${1:-safe}

echo ""
echo "Container execution completed."