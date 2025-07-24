# Apple Container Safety Levels for Andy Tools

## Overview

Different examples require different levels of filesystem access. All options use Apple Container for true isolation.

## 0. 🚀 SELF-CONTAINED MODE (New - Recommended)
**Script:** `run-apple-container-self-contained.sh`

```bash
./run-apple-container-self-contained.sh [example-set]
```

- ✅ Binary and dependencies included in the container image
- ✅ No local filesystem mounts needed
- ✅ All file operations happen in tmpfs (RAM only)
- ✅ Everything destroyed when container stops
- ✅ Writable tmpfs at `/workspace` and `/tmp`
- Working directory: `/app` (contains the binary)

**Best for:** Production-like isolation with no dependency on local files

## 1. 🟢 ISOLATED MODE
**Script:** `run-apple-container-isolated.sh`

```bash
export ANDY_TOOLS_BIN_PATH=/path/to/binaries
./run-apple-container-isolated.sh [example-set]
```

- ✅ Full examples can run (including file operations)
- ✅ All file operations happen in tmpfs (RAM only)
- ✅ No access to your actual filesystem
- ✅ Everything destroyed when container stops
- ✅ Writable tmpfs at `/workspace` for examples
- ⚠️ Requires local binary path to be set
- Working directory: `/workspace` (writable tmpfs)

**Best for:** Development and testing when you have local binaries

## 2. 🟡 SAFE MODE
**Script:** `run-apple-container-safe.sh`

```bash
export ANDY_TOOLS_BIN_PATH=/path/to/binaries
./run-apple-container-safe.sh [example-set]
```

- ⚠️ Limited tmpfs access (1MB)
- ⚠️ Working directory in `/tmp`
- ✅ No access to host filesystem
- ✅ Non-root user
- Working directory: `/tmp` (limited tmpfs)

**Best for:** Running specific non-destructive examples

## 3. 🔴 ULTRA-SAFE MODE
**Script:** `run-apple-container-ultra-safe.sh`

```bash
export ANDY_TOOLS_BIN_PATH=/path/to/binaries
./run-apple-container-ultra-safe.sh [example-set]
```

- ❌ NO writable filesystem at all
- ❌ File operation examples will fail
- ✅ Maximum security
- ✅ Only in-memory operations work
- Working directory: `/nonexistent` (doesn't exist)

**Best for:** Testing only computation/encoding examples

## Comparison Table

| Feature | Self-Contained | Isolated | Safe | Ultra-Safe |
|---------|----------------|----------|------|------------|
| File Operations | ✅ Yes (tmpfs) | ✅ Yes (tmpfs) | ⚠️ Limited | ❌ No |
| Network Access | ✅ Allowed | ✅ Allowed | ✅ Allowed | ❌ No DNS |
| Host FS Access | ❌ No | ❌ No | ❌ No | ❌ No |
| Memory Limit | 512MB | 512MB | 256MB | 128MB |
| Tmpfs Size | Multiple | Multiple | 1MB | Minimal |
| Working Dir | `/app` | `/workspace` | `/tmp` | `/nonexistent` |
| User | 1000:1000 | 1000:1000 | 1000:1000 | nobody |
| Binary Location | In image | Mounted from host | Mounted from host | Mounted from host |

## Which Mode to Use?

### Want to see all examples work properly?
Use **ISOLATED MODE** - it provides proper filesystem access in a completely safe tmpfs environment.

### Only need basic operations?
Use **SAFE MODE** - good for date/time, encoding, hashing examples.

### Maximum paranoia?
Use **ULTRA-SAFE MODE** - but expect file operations to fail.

## What Happens to Files?

In all modes:
- **No files are written to your actual filesystem**
- All operations happen in container-isolated areas
- When container stops, everything is destroyed
- Your Mac's filesystem remains untouched

## Example Commands

```bash
# Run all examples in isolated environment
./run-apple-container-isolated.sh all

# Run only basic examples
./run-apple-container-isolated.sh basic

# Run only text processing
./run-apple-container-isolated.sh text
```

## Container Requirements

### Pre-Built Binary
All scripts require a pre-built binary. Build it outside this directory:

```bash
cd ../examples/Andy.Tools.Examples
dotnet publish -c Release -r osx-arm64 --self-contained
export ANDY_TOOLS_BIN_PATH="$(pwd)/bin/Release/net8.0/osx-arm64/publish"
cd ../../container
```

### Kernel Setup
Apple Container requires a kernel:

```bash
curl -LO https://github.com/kata-containers/kata-containers/releases/download/3.17.0/kata-static-3.17.0-arm64.tar.xz
tar xf kata-static-3.17.0-arm64.tar.xz
sudo container system kernel set kata-static/vmlinux.container
```

## Verification

To verify container isolation:

```bash
# Check container is truly isolated
container exec andy-tools-isolated ls /Users
# Should fail - no access to host filesystem

# Check what's running
container list

# Check resource usage
container stats andy-tools-isolated
```