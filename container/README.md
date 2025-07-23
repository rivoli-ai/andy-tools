# Running Andy Tools Examples in Apple Container

This directory contains configuration files for running the Andy Tools examples in an Apple Container environment.

## ⚠️ ALPHA SOFTWARE WARNING

**This software is in ALPHA stage and can perform DESTRUCTIVE operations. Run only in isolated container environments!**

## Prerequisites

- Apple Container runtime installed on your machine
- Docker (for building the image)
- At least 1GB of available memory

## Quick Start

### Option 1: Using the Launch Script

```bash
cd container
./launch.sh
```

This script will:
1. Build the Docker image
2. Create an Apple Container with appropriate security settings
3. Run all examples
4. Stream the logs to your terminal

### Option 2: Manual Steps

1. **Build the Docker image:**
   ```bash
   docker build -t andy-tools:latest ..
   ```

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
- Ensure Docker image built successfully: `docker images | grep andy-tools`

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
docker rmi andy-tools:latest
```

## Additional Notes

- The container uses a tmpfs mount for temporary files
- All example data is ephemeral and destroyed when container stops
- Logs are preserved until container is removed
- Container health is checked every 30 seconds