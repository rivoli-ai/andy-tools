#!/bin/bash
# Run Andy Tools in Apple Container with proper isolation

echo "=== Running Andy Tools in Apple Container ==="
echo "Using Microsoft's official .NET runtime image for isolation"
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

echo "Attempting to run in container..."
echo "This provides full isolation from your system."
echo ""

# Run with Apple Container using Microsoft's .NET runtime image
# This image will be pulled if not already available
container run \
  --rm \
  --name andy-tools-safe-demo \
  --mount type=bind,source="$BIN_PATH",target=/app,readonly \
  --mount type=tmpfs,target=/tmp \
  --workdir /tmp \
  --user 1000:1000 \
  --memory 256M \
  --cpus 1 \
  --env DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
  --env LC_ALL=en_US.UTF-8 \
  --env LANG=en_US.UTF-8 \
  --env HOME=/tmp \
  --env TMPDIR=/tmp \
  --env DOTNET_CLI_HOME=/tmp \
  --env DOTNET_DISABLE_GUI_ERRORS=true \
  --env DOTNET_RUNNING_IN_CONTAINER=true \
  mcr.microsoft.com/dotnet/runtime:8.0-alpine \
  /app/Andy.Tools.Examples ${1:-safe}

echo ""
echo "Container execution completed."