# Test Failures Tracking Document

Generated: 2025-07-20

## Summary
- **Total Failing Tests**: 400
- **Tests Fixed So Far**: 36 (ComputedProperty: 6, TypewriterEffect: 30)
- **Tests Remaining**: 364

## Progress Tracker

### ✅ Completed
- [x] Andy.UI.Tests - ComputedProperty tests (6 tests)
- [x] Andy.UI.Tests - TypewriterEffect tests (30 tests)

### 🚧 In Progress
- [ ] Andy.Tools.Tests - ToolExecutor constructor validation (started, 11 remaining)

### 📋 To Do (Organized by Priority)

#### High Priority - Core Infrastructure (79 tests)
- [ ] **Andy.Tools.Tests.Execution.ToolExecutorTests** (12 tests)
  - Constructor null parameter validation
  - Execution timeout handling
  - Security violation handling
  - Resource monitoring
  
- [ ] **Andy.Tools.Tests.Advanced.ToolChainBuilderTests** (8 tests)
  - Constructor null parameter validation
  - Fluent interface methods
  - Build configuration
  
- [ ] **Andy.Tools.Tests.Advanced.MemoryToolExecutionCacheTests** (6 tests)
  - Constructor null parameter validation
  - Cache eviction logic
  - Statistics tracking

- [ ] **Andy.CLI.Tests.Services.AuthenticationServiceTests** (13 tests)
  - Authentication flow
  - Token refresh logic
  - Provider validation

- [ ] **Andy.CLI.Tests.Services.TaskSummaryServiceTests** (22 tests)
  - Summary generation
  - Token counting
  - Message formatting

- [ ] **Andy.GeminiClient Tests** (18 tests across multiple classes)
  - Service configuration
  - Rate limiting
  - Model serialization

#### High Priority - File System Tools (72 tests)
- [ ] **MoveFileToolTests** (21 tests)
  - File/directory move operations
  - Overwrite handling
  - Cross-volume moves
  - Statistics reporting

- [ ] **DeleteFileToolTests** (17 tests)
  - File/directory deletion
  - Recursive deletion
  - Backup creation
  - Read-only file handling

- [ ] **ListDirectoryToolTests** (19 tests)
  - Directory listing
  - Pattern filtering
  - Hidden file handling
  - Recursive listing

- [ ] **CopyFileToolTests** (15 tests)
  - File/directory copying
  - Exclude patterns
  - Timestamp preservation
  - Statistics reporting

#### Medium Priority - Text Processing Tools (70 tests)
- [ ] **FormatTextToolTests** (42 tests)
  - Various text transformations (case conversion, encoding, etc.)
  - JSON/XML formatting
  - Text extraction operations

- [ ] **ReplaceTextToolTests** (28 tests)
  - Text replacement operations
  - Regex support
  - Backup handling
  - Dry run mode

#### Medium Priority - CLI Components (50+ tests)
- [ ] **Configuration Commands** (30+ tests)
  - Auth command
  - Config command
  - Providers command
  
- [ ] **Built-in Commands** (20+ tests)
  - History command
  - Status command
  - Model management

#### Low Priority - Integration & Misc (100+ tests)
- [ ] Integration tests
- [ ] Non-interactive mode tests
- [ ] Markdown formatting tests
- [ ] Security manager tests

## Common Failure Patterns

### 1. Missing Null Parameter Validation
Most constructor tests expect `ArgumentNullException` when null parameters are passed.

**Fix Pattern**:
```csharp
public MyClass(IService service, ILogger<MyClass> logger)
{
    _service = service ?? throw new ArgumentNullException(nameof(service));
    _logger = logger ?? throw new ArgumentNullException(nameof(logger));
}
```

### 2. Unimplemented Tool Operations
Many file system and text processing tools have incomplete implementations.

**Fix Pattern**:
- Implement the actual file operations
- Add proper error handling
- Return appropriate ToolExecutionResult

### 3. Mock Configuration Issues
Some tests fail because mocks aren't properly configured.

**Fix Pattern**:
- Review test setup
- Ensure all required mock behaviors are defined
- Check for missing service registrations

## Recommended Fix Order

1. **Start with constructor validation** - Quick wins, many tests fixed with simple null checks
2. **Fix core infrastructure** - ToolExecutor, ToolChainBuilder, etc. as many other components depend on these
3. **Implement file system tools** - These are foundational for many operations
4. **Address text processing tools** - Less critical but high test count
5. **Fix CLI components** - User-facing but can work around issues
6. **Clean up integration tests** - Often fail due to upstream issues

## Next Steps

1. Create a branch for each major component group
2. Fix tests incrementally, committing after each class is complete
3. Run full test suite after each major fix to catch regressions
4. Update this document as tests are fixed

## Notes

- Some tests may be failing due to missing implementations rather than bugs
- Consider if some tests need to be updated rather than the code
- Watch for patterns - fixing one issue often fixes multiple tests
- Use `dotnet test --filter` to run specific test classes during development