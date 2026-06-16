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

### datetime_tool

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
var result = await executor.ExecuteAsync("datetime_tool", parameters);
```

### encoding_tool

Encodes and decodes text in various formats.

**Parameters:**
- `input_text` (string, required): Text to encode/decode
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
    ["input_text"] = "Hello World!",
    ["operation"] = "base64_encode"
};
var result = await executor.ExecuteAsync("encoding_tool", parameters);
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

## Data / DataFrame Tools

The `Andy.Tools.Data` package adds 28 `dataframe_*` tools — thin Andy `ITool` adapters over the
framework-independent [`Andy.Data`](https://github.com/rivoli-ai/andy-data) DuckDB-backed dataframe
engine. They load, inspect, transform, aggregate, join, reshape, and export tabular data
(CSV/JSON/Parquet/partitioned Parquet/Delta Lake) through a **closed, injection-safe vocabulary** —
no model-supplied SQL and no code execution. Register them after `AddAndyTools()`:

```csharp
services.AddAndyTools();
services.AddAndyDataFrameTools();   // registers all dataframe_* tools
// optional path scoping:  services.AddSingleton<IPathPolicy, MyPolicy>();
```

**Datasets.** Every tool works on named, session-scoped datasets. A load registers a dataset under a
`dataset_id` (`^[A-Za-z_][A-Za-z0-9_]{0,127}$`); a transform reads one or more datasets and registers
its result under `into` (or, when `into` is omitted, replaces `dataset_id` in place). `dataframe_join`,
`dataframe_union`, and `dataframe_rename` require `into`.

**Standard envelope.** Unless noted, every tool returns the same shape: `success`, `dataset_id`,
`schema` (array of `{ name, type, nullable }`), `row_count`, `preview_rows` (a bounded preview, ≤ 50
rows), `preview_truncated`, `warnings`, and `stats` (`{ elapsed_ms, bytes_scanned, rows_produced,
plan? }`). On failure: `success=false`, `error_code` (e.g. `DATASET_NOT_FOUND`, `COLUMN_NOT_FOUND`,
`INVALID_PREDICATE`, `FILE_NOT_FOUND`, `PERMISSION_DENIED`), `message`, and optional `details`. The
report-style tools (`dataframe_profile`, `dataframe_assert`, `dataframe_value_counts`,
`dataframe_list`) place one row of output per column/expectation/value/dataset in `preview_rows`.

**`explain`.** Every transform accepts an optional `explain` (boolean, default `false`); when `true`
the DuckDB query plan is returned in `stats.plan`.

The structured **predicate trees** (`dataframe_filter`, `group_by` `having`) and **expression trees**
(`dataframe_with_column`) are documented in the Andy.Data
[operations reference](https://github.com/rivoli-ai/andy-data/blob/main/docs/operations.md#predicate-trees).

### dataframe_load_csv

Loads a CSV file (or glob such as `data/*.csv`) into a named dataset; column types are inferred by
sampling unless overridden.

**Parameters:**
- `path` (string, required): CSV file or glob
- `dataset_id` (string, required): Id to register the dataset under
- `header` (boolean, optional): Whether row 1 holds column names (default: auto-detect)
- `delimiter` (string, optional): Field delimiter (default: auto-detect)
- `quote` (string, optional): Quote character (default: auto-detect)
- `null_string` (string, optional): Token to read as NULL (e.g. `"NA"`)
- `columns` (object, optional): Column→DuckDB-type overrides, e.g. `{ "amount": "DECIMAL(12,2)" }`
- `sample_size` (integer, optional): Rows sampled for type inference (default: 20480; `-1` = whole file)

**Example:**
```csharp
var parameters = new Dictionary<string, object?>
{
    ["path"] = "data/sales.csv",
    ["dataset_id"] = "sales"
};
var result = await executor.ExecuteAsync("dataframe_load_csv", parameters);
```

### dataframe_load_json

Loads a JSON file (or glob) — newline-delimited JSON (NDJSON) or a top-level array of objects; types
are inferred from the values.

**Parameters:**
- `path` (string, required): JSON file or glob (e.g. `data/*.ndjson`)
- `dataset_id` (string, required): Id to register the dataset under
- `format` (string, optional): `auto` (default), `newline_delimited`, or `array`

### dataframe_load_parquet

Loads a Parquet file, glob, or Hive-partitioned directory glob; schema/types come from file metadata.

**Parameters:**
- `path` (string, required): File, glob, or partitioned-dir glob (e.g. `events/**/*.parquet`)
- `dataset_id` (string, required): Id to register the dataset under
- `hive_partitioning` (boolean, optional): Expose `key=value/` directories as partition columns (default: auto)
- `union_by_name` (boolean, optional): Align columns by name across files with differing schemas (default: false)

### dataframe_load_delta

Loads a Delta Lake table; with no `version`/`timestamp` it reads the latest snapshot, otherwise it
performs time travel.

**Parameters:**
- `path` (string, required): Delta table root directory
- `dataset_id` (string, required): Id to register the dataset under
- `version` (integer, optional): Load this snapshot version (mutually exclusive with `timestamp`)
- `timestamp` (string, optional): Load the latest version at/before this ISO-8601 instant (mutually exclusive with `version`)

### dataframe_schema

Returns a dataset's column names, types, and nullability without scanning data.

**Parameters:**
- `dataset_id` (string, required): Dataset to describe

### dataframe_preview

Returns a bounded set of rows: first (`head`), last (`tail`), or a random `sample`.

**Parameters:**
- `dataset_id` (string, required): Dataset to preview
- `mode` (string, optional): `head` (default), `tail`, or `sample`
- `limit` (integer, optional): Rows to return, 1..1000 (default: 50)
- `seed` (integer, optional): Required when `mode=sample`; makes sampling repeatable

### dataframe_profile

A `describe()`-style per-column summary. `preview_rows` holds one stats row per column.

**Parameters:**
- `dataset_id` (string, required): Dataset to profile
- `columns` (array, optional): Subset of columns (default: all)
- `quantiles` (array, optional): Quantiles in [0,1] for numeric columns (default: `[0.25, 0.5, 0.75]`)

### dataframe_value_counts

Counts occurrences of each distinct value of a column (`{ <column>, count, proportion }`), ordered by
count descending. Registers the result.

**Parameters:**
- `dataset_id` (string, required): Input dataset
- `column` (string, required): Column to count
- `into` (string, optional): Output id (defaults to `dataset_id`)
- `limit` (integer, optional): Keep the top-N most frequent values
- `dropna` (boolean, optional): Exclude NULLs (default: true)

### dataframe_assert

Evaluates data-quality expectations and returns a per-expectation pass/fail report (does not modify or
register a dataset). `preview_rows` holds one row per expectation.

**Parameters:**
- `dataset_id` (string, required): Dataset to check
- `expectations` (array, required): `{ type, ... }` specs; `type` ∈ `not_null`, `unique`, `in_range`, `in_set`, `matches`, `row_count`

**Example:**
```csharp
var parameters = new Dictionary<string, object?>
{
    ["dataset_id"] = "sales",
    ["expectations"] = new object[]
    {
        new Dictionary<string, object?> { ["type"] = "not_null", ["column"] = "id" },
        new Dictionary<string, object?> { ["type"] = "in_range", ["column"] = "amount", ["min"] = 0 }
    }
};
var result = await executor.ExecuteAsync("dataframe_assert", parameters);
```

### dataframe_list

Lists the datasets registered in the session; `preview_rows` holds one row per dataset
(`dataset_id, row_count, column_count, source`). Takes no parameters.

### dataframe_select

Projects, renames, and reorders columns.

**Parameters:**
- `dataset_id` (string, required): Input dataset
- `columns` (array, required): Column names, or `{ column, as }` objects to rename
- `into` (string, optional): Output id (defaults to `dataset_id`)
- `explain` (boolean, optional): Include the query plan in `stats.plan`

### dataframe_filter

Selects rows matching a structured predicate tree (no SQL).

**Parameters:**
- `dataset_id` (string, required): Input dataset
- `predicate` (object, required): A predicate tree of condition/logical nodes
- `into` (string, optional): Output id (defaults to `dataset_id`)
- `explain` (boolean, optional): Include the query plan in `stats.plan`

**Example:**
```csharp
var parameters = new Dictionary<string, object?>
{
    ["dataset_id"] = "sales",
    ["into"] = "big_sales",
    ["predicate"] = new Dictionary<string, object?>
    {
        ["column"] = "amount", ["op"] = "gte", ["value"] = 1000
    }
};
var result = await executor.ExecuteAsync("dataframe_filter", parameters);
```

### dataframe_with_column

Adds or replaces a column computed from a structured expression tree (no SQL).

**Parameters:**
- `dataset_id` (string, required): Input dataset
- `name` (string, required): New or replaced column name
- `expression` (object, required): An expression tree
- `into` (string, optional): Output id (defaults to `dataset_id`)
- `explain` (boolean, optional): Include the query plan in `stats.plan`

### dataframe_rename

Renames columns; unmentioned columns are kept and order is preserved.

**Parameters:**
- `dataset_id` (string, required): Input dataset
- `into` (string, required): Output id
- `columns` (object, required): Map of old name → new name
- `explain` (boolean, optional): Include the query plan in `stats.plan`

### dataframe_group_by

Groups by zero or more columns and computes aggregates.

**Parameters:**
- `dataset_id` (string, required): Input dataset
- `group_by` (array, required): Grouping column names (may be empty for a grand total)
- `aggregations` (array, required): `{ column, function, alias, q?, column2? }` specs; `function` ∈ count, count_distinct, approx_count_distinct, sum, product, avg, min, max, median, mode, stddev(_pop/_samp), var(_pop/_samp), bool_and, bool_or, first, last, list, quantile, approx_quantile, corr, covar, arg_min, arg_max
- `into` (string, optional): Output id (defaults to `dataset_id`)
- `having` (object, optional): Predicate tree over the aggregated rows (columns must be group keys or aggregate aliases)
- `explain` (boolean, optional): Include the query plan in `stats.plan`

**Example:**
```csharp
var parameters = new Dictionary<string, object?>
{
    ["dataset_id"] = "sales",
    ["group_by"] = new[] { "region" },
    ["aggregations"] = new object[]
    {
        new Dictionary<string, object?> { ["column"] = "amount", ["function"] = "sum", ["alias"] = "total" },
        new Dictionary<string, object?> { ["column"] = "*", ["function"] = "count", ["alias"] = "n" }
    }
};
var result = await executor.ExecuteAsync("dataframe_group_by", parameters);
```

### dataframe_window

Adds window-function columns without collapsing rows.

**Parameters:**
- `dataset_id` (string, required): Input dataset
- `functions` (array, required): `{ function, column?, alias, args? }`; `function` ∈ row_number, rank, dense_rank, percent_rank, ntile, lag, lead, first_value, last_value, sum, avg, min, max, count
- `into` (string, optional): Output id (defaults to `dataset_id`)
- `partition_by` (array, optional): Partition column names
- `order_by` (array, optional): `{ column, direction (asc|desc), nulls (first|last) }`
- `frame` (object, optional): `{ start, end }` window-frame bounds
- `explain` (boolean, optional): Include the query plan in `stats.plan`

### dataframe_pivot

Reshapes long data to wide.

**Parameters:**
- `dataset_id` (string, required): Input dataset
- `index` (array, required): Columns that remain rows
- `columns` (string, required): Column whose distinct values become new columns
- `values` (string or array, required): A column name, or `{ column, aggregation, alias? }` objects
- `into` (string, optional): Output id (defaults to `dataset_id`)
- `aggregation` (string, optional): `sum` (default), `avg`, `min`, `max`, `count` (scalar-`values` form only)
- `explain` (boolean, optional): Include the query plan in `stats.plan`

### dataframe_unpivot

Reshapes wide data to long.

**Parameters:**
- `dataset_id` (string, required): Input dataset
- `id_columns` (array, required): Columns kept as row identifiers (may be empty)
- `value_columns` (array, required): Columns to stack into rows
- `into` (string, optional): Output id (defaults to `dataset_id`)
- `name_to` (string, optional): Output name column (default: `name`)
- `value_to` (string, optional): Output value column (default: `value`)
- `explain` (boolean, optional): Include the query plan in `stats.plan`

### dataframe_unnest

Explodes a `LIST` column so each element becomes its own row; other columns are replicated.

**Parameters:**
- `dataset_id` (string, required): Input dataset
- `column` (string, required): LIST column to explode
- `into` (string, optional): Output id (defaults to `dataset_id`)
- `explain` (boolean, optional): Include the query plan in `stats.plan`

### dataframe_join

Joins two datasets into `into`.

**Parameters:**
- `left` (string, required): Left dataset id
- `right` (string, required): Right dataset id
- `into` (string, required): Output id
- `how` (string, optional): `inner` (default), `left`, `right`, `full`, `semi`, `anti`, `cross`, `asof`
- `on` (array, optional): Key columns present in both sides
- `left_on` / `right_on` (array, optional): Equal-length key lists (alternative to `on`)
- `asof_op` (string, optional): `>=` (default) or `<=` for the as-of column when `how=asof`
- `suffix` (string, optional): Suffix for overlapping non-key right columns (default: `_right`)
- `explain` (boolean, optional): Include the query plan in `stats.plan`

### dataframe_union

Concatenates two or more datasets into `into`.

**Parameters:**
- `datasets` (array, required): Ordered ids to concatenate (≥ 2)
- `into` (string, required): Output id
- `by_name` (boolean, optional): Align columns by name rather than position (default: false)
- `distinct` (boolean, optional): Drop duplicate rows across the union (default: false)
- `explain` (boolean, optional): Include the query plan in `stats.plan`

### dataframe_sort

Orders rows by one or more keys; optional top-N.

**Parameters:**
- `dataset_id` (string, required): Input dataset
- `by` (array, required): `{ column, direction (asc|desc), nulls (first|last) }`
- `into` (string, optional): Output id (defaults to `dataset_id`)
- `limit` (integer, optional): Keep only the first N rows after sorting
- `explain` (boolean, optional): Include the query plan in `stats.plan`

### dataframe_sample

Materializes a deterministic reservoir sample.

**Parameters:**
- `dataset_id` (string, required): Input dataset
- `n` (integer, required): Reservoir size (≥ 1)
- `seed` (integer, required): Deterministic seed for repeatability
- `into` (string, optional): Output id (defaults to `dataset_id`)
- `explain` (boolean, optional): Include the query plan in `stats.plan`

### dataframe_distinct

Removes duplicate rows (whole-row, or per `columns` combination).

**Parameters:**
- `dataset_id` (string, required): Input dataset
- `into` (string, optional): Output id (defaults to `dataset_id`)
- `columns` (array, optional): Columns to dedupe on (default: all)
- `keep` (string, optional): `first` (default) or `last` within each group, under `order_by`
- `order_by` (array, optional): `{ column, direction (asc|desc) }` defining within-group order
- `explain` (boolean, optional): Include the query plan in `stats.plan`

### dataframe_fillna

Replaces NULLs — scalar mode (`value`/`values`) or carry mode (`method` ffill/bfill).

**Parameters:**
- `dataset_id` (string, required): Input dataset
- `into` (string, optional): Output id (defaults to `dataset_id`)
- `value` (string, optional): Scalar mode: global replacement, coerced to each column's type
- `values` (object, optional): Scalar mode: per-column overrides
- `method` (string, optional): Carry mode: `ffill` or `bfill` (requires `order_by`; mutually exclusive with `value`/`values`)
- `order_by` (array, optional): Ordering for `method`
- `partition_by` (array, optional): Carry-mode groups; the fill restarts per group
- `columns` (array, optional): Carry-mode subset to fill (default: all)
- `explain` (boolean, optional): Include the query plan in `stats.plan`

### dataframe_dropna

Removes rows with NULLs.

**Parameters:**
- `dataset_id` (string, required): Input dataset
- `into` (string, optional): Output id (defaults to `dataset_id`)
- `columns` (array, optional): Columns to check (default: all)
- `how` (string, optional): Drop when `any` (default) or `all` of the checked columns are NULL
- `explain` (boolean, optional): Include the query plan in `stats.plan`

### dataframe_export

Writes a dataset to disk. Requires filesystem write permission.

**Parameters:**
- `dataset_id` (string, required): Dataset to export
- `path` (string, required): Output file or directory
- `format` (string, required): `csv`, `parquet`, `json`, or `delta`
- `mode` (string, optional): `error` (default; fail if target exists), `append` (Delta only), or `overwrite`
- `partition_by` (array, optional): Partition columns (Parquet and Delta)
- `compression` (string, optional): Codec, e.g. `snappy`, `zstd`, `gzip` (Parquet/JSON)
- `array` (boolean, optional): JSON: write a top-level array instead of NDJSON (default: false)
- `header` (boolean, optional): CSV: write a header row (default: true)
- `delimiter` (string, optional): CSV field delimiter (default: `,`)
- `quote` (string, optional): CSV quote character (default: `"`)
- `escape` (string, optional): CSV escape character (default: `"`)

**Example:**
```csharp
var parameters = new Dictionary<string, object?>
{
    ["dataset_id"] = "summary",
    ["path"] = "out/summary.parquet",
    ["format"] = "parquet",
    ["mode"] = "overwrite"
};
var result = await executor.ExecuteAsync("dataframe_export", parameters);
```

### dataframe_drop

Releases a dataset; its backend resources are freed once no remaining dataset depends on them.

**Parameters:**
- `dataset_id` (string, required): Id of the dataset to release

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