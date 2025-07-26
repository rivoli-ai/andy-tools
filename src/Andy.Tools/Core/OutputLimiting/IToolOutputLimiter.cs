namespace Andy.Tools.Core.OutputLimiting;

/// <summary>
/// Interface for limiting and truncating tool output to prevent context overflow.
/// </summary>
public interface IToolOutputLimiter
{
    /// <summary>
    /// Limits the output based on the specified type and configuration.
    /// </summary>
    /// <param name="output">The output to limit.</param>
    /// <param name="outputType">The type of output being limited.</param>
    /// <param name="context">Additional context for the truncation.</param>
    /// <returns>A limited output result.</returns>
    public LimitedOutput LimitOutput(object output, OutputType outputType, OutputLimitContext? context = null);

    /// <summary>
    /// Checks if the output needs limiting.
    /// </summary>
    /// <param name="output">The output to check.</param>
    /// <param name="outputType">The type of output.</param>
    /// <returns>True if limiting is needed.</returns>
    public bool NeedsLimiting(object output, OutputType outputType);
}
