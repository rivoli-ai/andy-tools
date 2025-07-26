#!/bin/bash
# Run Andy Tools in Apple Container with self-contained image

echo "=== Running Andy Tools in Apple Container - SELF-CONTAINED ==="
echo "This uses a pre-built container image with the binary inside."
echo "No local filesystem access is needed or granted."
echo ""

# Check if the image exists
if ! container images list | grep -q "andy-tools.*self-contained"; then
    echo "Error: andy-tools:self-contained image not found"
    echo "Please build it first with:"
    echo "  cd /path/to/andy-tools && container build -t andy-tools:self-contained -f container/Dockerfile.apple ."
    exit 1
fi

echo "Starting self-contained container..."
echo "All operations will occur within the container's filesystem"
echo ""

# Run the self-contained container
# - No bind mounts from host filesystem
# - tmpfs for temporary workspace
# - Restricted resources and security settings
container run \
  --rm \
  --name andy-tools-self-contained \
  --mount type=tmpfs,target=/tmp \
  --mount type=tmpfs,target=/workspace \
  --workdir /app \
  --user 1000:1000 \
  --memory 512M \
  --cpus 1 \
  --env DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
  --env LC_ALL=en_US.UTF-8 \
  --env LANG=en_US.UTF-8 \
  --env HOME=/workspace \
  --env TMPDIR=/tmp \
  --env ANDY_TOOLS_WORKSPACE=/workspace \
  andy-tools:self-contained \
  ${1:-all}

echo ""
echo "✅ Container stopped. No changes were made to your host filesystem."