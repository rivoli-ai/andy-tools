namespace Andy.Tools.Advanced.ToolChains;

/// <summary>
/// Types of tool chain steps.
/// </summary>
public enum ToolChainStepType
{
    /// <summary>Tool execution step.</summary>
    Tool,
    /// <summary>Conditional branching step.</summary>
    Conditional,
    /// <summary>Parallel execution step.</summary>
    Parallel,
    /// <summary>Data transformation step.</summary>
    Transform,
    /// <summary>Loop/iteration step.</summary>
    Loop,
    /// <summary>Error handling step.</summary>
    ErrorHandler,
    /// <summary>Custom step.</summary>
    Custom
}
