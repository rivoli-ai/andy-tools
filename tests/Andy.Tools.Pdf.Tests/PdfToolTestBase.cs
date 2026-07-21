using Andy.Tools.Core;
using Andy.Tools.Library;

namespace Andy.Tools.Pdf.Tests;

/// <summary>
/// Base class for PDF tool tests: owns a private temp directory (auto-cleaned), writes byte
/// fixtures into it, builds an execution context whose <see cref="ToolPermissions.AllowedPaths"/>
/// is scoped to that directory, and exposes small reflection helpers for reading the anonymous
/// result objects the tools return.
/// </summary>
public abstract class PdfToolTestBase : IDisposable
{
    protected string WorkingDirectory { get; }

    protected PdfToolTestBase()
    {
        WorkingDirectory = Path.Combine(Path.GetTempPath(), $"andy_pdf_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(WorkingDirectory);
    }

    /// <summary>Writes fixture bytes to a file inside the working directory and returns its full path.</summary>
    protected string WriteFixture(byte[] bytes, string fileName = "fixture.pdf")
    {
        var path = Path.Combine(WorkingDirectory, fileName);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    /// <summary>An execution context allowing only the working directory, optionally pre-cancelled.</summary>
    protected ToolExecutionContext Context(
        IEnumerable<string>? allowedPaths = null,
        IEnumerable<string>? blockedPaths = null,
        CancellationToken cancellationToken = default) => new()
        {
            WorkingDirectory = WorkingDirectory,
            CancellationToken = cancellationToken,
            Permissions = new ToolPermissions
            {
                FileSystemAccess = true,
                AllowedPaths = new HashSet<string>(allowedPaths ?? new[] { WorkingDirectory }),
                BlockedPaths = blockedPaths is null ? null : new HashSet<string>(blockedPaths),
            },
        };

    /// <summary>Initializes a tool and executes it against the given parameters and context.</summary>
    protected static async Task<ToolResult> ExecuteAsync(
        ToolBase tool, Dictionary<string, object?> parameters, ToolExecutionContext context)
    {
        await tool.InitializeAsync();
        return await tool.ExecuteAsync(parameters, context);
    }

    /// <summary>Reads a named property from an anonymous result object via reflection.</summary>
    protected static object? Prop(object? data, string name)
    {
        ArgumentNullException.ThrowIfNull(data);
        var property = data.GetType().GetProperty(name)
            ?? throw new InvalidOperationException($"Result has no property '{name}'. Shape: {data.GetType()}");
        return property.GetValue(data);
    }

    /// <summary>Reads a named property and casts it to <typeparamref name="T"/>.</summary>
    protected static T Prop<T>(object? data, string name) => (T)Prop(data, name)!;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

        try
        {
            if (Directory.Exists(WorkingDirectory))
            {
                Directory.Delete(WorkingDirectory, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup; a leaked temp directory must not fail a test run.
        }
    }
}
