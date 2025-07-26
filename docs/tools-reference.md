# Built-in Tools Reference

This reference documents all built-in tools available in Andy Tools, organized by category.

## File System Tools

### read_file

Reads the contents of a file.

**Parameters:**
- `file_path` (string, required): Path to the file to read
- `encoding` (string, optional): Text encoding (default: "utf-8")

**Returns:**
- `content`: File contents as string
- `file_path`: Absolute path to the file
- `size`: File size in bytes
- `encoding`: Encoding used

**Example:**
```csharp
var parameters = new Dictionary<string, object?>
{
    ["file_path"] = "data.txt"
};
var result = await executor.ExecuteAsync("read_file", parameters);
```

### write_file

Writes content to a file, creating it if it doesn't exist.

**Parameters:**
- `file_path` (string, required): Path to the file
- `content` (string, required): Content to write
- `encoding` (string, optional): Text encoding (default: "utf-8")
- `create_directories` (boolean, optional): Create parent directories (default: true)

**Returns:**
- `file_path`: Absolute path to the written file
- `size`: Size of written content in bytes
- `created`: Whether a new file was created

**Example:**
```csharp
var parameters = new Dictionary<string, object?>
{
    ["file_path"] = "output.txt",
    ["content"] = "Hello, World!"
};
var result = await executor.ExecuteAsync("write_file", parameters);
```

### delete_file

Deletes a file from the file system.

**Parameters:**
- `target_path` (string, required): Path to the file to delete

**Returns:**
- `deleted_path`: Path of the deleted file
- `existed`: Whether the file existed before deletion

**Example:**
```csharp
var parameters = new Dictionary<string, object?>
{
    ["target_path"] = "temp.txt"
};
var result = await executor.ExecuteAsync("delete_file", parameters);
```

### copy_file

Copies a file to a new location with progress tracking.

**Parameters:**
- `source_path` (string, required): Source file path
- `destination_path` (string, required): Destination file path
- `overwrite` (boolean, optional): Overwrite if exists (default: false)
- `create_directories` (boolean, optional): Create parent directories (default: true)

**Returns:**
- `source_path`: Absolute source path
- `destination_path`: Absolute destination path
- `size`: File size in bytes
- `overwritten`: Whether an existing file was overwritten

**Progress Reporting:** Reports percentage complete during copy

**Example:**
```csharp
var parameters = new Dictionary<string, object?>
{
    ["source_path"] = "large_file.zip",
    ["destination_path"] = "backup/large_file.zip",
    ["overwrite"] = true
};
var result = await executor.ExecuteAsync("copy_file", parameters);
```

### move_file

Moves or renames a file.

**Parameters:**
- `source_path` (string, required): Source file path
- `destination_path` (string, required): Destination file path
- `overwrite` (boolean, optional): Overwrite if exists (default: false)
- `create_directories` (boolean, optional): Create parent directories (default: true)

**Returns:**
- `source_path`: Original file path
- `destination_path`: New file path
- `size`: File size in bytes

**Example:**
```csharp
var parameters = new Dictionary<string, object?>
{
    ["source_path"] = "old_name.txt",
    ["destination_path"] = "new_name.txt"
};
var result = await executor.ExecuteAsync("move_file", parameters);
```

### list_directory

Lists contents of a directory with optional filtering.

**Parameters:**
- `directory_path` (string, required): Directory to list
- `pattern` (string, optional): Search pattern (e.g., "*.txt")
- `recursive` (boolean, optional): Include subdirectories (default: false)
- `include_hidden` (boolean, optional): Include hidden files (default: false)

**Returns:**
- `directory_path`: Absolute directory path
- `entries`: Array of file/directory information
  - `name`: File/directory name
  - `path`: Full path
  - `type`: "file" or "directory"
  - `size`: Size in bytes (files only)
  - `modified`: Last modified date
- `total_count`: Total number of entries
- `total_size`: Combined size of all files

**Example:**
```csharp
var parameters = new Dictionary<string, object?>
{
    ["directory_path"] = "src",
    ["pattern"] = "*.cs",
    ["recursive"] = true
};
var result = await executor.ExecuteAsync("list_directory", parameters);
```

## Text Processing Tools

### format_text

Formats text in various formats (JSON, XML, etc.).

**Parameters:**
- `input_text` (string, required): Text to format
- `operation` (string, required): Format operation
  - "format_json": Pretty-print JSON
  - "minify_json": Compact JSON
  - "format_xml": Pretty-print XML
  - "minify_xml": Compact XML

**Returns:**
- `formatted_text`: The formatted text
- `original_size`: Original text size
- `formatted_size`: Formatted text size

**Example:**
```csharp
var parameters = new Dictionary<string, object?>
{
    ["input_text"] = "{\"name\":\"test\",\"value\":123}",
    ["operation"] = "format_json"
};
var result = await executor.ExecuteAsync("format_text", parameters);
```

### replace_text

Performs text replacement with optional regex support.

**Parameters:**
- `text` (string, required): Input text
- `search_pattern` (string, required): Pattern to search for
- `replacement` (string, required): Replacement text
- `use_regex` (boolean, optional): Use regex pattern (default: false)
- `case_sensitive` (boolean, optional): Case sensitive search (default: true)

**Returns:**
- `result`: Text after replacements
- `count`: Number of replacements made
- `locations`: Array of replacement locations (if not too many)

**Example:**
```csharp
var parameters = new Dictionary<string, object?>
{
    ["text"] = "Hello World. Hello Universe.",
    ["search_pattern"] = "Hello",
    ["replacement"] = "Hi",
    ["case_sensitive"] = false
};
var result = await executor.ExecuteAsync("replace_text", parameters);
```

### search_text

Searches for patterns in text with regex support.

**Parameters:**
- `text` (string, required): Text to search
- `pattern` (string, required): Search pattern
- `use_regex` (boolean, optional): Use regex (default: false)
- `case_sensitive` (boolean, optional): Case sensitive (default: true)
- `return_matches` (boolean, optional): Return match details (default: true)

**Returns:**
- `found`: Whether pattern was found
- `count`: Number of matches
- `matches`: Array of match details (if return_matches is true)
  - `text`: Matched text
  - `index`: Character index
  - `line`: Line number
  - `column`: Column number

**Example:**
```csharp
var parameters = new Dictionary<string, object?>
{
    ["text"] = "Error: File not found\nWarning: Low memory\nError: Network timeout",
    ["pattern"] = @"Error: (.+)",
    ["use_regex"] = true
};
var result = await executor.ExecuteAsync("search_text", parameters);
```

## Web Tools

### http_request

Makes HTTP requests with full control over headers and body.

**Parameters:**
- `url` (string, required): Target URL
- `method` (string, optional): HTTP method (default: "GET")
- `headers` (object, optional): Request headers as Dictionary<string, object?>
- `body` (string, optional): Request body
- `timeout_seconds` (integer, optional): Request timeout (default: 30)

**Returns:**
- `status_code`: HTTP status code
- `content`: Response body
- `headers`: Response headers
- `content_length`: Response size
- `elapsed_ms`: Request duration

**Example:**
```csharp
var parameters = new Dictionary<string, object?>
{
    ["url"] = "https://api.example.com/data",
    ["method"] = "POST",
    ["headers"] = new Dictionary<string, object?>
    {
        ["Content-Type"] = "application/json",
        ["Authorization"] = "Bearer token123"
    },
    ["body"] = "{\"query\":\"test\"}"
};
var result = await executor.ExecuteAsync("http_request", parameters);
```

### json_processor

Processes JSON data using JSONPath queries.

**Parameters:**
- `json` (string, required): JSON string to process
- `query` (string, optional): JSONPath query
- `operation` (string, optional): Operation to perform
  - "parse": Parse and return as object
  - "query": Execute JSONPath query
  - "format": Pretty-print
  - "minify": Compact format

**Returns:**
- `result`: Query result or processed JSON
- `type`: Result type description
- `count`: Number of results (for queries)

**JSONPath Examples:**
- `$`: Root element
- `$.store.book[*]`: All books
- `$.store.book[?(@.price < 10)]`: Books cheaper than 10
- `$..author`: All authors (recursive)

**Example:**
```csharp
var parameters = new Dictionary<string, object?>
{
    ["json"] = jsonString,
    ["query"] = "$.items[?(@.active == true)].name",
    ["operation"] = "query"
};
var result = await executor.ExecuteAsync("json_processor", parameters);
```

## System Tools

### system_info

Retrieves comprehensive system information.

**Parameters:** None

**Returns:**
- `os_description`: Operating system description
- `os_version`: OS version
- `machine_name`: Computer name
- `user_name`: Current user
- `processor_count`: Number of processors
- `total_memory_mb`: Total RAM in MB
- `available_memory_mb`: Available RAM in MB
- `architecture`: System architecture
- `dotnet_version`: .NET runtime version

**Example:**
```csharp
var result = await executor.ExecuteAsync("system_info", new Dictionary<string, object?>());
```

### process_info

Gets information about running processes.

**Parameters:**
- `process_name` (string, optional): Filter by process name
- `process_id` (integer, optional): Get specific process by ID

**Returns:**
- `processes`: Array of process information
  - `id`: Process ID
  - `name`: Process name
  - `cpu_percent`: CPU usage percentage
  - `memory_mb`: Memory usage in MB
  - `thread_count`: Number of threads
  - `start_time`: Process start time
  - `responding`: Whether process is responding

**Example:**
```csharp
var parameters = new Dictionary<string, object?>
{
    ["process_name"] = "dotnet"
};
var result = await executor.ExecuteAsync("process_info", parameters);
```

### date_time

Performs date and time operations.

**Parameters:**
- `operation` (string, required): Operation to perform
  - "current": Get current date/time
  - "format": Format a date/time
  - "parse": Parse a date/time string
  - "add": Add time to a date
  - "diff": Calculate difference
- `date` (string, optional): Date string for operations
- `format` (string, optional): Date format string
- `add_value` (number, optional): Value to add
- `add_unit` (string, optional): Unit to add (days, hours, etc.)

**Returns:**
- `result`: Operation result
- `timestamp`: ISO 8601 timestamp
- `unix_timestamp`: Unix timestamp

**Example:**
```csharp
var parameters = new Dictionary<string, object?>
{
    ["operation"] = "add",
    ["date"] = "2024-01-01",
    ["add_value"] = 30,
    ["add_unit"] = "days"
};
var result = await executor.ExecuteAsync("date_time", parameters);
```

### encoding

Encodes and decodes text in various formats.

**Parameters:**
- `text` (string, required): Text to encode/decode
- `operation` (string, required): Operation
  - "base64_encode"
  - "base64_decode"
  - "url_encode"
  - "url_decode"
  - "html_encode"
  - "html_decode"

**Returns:**
- `result`: Encoded/decoded text
- `original_length`: Original text length
- `result_length`: Result text length

**Example:**
```csharp
var parameters = new Dictionary<string, object?>
{
    ["text"] = "Hello World!",
    ["operation"] = "base64_encode"
};
var result = await executor.ExecuteAsync("encoding", parameters);
```

### environment_variables

Manages environment variables.

**Parameters:**
- `operation` (string, required): Operation
  - "get": Get specific variable
  - "list": List all variables
  - "set": Set variable value
- `name` (string, optional): Variable name
- `value` (string, optional): Variable value (for set)

**Returns:**
- For "get": `value` of the variable
- For "list": `variables` dictionary
- For "set": `previous_value` and `new_value`

**Example:**
```csharp
var parameters = new Dictionary<string, object?>
{
    ["operation"] = "get",
    ["name"] = "PATH"
};
var result = await executor.ExecuteAsync("environment_variables", parameters);
```

## Git Tools

### git_diff

Gets git diff information for a repository.

**Parameters:**
- `repository_path` (string, optional): Repository path (default: current directory)
- `commit_from` (string, optional): Starting commit/branch
- `commit_to` (string, optional): Ending commit/branch (default: HEAD)
- `file_path` (string, optional): Specific file to diff
- `unified_lines` (integer, optional): Context lines (default: 3)

**Returns:**
- `diff`: Unified diff output
- `files_changed`: Number of files changed
- `insertions`: Total lines added
- `deletions`: Total lines removed
- `files`: Array of changed files
  - `path`: File path
  - `status`: Change status (added, modified, deleted)
  - `insertions`: Lines added
  - `deletions`: Lines removed

**Example:**
```csharp
var parameters = new Dictionary<string, object?>
{
    ["repository_path"] = ".",
    ["commit_from"] = "main",
    ["commit_to"] = "feature-branch"
};
var result = await executor.ExecuteAsync("git_diff", parameters);
```

## Tool Categories

Tools are organized into categories for easier discovery:

- **FileSystem**: File and directory operations
- **Text**: Text processing and manipulation
- **Web**: HTTP and web-related operations
- **System**: System information and utilities
- **Data**: Data processing and transformation
- **Development**: Development tools (git, etc.)
- **Security**: Security-related operations
- **Utility**: General utility tools

## Common Patterns

### Error Handling

All tools follow consistent error handling:

```csharp
var result = await executor.ExecuteAsync("tool_name", parameters);

if (!result.IsSuccessful)
{
    Console.WriteLine($"Error: {result.ErrorMessage}");
    
    // Check for specific error codes
    if (result.ErrorCode == "FILE_NOT_FOUND")
    {
        // Handle file not found
    }
}
```

### Working with Large Files

File tools automatically handle large files efficiently:

```csharp
// Reading large files is memory-efficient
var result = await executor.ExecuteAsync("read_file", 
    new Dictionary<string, object?> { ["file_path"] = "large.log" });

// Output will be truncated if too large
if (result.Metadata.ContainsKey("output_truncated"))
{
    Console.WriteLine("Warning: Output was truncated");
}
```

### Cancellation Support

Long-running tools support cancellation:

```csharp
var cts = new CancellationTokenSource();
var context = new ToolExecutionContext 
{ 
    CancellationToken = cts.Token 
};

// Cancel after 10 seconds
cts.CancelAfter(TimeSpan.FromSeconds(10));

try
{
    var result = await executor.ExecuteAsync("copy_file", parameters, context);
}
catch (OperationCanceledException)
{
    Console.WriteLine("Operation cancelled");
}
```

## Best Practices

1. **Always check IsSuccessful** before using result data
2. **Use appropriate timeouts** for network operations
3. **Handle cancellation** for long-running operations
4. **Validate file paths** before operations
5. **Use progress reporting** for user feedback
6. **Check metadata** for additional information
7. **Handle errors gracefully** with meaningful messages

## Next Steps

- Learn to [Create Custom Tools](custom-tools.md)
- Explore [Advanced Features](advanced-features.md)
- See [Examples and Tutorials](examples.md)