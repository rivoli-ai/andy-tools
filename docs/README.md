# Andy Tools Technical Documentation

Welcome to the Andy Tools technical documentation. This comprehensive guide covers all aspects of the Andy Tools framework, from basic usage to advanced features and custom tool development.

## Table of Contents

1. [Getting Started](getting-started.md) - Installation, setup, and your first tool execution
2. [Architecture Overview](architecture.md) - System design and component interactions
3. [Core Concepts](core-concepts.md) - Understanding tools, executors, and contexts
4. [Built-in Tools Reference](tools-reference.md) - Complete reference for all built-in tools
5. [Creating Custom Tools](custom-tools.md) - Step-by-step guide to building your own tools
6. [Security and Permissions](security.md) - Permission system and security best practices
7. [Advanced Features](advanced-features.md) - Tool chains, caching, and metrics
8. [API Reference](api-reference.md) - Complete API documentation
9. [Examples and Tutorials](examples.md) - Practical examples and use cases
10. [Troubleshooting](troubleshooting.md) - Common issues and solutions
11. [Container Deployment](container.md) - Running Andy Tools in containers

## Quick Links

- [GitHub Repository](https://github.com/rivoli-ai/andy-tools)
- [Issue Tracker](https://github.com/rivoli-ai/andy-tools/issues)
- [Release Notes](https://github.com/rivoli-ai/andy-tools/releases)

## Overview

Andy Tools is a comprehensive .NET framework for building and executing tools with a focus on:

- **Modularity**: Clean separation of concerns with well-defined interfaces
- **Extensibility**: Easy to extend with custom tools and features
- **Security**: Fine-grained permission control and resource management
- **Performance**: Built-in caching, metrics, and resource monitoring
- **Usability**: Simple API with powerful capabilities

## Key Features

### Tool Framework
- Unified tool interface with metadata and parameter validation
- Automatic parameter type conversion and validation
- Progress reporting and cancellation support
- Output limiting for large results

### Built-in Tools
- File system operations (read, write, copy, move, delete)
- Text processing (format, search, replace)
- Web operations (HTTP requests, JSON processing)
- System utilities (info, processes, date/time, encoding)
- Git integration (diff tool)

### Advanced Features
- Tool chains for complex workflows
- Result caching with configurable TTL
- Performance metrics collection
- Resource monitoring and limits
- Security permission system

### Developer Experience
- Dependency injection integration
- Comprehensive logging and observability
- Detailed error messages and validation
- Extensive unit test coverage
- Clear documentation and examples

## Getting Help

If you need help with Andy Tools:

1. Check the [Troubleshooting Guide](troubleshooting.md)
2. Search existing [GitHub Issues](https://github.com/rivoli-ai/andy-tools/issues)
3. Create a new issue with:
   - Clear description of the problem
   - Steps to reproduce
   - Expected vs actual behavior
   - Environment details (OS, .NET version, etc.)

## Contributing

We welcome contributions! Please see our [Contributing Guide](../CONTRIBUTING.md) for details on:

- Code style and standards
- Testing requirements
- Pull request process
- Development setup

## License

Andy Tools is licensed under the Apache License 2.0. See the [LICENSE](../LICENSE) file for details.