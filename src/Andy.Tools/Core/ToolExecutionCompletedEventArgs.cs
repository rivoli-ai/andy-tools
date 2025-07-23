namespace Andy.Tools.Core;

/// <summary>
/// Event arguments for tool execution completed events.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ToolExecutionCompletedEventArgs"/> class.
/// </remarks>
/// <param name="result">The execution result.</param>
public class ToolExecutionCompletedEventArgs(ToolExecutionResult result) : EventArgs
{
    /// <summary>
    /// Gets the execution result.
    /// </summary>
    public ToolExecutionResult Result { get; } = result;
}
