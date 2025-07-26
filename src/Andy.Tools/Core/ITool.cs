using System.Text.Json;

namespace Andy.Tools.Core;

/// <summary>
/// Base interface for all tools in the Andy system.
/// </summary>
public interface ITool
{
    /// <summary>
    /// Gets the metadata describing this tool.
    /// </summary>
    public ToolMetadata Metadata { get; }

    /// <summary>
    /// Initializes the tool with the given configuration.
    /// </summary>
    /// <param name="configuration">The tool configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the initialization operation.</returns>
    public Task InitializeAsync(Dictionary<string, object?>? configuration = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the tool with the given parameters and context.
    /// </summary>
    /// <param name="parameters">The parameters for tool execution.</param>
    /// <param name="context">The execution context.</param>
    /// <returns>A task representing the tool execution result.</returns>
    public Task<ToolResult> ExecuteAsync(Dictionary<string, object?> parameters, ToolExecutionContext context);

    /// <summary>
    /// Validates the given parameters for this tool.
    /// </summary>
    /// <param name="parameters">The parameters to validate.</param>
    /// <returns>A list of validation errors, or empty if valid.</returns>
    public IList<string> ValidateParameters(Dictionary<string, object?> parameters);

    /// <summary>
    /// Determines if this tool can execute with the given permissions.
    /// </summary>
    /// <param name="permissions">The permissions to check.</param>
    /// <returns>True if the tool can execute with the given permissions.</returns>
    public bool CanExecuteWithPermissions(ToolPermissions permissions);

    /// <summary>
    /// Disposes resources used by the tool.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the disposal operation.</returns>
    public Task DisposeAsync(CancellationToken cancellationToken = default);
}
