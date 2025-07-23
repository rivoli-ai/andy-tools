# Apple Container Guide for Andy Tools

This guide provides detailed instructions for running Andy Tools Examples using Apple Container runtime.

## Prerequisites

1. **Apple Container Runtime**: Install from [Apple Container GitHub](https://github.com/apple/container)
   ```bash
   # Install using Homebrew (if available)
   brew install apple/container/container
   
   # Or build from source
   git clone https://github.com/apple/container.git
   cd container
   make && sudo make install
   ```

2. **Docker**: Required for building the image
   ```bash
   # Verify Docker is installed
   docker --version
   ```

## Building the Container Image

First, build the Docker image that will be used by Apple Container:

```bash
cd /path/to/andy-tools
docker build -t andy-tools:latest .
```

## Running with Apple Container

### Method 1: Using the Spec File

```bash
cd container
container run --spec andy-tools.acspec
```

### Method 2: Using Command Line

```bash
container run \
  --name andy-tools-examples \
  --image andy-tools:latest \
  --runtime-user andytools \
  --runtime-workdir /app \
  --runtime-env DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
  --runtime-env LC_ALL=en_US.UTF-8 \
  --runtime-env LANG=en_US.UTF-8 \
  --runtime-env ANDY_TOOLS_SAFE_MODE=true \
  --resources-memory-limit 1Gi \
  --resources-cpu-limit 2 \
  --security-privileged=false \
  --security-readonly-rootfs=false \
  --security-no-new-privileges \
  --mount type=tmpfs,dst=/tmp/andy-tools-examples,size=100Mi \
  --network-mode bridge \
  --network-allow-outbound \
  -- dotnet Andy.Tools.Examples.dll all
```

### Method 3: Creating and Starting Container

```bash
# Create container
container create \
  --name andy-tools-examples \
  --spec andy-tools.acspec

# Start container
container start andy-tools-examples

# View logs
container logs -f andy-tools-examples
```

## Running Specific Examples

To run specific examples, override the arguments:

```bash
# Run only basic examples
container run --spec andy-tools.acspec -- dotnet Andy.Tools.Examples.dll basic

# Run only file operations
container run --spec andy-tools.acspec -- dotnet Andy.Tools.Examples.dll file

# Run only security examples
container run --spec andy-tools.acspec -- dotnet Andy.Tools.Examples.dll security
```

## Container Management

### List Containers
```bash
container list
container list --all  # Include stopped containers
```

### Inspect Container
```bash
container inspect andy-tools-examples
```

### Stop Container
```bash
container stop andy-tools-examples
```

### Remove Container
```bash
container rm andy-tools-examples
```

### View Resource Usage
```bash
container stats andy-tools-examples
```

## Security Considerations

The container is configured with strict security settings:

- **Non-root execution**: Runs as `andytools` user
- **No privilege escalation**: Cannot gain additional privileges
- **Dropped capabilities**: All Linux capabilities are dropped
- **Resource limits**: Memory and CPU are limited
- **Isolated filesystem**: Uses tmpfs for temporary files

## Networking

By default, the container allows outbound connections for web examples but blocks inbound connections. To run with no network:

```bash
container run --spec andy-tools.acspec --network-mode none
```

## Troubleshooting

### Container fails to start

1. Check Apple Container is properly installed:
   ```bash
   container version
   ```

2. Verify the Docker image exists:
   ```bash
   docker images | grep andy-tools
   ```

3. Check container logs:
   ```bash
   container logs andy-tools-examples
   ```

### Permission denied errors

The container runs with limited permissions. Ensure:
- File operations use `/tmp/andy-tools-examples`
- Network operations are allowed (if needed)

### Resource limit exceeded

Adjust limits in the spec file or command line:
```bash
--resources-memory-limit 2Gi
--resources-cpu-limit 4
```

## Advanced Usage

### Custom Mounts

To mount a local directory for persistent storage:
```bash
container run --spec andy-tools.acspec \
  --mount type=bind,src=/path/to/local/dir,dst=/data,readonly
```

### Environment Variables

Add custom environment variables:
```bash
container run --spec andy-tools.acspec \
  --runtime-env MY_CUSTOM_VAR=value
```

### Interactive Mode

For debugging, run interactively:
```bash
container run --spec andy-tools.acspec \
  --interactive \
  --tty \
  -- /bin/bash
```

## Performance Monitoring

Monitor container performance:
```bash
# Real-time stats
container stats andy-tools-examples

# Export metrics
container export-metrics andy-tools-examples --format json
```

## Cleanup

Remove all Andy Tools containers and images:
```bash
# Stop and remove containers
container stop andy-tools-examples
container rm andy-tools-examples

# Remove Docker image
docker rmi andy-tools:latest
```

## Additional Resources

- [Apple Container Documentation](https://github.com/apple/container/blob/main/docs/tutorial.md)
- [Apple Container How-To Guide](https://github.com/apple/container/blob/main/docs/how-to.md)
- [Andy Tools Documentation](../README.md)