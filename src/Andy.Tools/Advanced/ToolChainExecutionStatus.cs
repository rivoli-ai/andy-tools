namespace Andy.Tools.Advanced;

/// <summary>
/// Status of tool chain execution.
/// </summary>
public enum ToolChainExecutionStatus
{
    /// <summary>Not started.</summary>
    NotStarted,
    /// <summary>Currently running.</summary>
    Running,
    /// <summary>Completed successfully.</summary>
    Completed,
    /// <summary>Failed with errors.</summary>
    Failed,
    /// <summary>Cancelled by user.</summary>
    Cancelled,
    /// <summary>Partially completed with some failures.</summary>
    PartiallyCompleted
}
