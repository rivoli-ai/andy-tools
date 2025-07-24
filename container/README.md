# Running Andy Tools Examples in Apple Container

This directory contains configuration files for running the Andy Tools examples in an Apple Container environment.

## ⚠️ ALPHA SOFTWARE WARNING

**This software is in ALPHA stage and can perform DESTRUCTIVE operations. Run only in isolated container environments!**

## Prerequisites

- Apple Container runtime installed on your machine
- Rosetta 2 (for Apple Silicon Macs) - install with `softwareupdate --install-rosetta --agree-to-license`
- A configured kernel for Apple Container (see setup instructions)
- At least 1GB of available memory

## Quick Start - Self-Contained Container (Recommended)

### Step 1: Build the Project

```bash
cd ..
dotnet build examples/Andy.Tools.Examples/Andy.Tools.Examples.csproj
cd container
```

### Step 2: Configure Apple Container Kernel

```bash
# For Apple Silicon (arm64)
container system kernel set --recommended --arch arm64

# For Intel Macs (x86_64)
container system kernel set --recommended --arch amd64
```

### Step 3: Build the Container Image

```bash
container build -t andy-tools:self-contained -f Dockerfile.apple ..
```

### Step 4: Run the Self-Contained Container

```bash
./run-apple-container-self-contained.sh
```

This runs all examples in a completely self-contained container with:
- Binary and dependencies included in the image
- No access to your host filesystem
- All operations in temporary memory (tmpfs)
- Everything destroyed when done

## Alternative: Using Local Binary Mount

If you prefer to mount your local binary instead of building an image:

### Step 1: Build and Set Path

```bash
cd ../examples/Andy.Tools.Examples
dotnet publish -c Release
export ANDY_TOOLS_BIN_PATH="$(pwd)/bin/Release/net8.0"
cd ../../container
```

### Step 2: Run with Local Mount

```bash
./run-apple-container-isolated.sh
```

See [CONTAINER_SAFETY_LEVELS.md](CONTAINER_SAFETY_LEVELS.md) for other safety levels.

This script will:
1. Use your local pre-built binaries
2. Create an Apple Container with appropriate security settings
3. Run all examples
4. Stream the logs to your terminal

### Available Safety Levels

1. **Isolated Mode** (Recommended):
   ```bash
   ./run-apple-container-isolated.sh
   ```
   - Full examples with tmpfs filesystem
   - All file operations work properly
   - Everything contained in memory

2. **Safe Mode**:
   ```bash
   ./run-apple-container-safe.sh
   ```
   - Limited filesystem access
   - Some examples may fail

3. **Ultra-Safe Mode**:
   ```bash
   ./run-apple-container-ultra-safe.sh
   ```
   - No filesystem access at all
   - Only computation examples work

## Advanced Usage

For detailed configuration options and troubleshooting, see [APPLE_CONTAINER_GUIDE.md](APPLE_CONTAINER_GUIDE.md).

2. **Create the container:**
   ```bash
   container create \
     --name andy-tools-examples \
     --image andy-tools:latest \
     --memory 1G \
     --cpu 2 \
     --mount type=tmpfs,destination=/tmp/andy-tools-examples,tmpfs-size=100M \
     --env ANDY_TOOLS_SAFE_MODE=true \
     --security-opt no-new-privileges \
     --user andytools \
     dotnet Andy.Tools.Examples.dll all
   ```

3. **Start the container:**
   ```bash
   container start andy-tools-examples
   ```

4. **View logs:**
   ```bash
   container logs -f andy-tools-examples
   ```

## Running Specific Examples

You can run specific examples instead of all by passing an argument:

```bash
# Run only basic examples
container run \
  --rm \
  --image andy-tools:latest \
  --memory 1G \
  dotnet Andy.Tools.Examples.dll basic

# Available options:
# - basic     : Basic Tool Usage
# - file      : File Operations  
# - text      : Text Processing
# - chain     : Tool Chains
# - custom    : Custom Tools
# - security  : Security and Permissions
# - cache     : Caching Examples
# - web       : Web Operations
# - system    : System Information
# - all       : Run all examples (default)
```

## Security Features

The container is configured with:
- Non-root user execution
- Read-only root filesystem (where possible)
- No privilege escalation
- Limited resources (1GB memory, 2 CPU cores)
- Temporary filesystem for work files
- Network isolation (configurable)

## Troubleshooting

### Container fails to start
- Check Apple Container runtime is installed: `container version`
- Ensure container image built successfully: `container images list | grep andy-tools`

### Permission errors
- The container runs as user `andytools` with limited permissions
- File operations are restricted to `/tmp/andy-tools-examples`

### Network errors in web examples
- By default, outbound network is allowed for web examples
- If network is restricted, web examples will fail gracefully

## Cleanup

Remove the container and image:
```bash
container rm andy-tools-examples
container images rm andy-tools:self-contained
```

## Additional Notes

- The container uses a tmpfs mount for temporary files
- All example data is ephemeral and destroyed when container stops
- Logs are preserved until container is removed
- Container health is checked every 30 seconds