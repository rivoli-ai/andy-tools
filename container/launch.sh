#!/bin/bash
# Launch script for Andy Tools Examples in Apple Container

# Build the Docker image first
echo "Building Docker image..."
docker build -t andy-tools:latest ..

# Create container using Apple Container runtime
echo "Creating Apple Container..."
container create \
  --name andy-tools-examples \
  --image andy-tools:latest \
  --memory 1G \
  --cpu 2 \
  --mount type=tmpfs,destination=/tmp/andy-tools-examples,tmpfs-size=100M \
  --env DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
  --env LC_ALL=en_US.UTF-8 \
  --env LANG=en_US.UTF-8 \
  --env ANDY_TOOLS_SAFE_MODE=true \
  --security-opt no-new-privileges \
  --security-opt seccomp=unconfined \
  --user andytools \
  dotnet Andy.Tools.Examples.dll all

# Start the container
echo "Starting container..."
container start andy-tools-examples

# Follow logs
echo "Following container logs..."
container logs -f andy-tools-examples