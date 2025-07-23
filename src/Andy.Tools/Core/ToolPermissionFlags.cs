namespace Andy.Tools.Core;

/// <summary>
/// Represents permission flags required by a tool.
/// </summary>
[Flags]
public enum ToolPermissionFlags
{
    /// <summary>No special permissions required.</summary>
    None = 0,
    /// <summary>Requires file system read access.</summary>
    FileSystemRead = 1 << 0,
    /// <summary>Requires file system write access.</summary>
    FileSystemWrite = 1 << 1,
    /// <summary>Requires network access.</summary>
    Network = 1 << 2,
    /// <summary>Requires process execution permissions.</summary>
    ProcessExecution = 1 << 3,
    /// <summary>Requires system information access.</summary>
    SystemInformation = 1 << 4,
    /// <summary>Requires environment variable access.</summary>
    Environment = 1 << 5,
    /// <summary>Requires elevated privileges.</summary>
    Elevated = 1 << 6
}
