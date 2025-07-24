#!/bin/bash
# Run Andy Tools in Apple Container with isolated filesystem

echo "=== Running Andy Tools in Apple Container - ISOLATED MODE ==="
echo "Filesystem access is limited to:"
echo "- Read-only application mount at /app"
echo "- Writable tmpfs at /workspace (in-memory only)"
echo "- No access to host filesystem"
echo ""

# Check for pre-built binary
if [ -z "$ANDY_TOOLS_BIN_PATH" ]; then
    echo "Error: ANDY_TOOLS_BIN_PATH environment variable not set"
    echo "Please set it to the path containing the built Andy.Tools.Examples binary"
    echo "Example: export ANDY_TOOLS_BIN_PATH=/path/to/andy-tools/examples/bin/Release"
    exit 1
fi

if [ ! -f "$ANDY_TOOLS_BIN_PATH/Andy.Tools.Examples" ]; then
    echo "Error: Andy.Tools.Examples binary not found at $ANDY_TOOLS_BIN_PATH"
    echo "Please build the examples project first and set ANDY_TOOLS_BIN_PATH"
    exit 1
fi

# Get absolute path to binary directory
BIN_PATH="$ANDY_TOOLS_BIN_PATH"

echo "Starting container with isolated filesystem..."
echo "All file operations will occur in temporary memory (tmpfs)"
echo ""

# Run with balanced restrictions:
# - Application mounted read-only
# - Working directory is a writable tmpfs (memory-only)
# - Examples can create/modify files, but only in tmpfs
# - Everything is destroyed when container stops
container run \
  --rm \
  --name andy-tools-isolated \
  --mount type=bind,source="$BIN_PATH",target=/app,readonly \
  --mount type=tmpfs,target=/workspace \
  --workdir /workspace \
  --user 1000:1000 \
  --memory 512M \
  --cpus 1 \
  --env DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
  --env LC_ALL=en_US.UTF-8 \
  --env LANG=en_US.UTF-8 \
  --env HOME=/workspace \
  --env TMPDIR=/workspace/tmp \
  --env ANDY_TOOLS_WORKSPACE=/workspace \
  mcr.microsoft.com/dotnet/runtime:8.0-alpine \
  sh -c "mkdir -p /workspace/tmp && /app/Andy.Tools.Examples ${1:-all}"

echo ""
echo "✅ Container stopped. All temporary files have been destroyed."
echo "No changes were made to your actual filesystem."