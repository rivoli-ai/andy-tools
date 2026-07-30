namespace Andy.Tools.Library.System;

/// <summary>
/// Host-controlled limits for <see cref="ExecuteCommandTool"/>.
/// </summary>
public sealed class ExecuteCommandToolOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "ExecuteCommand";

    /// <summary>
    /// Gets or sets the maximum number of seconds any command may run.
    /// A null value preserves the model-requested or default timeout.
    /// </summary>
    public int? MaximumTimeoutSeconds { get; set; }
}
