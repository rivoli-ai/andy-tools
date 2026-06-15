using System.Collections;
using Andy.Tools.Core;
using Andy.Tools.Library;
using Andy.Tools.Library.Common;

namespace Andy.Tools.Semantic;

/// <summary>
/// Ranks code/text chunks by embedding similarity to a query, using a pluggable
/// <see cref="IEmbeddingProvider"/>.
/// </summary>
/// <remarks>
/// The tool has two constructors by design:
/// <list type="bullet">
/// <item>The parameterless constructor is used by the registry's <c>Activator.CreateInstance</c>
/// metadata probe (registration throws if a tool type has no parameterless constructor). It leaves
/// the provider unset, so execution returns a <c>NO_EMBEDDING_PROVIDER</c> failure until one is wired up.</item>
/// <item>The <see cref="SemanticSearchTool(IEmbeddingProvider)"/> constructor is selected by
/// <c>ActivatorUtilities.CreateInstance</c> at execution time when an <see cref="IEmbeddingProvider"/>
/// is registered in DI.</item>
/// </list>
/// There is intentionally no built-in default provider, which avoids locking the library into a
/// specific embedding API. This MVP embeds chunks on the fly (no persistent index); a persistent
/// index is a possible follow-up.
/// </remarks>
public class SemanticSearchTool : ToolBase
{
    private const int DefaultMaxResults = 5;
    private const int DefaultChunkLines = 50;

    private readonly IEmbeddingProvider? _provider;

    /// <summary>
    /// Initializes a new instance of the <see cref="SemanticSearchTool"/> class without an embedding
    /// provider. This constructor exists so the registry's metadata probe can instantiate the tool;
    /// execution will fail with <c>NO_EMBEDDING_PROVIDER</c> until a provider is configured.
    /// </summary>
    public SemanticSearchTool()
    {
        _provider = null;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SemanticSearchTool"/> class using the supplied
    /// embedding provider.
    /// </summary>
    /// <param name="provider">The embedding provider to use.</param>
    public SemanticSearchTool(IEmbeddingProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    /// <inheritdoc />
    public override ToolMetadata Metadata { get; } = new()
    {
        Id = "semantic_search",
        Name = "Semantic Search",
        Description = "Ranks code/text chunks from the given files by embedding similarity to a query using a pluggable embedding provider",
        Version = "1.0.0",
        Category = ToolCategory.TextProcessing,
        RequiredPermissions = ToolPermissionFlags.FileSystemRead,
        Parameters =
        [
            new()
            {
                Name = "query",
                Description = "The natural-language or code query to rank chunks against",
                Type = "string",
                Required = true
            },
            new()
            {
                Name = "paths",
                Description = "An array of file paths, directories, and/or glob patterns (e.g. 'src/*.cs') to search",
                Type = "array",
                Required = true
            },
            new()
            {
                Name = "max_results",
                Description = "Maximum number of chunks to return (default: 5)",
                Type = "integer",
                Required = false,
                DefaultValue = DefaultMaxResults,
                MinValue = 1,
                MaxValue = 50
            },
            new()
            {
                Name = "chunk_lines",
                Description = "Approximate number of lines per chunk (default: 50)",
                Type = "integer",
                Required = false,
                DefaultValue = DefaultChunkLines,
                MinValue = 1
            }
        ]
    };

    /// <summary>
    /// Computes the cosine similarity of two vectors. Returns 0 if either vector is zero-length,
    /// has zero magnitude, or the lengths differ.
    /// </summary>
    /// <param name="a">The first vector.</param>
    /// <param name="b">The second vector.</param>
    /// <returns>The cosine similarity in the range [-1, 1] (0 for degenerate inputs).</returns>
    public static double CosineSimilarity(float[] a, float[] b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        if (a.Length == 0 || a.Length != b.Length)
        {
            return 0d;
        }

        double dot = 0d;
        double magA = 0d;
        double magB = 0d;

        for (var i = 0; i < a.Length; i++)
        {
            dot += (double)a[i] * b[i];
            magA += (double)a[i] * a[i];
            magB += (double)b[i] * b[i];
        }

        if (magA <= 0d || magB <= 0d)
        {
            return 0d;
        }

        return dot / (Math.Sqrt(magA) * Math.Sqrt(magB));
    }

    /// <inheritdoc />
    protected override async Task<ToolResult> ExecuteInternalAsync(Dictionary<string, object?> parameters, ToolExecutionContext context)
    {
        if (_provider is null)
        {
            return ToolResults.Failure(
                "No embedding provider configured. Register an IEmbeddingProvider and call AddSemanticSearch(...).",
                "NO_EMBEDDING_PROVIDER");
        }

        var query = GetParameter<string>(parameters, "query");
        var patterns = GetParameterAsStringList(parameters, "paths") ?? [];
        var maxResults = GetParameter(parameters, "max_results", DefaultMaxResults);
        var chunkLines = GetParameter(parameters, "chunk_lines", DefaultChunkLines);

        if (string.IsNullOrWhiteSpace(query))
        {
            return ToolResults.InvalidParameter("query", query, "Query cannot be empty");
        }

        if (patterns.Count == 0)
        {
            return ToolResults.InvalidParameter("paths", null, "At least one path, directory, or glob pattern is required");
        }

        if (maxResults < 1)
        {
            maxResults = DefaultMaxResults;
        }

        if (chunkLines < 1)
        {
            chunkLines = DefaultChunkLines;
        }

        var workingDirectory = context.WorkingDirectory ?? Directory.GetCurrentDirectory();

        try
        {
            ReportProgress(context, "Resolving files...", 10);

            var resolution = ResolveFiles(patterns, workingDirectory, context);
            var files = resolution.Files;
            var skippedOutsideAllowed = resolution.SkippedOutsideAllowed;

            var chunks = new List<Chunk>();
            var skippedBinary = 0;

            foreach (var file in files)
            {
                if (IsBinaryFile(file))
                {
                    skippedBinary++;
                    continue;
                }

                var encoding = ToolHelpers.DetectEncoding(file);
                var content = await ToolHelpers.ReadTextFileAsync(file, encoding, context.CancellationToken);
                chunks.AddRange(SplitIntoChunks(file, content, chunkLines));
            }

            if (chunks.Count == 0)
            {
                var emptyMeta = new Dictionary<string, object?>
                {
                    ["files_matched"] = files.Count,
                    ["skipped_binary"] = skippedBinary,
                    ["skipped_outside_allowed"] = skippedOutsideAllowed,
                    ["chunks_considered"] = 0
                };

                return ToolResults.Success(new List<Dictionary<string, object?>>(), "No text chunks found to rank", emptyMeta);
            }

            ReportProgress(context, $"Embedding {chunks.Count} chunk(s)...", 50);

            // Embed the query first, then all chunk texts, in a single batch call.
            var texts = new List<string> { query };
            texts.AddRange(chunks.Select(c => c.Text));

            var vectors = await _provider.EmbedAsync(texts, context.CancellationToken);

            if (vectors.Count != texts.Count)
            {
                return ToolResults.Failure(
                    $"Embedding provider returned {vectors.Count} vectors for {texts.Count} inputs.",
                    "EMBEDDING_COUNT_MISMATCH");
            }

            var queryVector = vectors[0];

            var ranked = chunks
                .Select((chunk, i) => new
                {
                    chunk,
                    score = CosineSimilarity(queryVector, vectors[i + 1])
                })
                .OrderByDescending(x => x.score)
                .Take(maxResults)
                .Select(x => new Dictionary<string, object?>
                {
                    ["file"] = x.chunk.File,
                    ["start_line"] = x.chunk.StartLine,
                    ["end_line"] = x.chunk.EndLine,
                    ["score"] = x.score,
                    ["snippet"] = x.chunk.Text
                })
                .ToList();

            ReportProgress(context, "Ranking complete", 100);

            var metadata = new Dictionary<string, object?>
            {
                ["query"] = query,
                ["max_results"] = maxResults,
                ["chunk_lines"] = chunkLines,
                ["files_matched"] = files.Count,
                ["skipped_binary"] = skippedBinary,
                ["skipped_outside_allowed"] = skippedOutsideAllowed,
                ["chunks_considered"] = chunks.Count,
                ["result_count"] = ranked.Count
            };

            return ToolResults.Success(ranked, $"Ranked {chunks.Count} chunk(s); returning top {ranked.Count}", metadata);
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            return ToolResults.Failure("Semantic search was cancelled", "CANCELLED");
        }
        catch (UnauthorizedAccessException)
        {
            return ToolResults.AccessDenied(workingDirectory, "read");
        }
        catch (Exception ex)
        {
            return ToolResults.Failure($"Semantic search failed: {ex.Message}", "SEMANTIC_SEARCH_ERROR", details: ex);
        }
    }

    /// <summary>
    /// Resolves explicit file paths, directories (recursively), and glob patterns into a confined,
    /// de-duplicated list of files, each within the caller's allowed paths.
    /// </summary>
    private static FileResolution ResolveFiles(
        List<string> patterns,
        string workingDirectory,
        ToolExecutionContext context)
    {
        var results = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var skippedOutsideAllowed = 0;

        void Add(string candidate)
        {
            if (!File.Exists(candidate))
            {
                return;
            }

            if (!ToolHelpers.IsPathWithinAllowedPaths(candidate, context.Permissions))
            {
                skippedOutsideAllowed++;
                return;
            }

            if (seen.Add(candidate))
            {
                results.Add(candidate);
            }
        }

        foreach (var pattern in patterns)
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                continue;
            }

            if (pattern.Contains('*') || pattern.Contains('?'))
            {
                foreach (var match in ExpandGlob(pattern, workingDirectory))
                {
                    Add(match);
                }

                continue;
            }

            var safePath = SafeResolve(pattern, workingDirectory);
            if (safePath is null)
            {
                continue;
            }

            if (Directory.Exists(safePath))
            {
                foreach (var file in EnumerateDirectory(safePath))
                {
                    Add(file);
                }
            }
            else
            {
                Add(safePath);
            }
        }

        return new FileResolution(results, skippedOutsideAllowed);
    }

    /// <summary>
    /// Resolves a path to a confined absolute path. Relative paths are resolved against (and confined
    /// to) the working directory; absolute paths are normalized but not forced under the working
    /// directory, since cross-tree confinement is enforced separately via
    /// <see cref="ToolHelpers.IsPathWithinAllowedPaths"/>. Returns <c>null</c> if the path is invalid
    /// or escapes the working directory.
    /// </summary>
    private static string? SafeResolve(string path, string workingDirectory)
    {
        try
        {
            return Path.IsPathRooted(path)
                ? ToolHelpers.GetSafePath(path)
                : ToolHelpers.GetSafePath(path, workingDirectory);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static IEnumerable<string> EnumerateDirectory(string directory)
    {
        try
        {
            return Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or DirectoryNotFoundException)
        {
            return [];
        }
    }

    private static IEnumerable<string> ExpandGlob(string pattern, string workingDirectory)
    {
        var directoryPart = Path.GetDirectoryName(pattern);
        var filePart = Path.GetFileName(pattern);

        var searchDirectory = SafeResolve(
            string.IsNullOrEmpty(directoryPart) ? "." : directoryPart,
            workingDirectory);

        if (searchDirectory is null || !Directory.Exists(searchDirectory))
        {
            yield break;
        }

        IEnumerable<string> candidates;
        try
        {
            candidates = Directory.EnumerateFiles(searchDirectory, "*", SearchOption.TopDirectoryOnly);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or DirectoryNotFoundException)
        {
            yield break;
        }

        foreach (var candidate in candidates)
        {
            var name = Path.GetFileName(candidate);
            if (ToolHelpers.IsGlobMatch(name, filePart))
            {
                yield return candidate;
            }
        }
    }

    /// <summary>
    /// Splits text into chunks of approximately <paramref name="chunkLines"/> lines, tracking the
    /// 1-based start and end line of each chunk. Whitespace-only chunks are dropped.
    /// </summary>
    private static IEnumerable<Chunk> SplitIntoChunks(string file, string content, int chunkLines)
    {
        var lines = content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

        for (var start = 0; start < lines.Length; start += chunkLines)
        {
            var count = Math.Min(chunkLines, lines.Length - start);
            var slice = string.Join("\n", lines, start, count);

            if (string.IsNullOrWhiteSpace(slice))
            {
                continue;
            }

            yield return new Chunk(file, start + 1, start + count, slice);
        }
    }

    /// <summary>
    /// Heuristically determines whether a file is binary by scanning the first 1KB for a null byte.
    /// </summary>
    private static bool IsBinaryFile(string filePath)
    {
        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            var buffer = new byte[1024];
            var read = stream.Read(buffer, 0, buffer.Length);
            for (var i = 0; i < read; i++)
            {
                if (buffer[i] == 0)
                {
                    return true;
                }
            }

            return false;
        }
        catch
        {
            return true;
        }
    }

    private static List<string>? GetParameterAsStringList(Dictionary<string, object?> parameters, string name)
    {
        if (!parameters.TryGetValue(name, out var value) || value == null)
        {
            return null;
        }

        return value switch
        {
            List<string> list => list,
            string[] array => [.. array],
            IEnumerable<string> enumerable => [.. enumerable],
            IEnumerable enumerable => [.. enumerable.Cast<object?>().Select(o => o?.ToString() ?? string.Empty)],
            _ => null
        };
    }

    private readonly record struct Chunk(string File, int StartLine, int EndLine, string Text);

    private sealed record FileResolution(List<string> Files, int SkippedOutsideAllowed);
}
