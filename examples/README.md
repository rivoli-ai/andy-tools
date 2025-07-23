# Andy Tools Examples

> ⚠️ **WARNING**: These examples use ALPHA software that can perform destructive operations. Always run in a safe, isolated environment with proper backups.

## Overview

This project contains comprehensive examples demonstrating how to use the Andy Tools framework. The examples are organized into different categories to help you learn specific aspects of the framework.

## Running the Examples

1. Build the solution:
   ```bash
   dotnet build
   ```

2. Run the examples:
   ```bash
   dotnet run --project examples/Andy.Tools.Examples
   ```

3. Select an example category from the menu

## Example Categories

### 1. Basic Tool Usage
- Simple tool execution
- Using parameters
- Error handling
- Execution contexts

### 2. File Operations
- Reading and writing files
- Copying files with progress tracking
- Directory listing and filtering
- Moving/renaming files
- Safe file deletion

### 3. Text Processing
- JSON/XML formatting
- Search and replace with regex
- Text searching with context
- Format conversion

### 4. Tool Chains
- Sequential tool execution
- Dependency management
- Conditional execution
- Data transformation pipelines

### 5. Custom Tools
- Creating custom tools
- Word count tool example
- CSV processor example
- Password generator example

### 6. Security and Permissions
- Permission restrictions
- Resource limits
- Security violation monitoring
- Sandboxed execution

### 7. Caching Examples
- Basic result caching
- Cache expiration
- Cache invalidation
- Performance improvements

### 8. Web Operations
- HTTP requests
- JSON processing
- Custom headers
- Error handling

### 9. System Information
- OS and hardware info
- Environment variables
- Process information
- System metrics

## Safety Guidelines

1. **Always run in a test environment** - Never run these examples on production systems
2. **Check file paths** - Examples create temporary files; ensure paths are safe
3. **Monitor resource usage** - Some examples demonstrate resource limits
4. **Review permissions** - Understand what permissions each example requires
5. **Backup data** - Always have backups before running file operation examples

## Key Concepts Demonstrated

### Dependency Injection
All examples use Microsoft.Extensions.DependencyInjection for service registration and resolution.

### Execution Contexts
Examples show how to use `ToolExecutionContext` to control:
- Working directories
- Permissions
- Resource limits
- Caching
- Progress reporting

### Error Handling
Proper error handling patterns including:
- Checking `result.IsSuccess`
- Examining error messages and codes
- Handling specific error scenarios

### Tool Composition
Examples demonstrate:
- Using output from one tool as input to another
- Building complex workflows with tool chains
- Conditional execution based on results

## Creating Your Own Examples

To add a new example:

1. Create a new static class in the Examples namespace
2. Add a static `RunAsync` method that accepts `IServiceProvider`
3. Update `Program.cs` to include your example in the menu
4. Follow the existing patterns for consistency

## Troubleshooting

### Missing Tools
If a tool is not found, ensure:
- The framework is configured with `RegisterBuiltInTools = true`
- Custom tools are properly registered with `AddTool<T>()`

### Permission Errors
If you get permission errors:
- Check the execution context permissions
- Ensure file system access is allowed for file operations
- Verify network access for web operations

### Cache Issues
If caching behaves unexpectedly:
- Check if caching is enabled in the context
- Verify cache TTL settings
- Use cache invalidation when needed

## Further Resources

- Main README: [/README.md](../README.md)
- API Documentation: See XML comments in source code
- Tests: [/tests/Andy.Tools.Tests](../tests/Andy.Tools.Tests)

Remember: This is ALPHA software. Always prioritize safety and use in controlled environments only.