namespace Andy.Tools.Core;

/// <summary>
/// Represents capabilities required by a tool.
/// </summary>
[Flags]
public enum ToolCapability
{
    /// <summary>No special capabilities required.</summary>
    None = 0,
    /// <summary>Requires file system access.</summary>
    FileSystem = 1 << 0,
    /// <summary>Requires network access.</summary>
    Network = 1 << 1,
    /// <summary>Requires process execution.</summary>
    ProcessExecution = 1 << 2,
    /// <summary>Requires environment variable access.</summary>
    Environment = 1 << 3,
    /// <summary>Requires elevated privileges.</summary>
    Elevated = 1 << 4,
    /// <summary>Tool is potentially destructive.</summary>
    Destructive = 1 << 5,
    /// <summary>Tool performs long-running operations.</summary>
    LongRunning = 1 << 6,
    /// <summary>Tool requires user interaction.</summary>
    Interactive = 1 << 7
}
