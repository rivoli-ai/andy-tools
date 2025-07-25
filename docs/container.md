# Container Deployment Guide

This guide covers deploying Andy Tools in container environments using Docker and Apple Container.

## Docker Deployment

### Basic Dockerfile

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files
COPY ["src/Andy.Tools/Andy.Tools.csproj", "src/Andy.Tools/"]
COPY ["examples/Andy.Tools.Examples/Andy.Tools.Examples.csproj", "examples/Andy.Tools.Examples/"]
RUN dotnet restore "examples/Andy.Tools.Examples/Andy.Tools.Examples.csproj"

# Copy source code
COPY . .
WORKDIR "/src/examples/Andy.Tools.Examples"
RUN dotnet build -c Release -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish -c Release -o /app/publish

# Runtime image
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS final
WORKDIR /app

# Create non-root user
RUN groupadd -r andytools && useradd -r -g andytools andytools

# Copy published files
COPY --from=publish /app/publish .

# Set permissions
RUN chown -R andytools:andytools /app

# Switch to non-root user
USER andytools

ENTRYPOINT ["dotnet", "Andy.Tools.Examples.dll"]
```

### Building and Running

```bash
# Build the image
docker build -t andy-tools:latest .

# Run with basic settings
docker run --rm andy-tools:latest

# Run with specific example
docker run --rm andy-tools:latest basic

# Run with volume mount for file operations
docker run --rm \
  -v $(pwd)/data:/app/data \
  andy-tools:latest file

# Run with environment variables
docker run --rm \
  -e ANDY_TOOLS_LOG_LEVEL=Debug \
  -e ANDY_TOOLS_SAFE_MODE=true \
  andy-tools:latest
```

### Docker Compose

```yaml
version: '3.8'

services:
  andy-tools:
    build: .
    image: andy-tools:latest
    container_name: andy-tools-app
    environment:
      - ANDY_TOOLS_LOG_LEVEL=Information
      - ANDY_TOOLS_SAFE_MODE=true
      - DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
    volumes:
      - ./data:/app/data:rw
      - ./config:/app/config:ro
    networks:
      - andy-network
    deploy:
      resources:
        limits:
          cpus: '2'
          memory: 1G
        reservations:
          cpus: '0.5'
          memory: 256M
    security_opt:
      - no-new-privileges:true
    read_only: true
    tmpfs:
      - /tmp
      - /app/temp

  redis:
    image: redis:7-alpine
    container_name: andy-tools-cache
    networks:
      - andy-network
    volumes:
      - redis-data:/data

networks:
  andy-network:
    driver: bridge

volumes:
  redis-data:
```

## Production Docker Configuration

### Multi-stage Dockerfile for Production

```dockerfile
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

# Copy only project files first (for better caching)
COPY src/**/*.csproj ./src/
COPY examples/**/*.csproj ./examples/
COPY tests/**/*.csproj ./tests/
RUN find . -name "*.csproj" -exec dirname {} \; | \
    xargs -I {} sh -c 'mkdir -p /src/{} && mv /src/{}/*.csproj /src/{}/'

# Restore dependencies
WORKDIR /src
COPY Andy.Tools.sln .
RUN dotnet restore

# Copy source code
COPY . .

# Build and test
RUN dotnet build -c Release --no-restore
RUN dotnet test -c Release --no-build --logger:trx

# Publish
WORKDIR /src/examples/Andy.Tools.Examples
RUN dotnet publish -c Release -o /app/publish \
    --no-restore \
    --runtime linux-musl-x64 \
    --self-contained true \
    /p:PublishSingleFile=true \
    /p:PublishTrimmed=true

# Runtime stage
FROM alpine:3.18 AS final
WORKDIR /app

# Install runtime dependencies
RUN apk add --no-cache \
    ca-certificates \
    icu-libs \
    libgcc \
    libssl1.1 \
    libstdc++ \
    zlib

# Create non-root user
RUN addgroup -g 1000 andytools && \
    adduser -u 1000 -G andytools -s /bin/sh -D andytools

# Copy application
COPY --from=build --chown=andytools:andytools /app/publish /app

# Create necessary directories
RUN mkdir -p /app/data /app/temp /app/logs && \
    chown -R andytools:andytools /app

# Security hardening
RUN chmod -R 755 /app && \
    chmod 700 /app/data /app/temp /app/logs

USER andytools

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD ["/app/Andy.Tools.Examples", "health"]

ENTRYPOINT ["/app/Andy.Tools.Examples"]
CMD ["all"]
```

### Container Security

```dockerfile
# Security-focused Dockerfile snippet
FROM mcr.microsoft.com/dotnet/runtime:8.0-alpine AS final

# Remove unnecessary packages
RUN apk update && \
    apk upgrade && \
    apk add --no-cache ca-certificates && \
    rm -rf /var/cache/apk/*

# Create app directory with specific permissions
RUN mkdir -p /app && chmod 755 /app

# Create non-root user with specific UID/GID
RUN addgroup -g 1001 -S andytools && \
    adduser -u 1001 -S andytools -G andytools

# Copy application with correct ownership
COPY --from=build --chown=1001:1001 /app/publish /app

# Set security options
USER 1001:1001

# Disable .NET debugging and telemetry
ENV DOTNET_EnableDiagnostics=0 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1

# Read-only root filesystem
RUN chmod -R a-w /app

WORKDIR /app
ENTRYPOINT ["./Andy.Tools.Examples"]
```

## Apple Container Support

### Apple Container Dockerfile

```dockerfile
# Dockerfile.apple - Optimized for Apple Container
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /app

# Copy and restore
COPY src/ ./src/
COPY examples/ ./examples/
COPY Andy.Tools.sln .
RUN dotnet restore

# Build and publish
WORKDIR /app/examples/Andy.Tools.Examples
RUN dotnet publish -c Release -o /app/publish

# Runtime image
FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app

# Apple Container specific optimizations
LABEL com.apple.container.version="1.0"
LABEL com.apple.container.arch="arm64,amd64"

# Create user
RUN groupadd -r andytools && useradd -r -g andytools andytools

# Copy application
COPY --from=build-env /app/publish .

# Set up directories
RUN mkdir -p /tmp/andy-tools-examples && \
    chown -R andytools:andytools /app /tmp/andy-tools-examples

USER andytools

# Apple Container entry point
ENTRYPOINT ["dotnet", "Andy.Tools.Examples.dll"]
CMD ["all"]
```

### Apple Container Spec File

```yaml
# andy-tools.acspec
version: "1.0"
name: "andy-tools-examples"
image: "andy-tools:latest"

runtime:
  user: "andytools"
  workdir: "/app"
  env:
    - DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
    - LC_ALL=en_US.UTF-8
    - LANG=en_US.UTF-8
    - ANDY_TOOLS_SAFE_MODE=true

resources:
  memory:
    limit: "1Gi"
    reservation: "256Mi"
  cpu:
    limit: 2
    reservation: 0.5

security:
  privileged: false
  readonly_rootfs: false
  no_new_privileges: true
  capabilities:
    drop:
      - ALL

mounts:
  - type: tmpfs
    destination: /tmp/andy-tools-examples
    options:
      size: "100Mi"

network:
  mode: bridge
  allow_outbound: true

entrypoint: ["dotnet", "Andy.Tools.Examples.dll"]
args: ["all"]
```

### Running with Apple Container

```bash
# Build for Apple Container
container build -t andy-tools:latest -f Dockerfile.apple .

# Run with spec file
container run --spec andy-tools.acspec

# Run with inline options
container run \
  --name andy-tools \
  --image andy-tools:latest \
  --runtime-user andytools \
  --runtime-workdir /app \
  --resources-memory-limit 1Gi \
  --security-no-new-privileges \
  -- dotnet Andy.Tools.Examples.dll basic
```

## Container Configuration

### Environment Variables

```yaml
# docker-compose.yml
environment:
  # Logging
  - ANDY_TOOLS_LOG_LEVEL=Information  # Debug, Information, Warning, Error
  - ANDY_TOOLS_LOG_FORMAT=json        # json or text
  
  # Security
  - ANDY_TOOLS_SAFE_MODE=true         # Enable all security restrictions
  - ANDY_TOOLS_ALLOWED_PATHS=/app/data,/app/temp
  - ANDY_TOOLS_ALLOWED_DOMAINS=api.example.com,*.trusted.com
  
  # Performance
  - ANDY_TOOLS_MAX_CONCURRENT=10      # Max concurrent tool executions
  - ANDY_TOOLS_CACHE_ENABLED=true     # Enable result caching
  - ANDY_TOOLS_CACHE_TTL=600          # Cache TTL in seconds
  
  # Resource Limits
  - ANDY_TOOLS_MAX_MEMORY_MB=512      # Per-tool memory limit
  - ANDY_TOOLS_MAX_EXECUTION_TIME=300 # Max execution time in seconds
  - ANDY_TOOLS_MAX_OUTPUT_SIZE=10485760 # 10MB output limit
```

### Volume Mounts

```yaml
volumes:
  # Data directory for file operations
  - ./data:/app/data:rw
  
  # Configuration files (read-only)
  - ./config/appsettings.json:/app/appsettings.json:ro
  - ./config/tools.json:/app/tools.json:ro
  
  # Logs directory
  - ./logs:/app/logs:rw
  
  # Temporary files (use tmpfs for better performance)
  - type: tmpfs
    target: /app/temp
    tmpfs:
      size: 100M
```

### Resource Limits

```yaml
# docker-compose.yml
deploy:
  resources:
    limits:
      cpus: '2.0'
      memory: 1G
      pids: 100
    reservations:
      cpus: '0.5'
      memory: 256M
```

## Kubernetes Deployment

### Deployment Manifest

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: andy-tools
  namespace: tools
spec:
  replicas: 3
  selector:
    matchLabels:
      app: andy-tools
  template:
    metadata:
      labels:
        app: andy-tools
    spec:
      securityContext:
        runAsNonRoot: true
        runAsUser: 1001
        runAsGroup: 1001
        fsGroup: 1001
      containers:
      - name: andy-tools
        image: andy-tools:latest
        imagePullPolicy: Always
        ports:
        - containerPort: 8080
          protocol: TCP
        env:
        - name: ANDY_TOOLS_LOG_LEVEL
          valueFrom:
            configMapKeyRef:
              name: andy-tools-config
              key: log.level
        - name: ANDY_TOOLS_SAFE_MODE
          value: "true"
        resources:
          limits:
            memory: "1Gi"
            cpu: "1000m"
            ephemeral-storage: "1Gi"
          requests:
            memory: "256Mi"
            cpu: "100m"
            ephemeral-storage: "100Mi"
        livenessProbe:
          exec:
            command:
            - /app/Andy.Tools.Examples
            - health
          initialDelaySeconds: 10
          periodSeconds: 30
        readinessProbe:
          exec:
            command:
            - /app/Andy.Tools.Examples
            - ready
          initialDelaySeconds: 5
          periodSeconds: 10
        volumeMounts:
        - name: data
          mountPath: /app/data
        - name: temp
          mountPath: /app/temp
        - name: config
          mountPath: /app/config
          readOnly: true
        securityContext:
          allowPrivilegeEscalation: false
          readOnlyRootFilesystem: true
          capabilities:
            drop:
            - ALL
      volumes:
      - name: data
        persistentVolumeClaim:
          claimName: andy-tools-data
      - name: temp
        emptyDir:
          medium: Memory
          sizeLimit: 100Mi
      - name: config
        configMap:
          name: andy-tools-config
```

### ConfigMap

```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: andy-tools-config
  namespace: tools
data:
  log.level: "Information"
  appsettings.json: |
    {
      "Logging": {
        "LogLevel": {
          "Default": "Information",
          "Microsoft": "Warning",
          "Andy.Tools": "Debug"
        }
      },
      "AndyTools": {
        "SafeMode": true,
        "MaxConcurrentExecutions": 10,
        "DefaultTimeout": "00:05:00"
      }
    }
```

## Monitoring and Observability

### Health Checks

```csharp
// Implement health check endpoint
public class HealthCheckTool : ToolBase
{
    public override ToolMetadata Metadata { get; } = new()
    {
        Id = "health_check",
        Name = "Health Check",
        Description = "Performs system health check"
    };
    
    protected override async Task<ToolResult> ExecuteInternalAsync(
        Dictionary<string, object?> parameters,
        ToolExecutionContext context)
    {
        var health = new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            checks = new
            {
                file_system = CheckFileSystem(),
                memory = CheckMemory(),
                network = await CheckNetwork()
            }
        };
        
        return ToolResult.Success(health);
    }
}
```

### Prometheus Metrics

```yaml
# docker-compose.yml addition
services:
  prometheus:
    image: prom/prometheus:latest
    ports:
      - "9090:9090"
    volumes:
      - ./prometheus.yml:/etc/prometheus/prometheus.yml
      - prometheus-data:/prometheus
    command:
      - '--config.file=/etc/prometheus/prometheus.yml'
      - '--storage.tsdb.path=/prometheus'

volumes:
  prometheus-data:
```

### Logging Configuration

```json
{
  "Serilog": {
    "Using": ["Serilog.Sinks.Console", "Serilog.Sinks.File"],
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning"
      }
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "formatter": "Serilog.Formatting.Compact.CompactJsonFormatter, Serilog.Formatting.Compact"
        }
      },
      {
        "Name": "File",
        "Args": {
          "path": "/app/logs/andy-tools-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 7
        }
      }
    ]
  }
}
```

## Best Practices

### 1. Security

- Always run as non-root user
- Use read-only root filesystem where possible
- Drop all capabilities
- Use security scanning tools
- Keep base images updated

### 2. Performance

- Use Alpine-based images for smaller size
- Implement proper health checks
- Use tmpfs for temporary files
- Enable response compression
- Optimize image layers

### 3. Reliability

- Set appropriate resource limits
- Implement graceful shutdown
- Use init process for signal handling
- Configure restart policies
- Monitor container health

### 4. Debugging

- Include debugging tools in development images
- Use proper logging configuration
- Expose metrics endpoints
- Enable core dumps in development
- Use remote debugging when needed

## Troubleshooting

### Container Won't Start

```bash
# Check logs
docker logs andy-tools

# Inspect container
docker inspect andy-tools

# Run interactively for debugging
docker run -it --entrypoint /bin/sh andy-tools:latest
```

### Permission Issues

```bash
# Fix file permissions
docker exec andy-tools chown -R andytools:andytools /app/data

# Check user
docker exec andy-tools whoami
```

### Memory Issues

```bash
# Monitor memory usage
docker stats andy-tools

# Increase memory limit
docker run -m 2g andy-tools:latest
```

## Summary

Container deployment of Andy Tools provides:

1. **Isolation**: Secure execution environment
2. **Portability**: Consistent across platforms
3. **Scalability**: Easy horizontal scaling
4. **Security**: Defense in depth
5. **Observability**: Built-in monitoring

Follow the examples and best practices in this guide for successful container deployments.