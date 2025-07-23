namespace Andy.Tools.Core;

/// <summary>
/// Represents permissions granted to a tool for execution.
/// </summary>
public class ToolPermissions
{
    /// <summary>
    /// Gets or sets whether the tool can access the file system.
    /// </summary>
    public bool FileSystemAccess { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the tool can make network requests.
    /// </summary>
    public bool NetworkAccess { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the tool can execute processes.
    /// </summary>
    public bool ProcessExecution { get; set; } = false;

    /// <summary>
    /// Gets or sets whether the tool can access environment variables.
    /// </summary>
    public bool EnvironmentAccess { get; set; } = true;

    /// <summary>
    /// Gets or sets the allowed file system paths (null means all paths allowed if FileSystemAccess is true).
    /// </summary>
    public HashSet<string>? AllowedPaths { get; set; }

    /// <summary>
    /// Gets or sets the blocked file system paths (these paths are always blocked regardless of AllowedPaths).
    /// </summary>
    public HashSet<string>? BlockedPaths { get; set; }

    /// <summary>
    /// Gets or sets the allowed network hosts (null means all hosts allowed if NetworkAccess is true).
    /// </summary>
    public HashSet<string>? AllowedHosts { get; set; }

    /// <summary>
    /// Gets or sets the blocked network hosts (these hosts are always blocked regardless of AllowedHosts).
    /// </summary>
    public HashSet<string>? BlockedHosts { get; set; }

    /// <summary>
    /// Gets or sets custom permissions specific to tools.
    /// </summary>
    public Dictionary<string, object?> CustomPermissions { get; set; } = [];

    /// <summary>
    /// Gets or sets tool-specific permissions that override global permissions.
    /// </summary>
    public Dictionary<string, bool> ToolSpecificPermissions { get; set; } = [];

    /// <summary>
    /// Gets or sets the name of this permission profile.
    /// </summary>
    public string ProfileName { get; set; } = "default";

    /// <summary>
    /// Gets or sets the description of this permission profile.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets when this permission profile was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets when this permission profile was last modified.
    /// </summary>
    public DateTimeOffset ModifiedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Creates a copy of the current permissions with a new profile name.
    /// </summary>
    /// <param name="newProfileName">The new profile name.</param>
    /// <returns>A copy of the current permissions with the new profile name.</returns>
    public ToolPermissions Clone(string newProfileName)
    {
        return new ToolPermissions
        {
            FileSystemAccess = FileSystemAccess,
            NetworkAccess = NetworkAccess,
            ProcessExecution = ProcessExecution,
            EnvironmentAccess = EnvironmentAccess,
            AllowedPaths = AllowedPaths != null ? new HashSet<string>(AllowedPaths) : null,
            BlockedPaths = BlockedPaths != null ? new HashSet<string>(BlockedPaths) : null,
            AllowedHosts = AllowedHosts != null ? new HashSet<string>(AllowedHosts) : null,
            BlockedHosts = BlockedHosts != null ? new HashSet<string>(BlockedHosts) : null,
            CustomPermissions = new Dictionary<string, object?>(CustomPermissions),
            ToolSpecificPermissions = new Dictionary<string, bool>(ToolSpecificPermissions),
            ProfileName = newProfileName,
            Description = Description,
            CreatedAt = DateTimeOffset.UtcNow,
            ModifiedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Creates a copy of the current permissions.
    /// </summary>
    /// <returns>A copy of the current permissions.</returns>
    public ToolPermissions Clone()
    {
        return Clone(ProfileName);
    }
}
