# FileSystem Tools Implementation TODO

## Summary
The FileSystem tool tests are now properly set up with initialization and correct parameter names, but the actual tool implementations are incomplete. All tools inherit from ToolBase but their ExecuteInternalAsync methods need to be implemented.

## Tools Requiring Implementation

### 1. CopyFileTool (15 tests failing)
- **Location**: `/src/Andy.Tools/Library/FileSystem/CopyFileTool.cs`
- **Missing**: Complete implementation of file/directory copying logic
- **Key Features to Implement**:
  - Single file copying
  - Directory recursive copying
  - Overwrite handling
  - Timestamp preservation
  - Exclude patterns
  - Progress reporting

### 2. DeleteFileTool (17 tests failing)
- **Location**: `/src/Andy.Tools/Library/FileSystem/DeleteFileTool.cs`
- **Missing**: Complete implementation of deletion logic
- **Key Features to Implement**:
  - File deletion
  - Directory deletion (recursive and non-recursive)
  - Backup creation before deletion
  - Force deletion of read-only files
  - Size limit checking
  - Exclude patterns

### 3. MoveFileTool (21 tests failing)
- **Location**: `/src/Andy.Tools/Library/FileSystem/MoveFileTool.cs`
- **Missing**: Complete implementation of move/rename logic
- **Key Features to Implement**:
  - File moving/renaming
  - Directory moving
  - Cross-volume moves
  - Overwrite handling
  - Backup of existing destination files
  - Statistics reporting

### 4. ListDirectoryTool (19 tests failing)
- **Location**: `/src/Andy.Tools/Library/FileSystem/ListDirectoryTool.cs`
- **Missing**: Complete implementation of directory listing logic
- **Key Features to Implement**:
  - Basic directory listing
  - Recursive listing
  - Hidden file handling
  - Pattern filtering
  - Sorting (by name, size, date)
  - Detailed file information
  - Max depth limiting

### 5. WriteFileTool (2 tests failing)
- **Location**: `/src/Andy.Tools/Library/FileSystem/WriteFileTool.cs`
- **Note**: This tool seems mostly implemented but has some edge cases failing
- **Key Features to Check**:
  - Backup creation
  - Different encoding support

## Common Implementation Patterns

All tools should follow these patterns from ToolBase:

```csharp
protected override async Task<ToolResult> ExecuteInternalAsync(
    Dictionary<string, object?> parameters, 
    ToolExecutionContext context)
{
    // 1. Extract parameters using GetParameter<T>
    var param = GetParameter<string>(parameters, "param_name");
    
    // 2. Validate paths using ToolHelpers.GetSafePath
    var safePath = ToolHelpers.GetSafePath(param, context.WorkingDirectory);
    
    // 3. Report progress
    ReportProgress(context, "Operation starting...", 0);
    
    // 4. Perform operation
    try
    {
        // Implementation here
        
        // 5. Return success with metadata
        return ToolResults.Success(
            data: statisticsObject,
            message: "Operation completed",
            metadata: new Dictionary<string, object?>
            {
                ["key"] = value
            }
        );
    }
    catch (UnauthorizedAccessException)
    {
        return ToolResults.AccessDenied(path, "operation");
    }
    catch (Exception ex)
    {
        return ToolResults.Failure($"Error: {ex.Message}", "ERROR_CODE", details: ex);
    }
}
```

## Test Summary
- **Total FileSystem Tests**: 91
- **Currently Passing**: 17 (metadata and parameter validation tests)
- **Currently Failing**: 74 (execution tests requiring implementation)

## Next Steps
1. Implement each tool's ExecuteInternalAsync method
2. Use existing helper methods from ToolHelpers and ToolResults
3. Ensure all error cases are handled properly
4. Add progress reporting for long operations
5. Implement cancellation support using context.CancellationToken

## Note
The test infrastructure is now correct. Once the tool implementations are complete, all tests should pass.