#!/bin/bash
# Run Andy Tools Examples in standard Docker

echo "Building Docker image..."
docker build -t andy-tools:latest ..

echo "Running examples in Docker..."
docker run \
  --rm \
  --name andy-tools-examples \
  --memory=1g \
  --cpus=2 \
  --mount type=tmpfs,destination=/tmp/andy-tools-examples,tmpfs-mode=1777,tmpfs-size=100m \
  --env DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
  --env LC_ALL=en_US.UTF-8 \
  --env LANG=en_US.UTF-8 \
  --env ANDY_TOOLS_SAFE_MODE=true \
  --security-opt no-new-privileges \
  --user andytools \
  andy-tools:latest $@