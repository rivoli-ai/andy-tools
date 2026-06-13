using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Andy.Tools.Core;

/// <summary>
/// Service for managing tool permission profiles with JSON persistence.
/// </summary>
public class PermissionProfileService : IPermissionProfileService
{
    private readonly ILogger<PermissionProfileService> _logger;
    private readonly string _profileDirectory;
    private readonly JsonSerializerOptions _jsonOptions;
    private ToolPermissions _currentPermissions;
    private bool _defaultProfileLoaded;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    /// <summary>
    /// Initializes a new instance of the <see cref="PermissionProfileService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public PermissionProfileService(ILogger<PermissionProfileService> logger)
    {
        _logger = logger;
        _profileDirectory = GetProfileDirectory();
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        _currentPermissions = CreateDefaultPermissions();

        // Ensure the profile directory exists
        Directory.CreateDirectory(_profileDirectory);

        // The persisted "default" profile is loaded lazily on first access (see GetCurrentPermissionsAsync)
        // rather than via a fire-and-forget Task in the constructor, which raced with the first read and
        // could expose built-in defaults or a half-applied profile.
    }

    /// <inheritdoc />
    public async Task<ToolPermissions> GetCurrentPermissionsAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            await EnsureDefaultProfileLoadedAsync(cancellationToken);
            return _currentPermissions.Clone();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    // Loads the persisted "default" profile once, on first access, while the caller holds the semaphore.
    // Sets _currentPermissions directly (does NOT call SetCurrentPermissionsAsync, which would re-enter
    // the semaphore and deadlock). Marked loaded even on failure so it is attempted at most once.
    private async Task EnsureDefaultProfileLoadedAsync(CancellationToken cancellationToken)
    {
        if (_defaultProfileLoaded)
        {
            return;
        }

        _defaultProfileLoaded = true;

        try
        {
            if (await ProfileExistsAsync("default", cancellationToken))
            {
                var defaultPermissions = await LoadProfileAsync("default", cancellationToken);
                defaultPermissions.ModifiedAt = DateTimeOffset.UtcNow;
                _currentPermissions = defaultPermissions;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load default profile, using built-in defaults");
        }
    }

    /// <inheritdoc />
    public async Task SetCurrentPermissionsAsync(ToolPermissions permissions, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(permissions);

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            permissions.ModifiedAt = DateTimeOffset.UtcNow;
            _currentPermissions = permissions.Clone();
            // An explicit set supersedes the lazy default-profile load so a later read can't clobber it.
            _defaultProfileLoaded = true;

            _logger.LogInformation("Updated current permissions to profile: {ProfileName}", permissions.ProfileName);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <inheritdoc />
    public async Task<ToolPermissions> LoadProfileAsync(string profileName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(profileName))
        {
            throw new ArgumentException("Profile name cannot be null or empty.", nameof(profileName));
        }

        var profilePath = GetProfilePath(profileName);

        if (!File.Exists(profilePath))
        {
            throw new InvalidOperationException($"Permission profile '{profileName}' not found.");
        }

        try
        {
            var json = await File.ReadAllTextAsync(profilePath, cancellationToken);
            var permissions = JsonSerializer.Deserialize<ToolPermissions>(json, _jsonOptions);

            if (permissions == null)
            {
                throw new InvalidOperationException($"Failed to deserialize permission profile '{profileName}'.");
            }

            permissions.ProfileName = profileName; // Ensure profile name is set

            _logger.LogInformation("Loaded permission profile: {ProfileName}", profileName);
            return permissions;
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "Failed to load permission profile: {ProfileName}", profileName);
            throw new InvalidOperationException($"Failed to load permission profile '{profileName}': {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public async Task SaveProfileAsync(string profileName, ToolPermissions permissions, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(profileName))
        {
            throw new ArgumentException("Profile name cannot be null or empty.", nameof(profileName));
        }

        ArgumentNullException.ThrowIfNull(permissions);

        var profilePath = GetProfilePath(profileName);

        try
        {
            var permissionsToSave = permissions.Clone(profileName);
            permissionsToSave.ModifiedAt = DateTimeOffset.UtcNow;

            var json = JsonSerializer.Serialize(permissionsToSave, _jsonOptions);
            await File.WriteAllTextAsync(profilePath, json, cancellationToken);

            _logger.LogInformation("Saved permission profile: {ProfileName}", profileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save permission profile: {ProfileName}", profileName);
            throw new InvalidOperationException($"Failed to save permission profile '{profileName}': {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public Task<IList<string>> ListProfilesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var profiles = new List<string>();

            if (Directory.Exists(_profileDirectory))
            {
                var files = Directory.GetFiles(_profileDirectory, "*.json");
                profiles.AddRange(files.Select(f => Path.GetFileNameWithoutExtension(f)).OrderBy(name => name));
            }

            return Task.FromResult<IList<string>>(profiles);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list permission profiles");
            throw new InvalidOperationException($"Failed to list permission profiles: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public Task DeleteProfileAsync(string profileName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(profileName))
        {
            throw new ArgumentException("Profile name cannot be null or empty.", nameof(profileName));
        }

        if (string.Equals(profileName, "default", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Cannot delete the default profile.");
        }

        var profilePath = GetProfilePath(profileName);

        if (!File.Exists(profilePath))
        {
            throw new InvalidOperationException($"Permission profile '{profileName}' not found.");
        }

        try
        {
            File.Delete(profilePath);
            _logger.LogInformation("Deleted permission profile: {ProfileName}", profileName);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete permission profile: {ProfileName}", profileName);
            throw new InvalidOperationException($"Failed to delete permission profile '{profileName}': {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public async Task<ToolPermissions> GetDefaultProfileAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await LoadProfileAsync("default", cancellationToken);
        }
        catch (InvalidOperationException)
        {
            // Default profile doesn't exist, create it
            var defaultPermissions = CreateDefaultPermissions();
            await SaveProfileAsync("default", defaultPermissions, cancellationToken);
            return defaultPermissions;
        }
    }

    /// <inheritdoc />
    public async Task ResetToDefaultAsync(CancellationToken cancellationToken = default)
    {
        var defaultPermissions = await GetDefaultProfileAsync(cancellationToken);
        await SetCurrentPermissionsAsync(defaultPermissions, cancellationToken);

        _logger.LogInformation("Reset current permissions to default profile");
    }

    /// <inheritdoc />
    public Task<bool> ProfileExistsAsync(string profileName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(profileName))
        {
            return Task.FromResult(false);
        }

        var profilePath = GetProfilePath(profileName);
        return Task.FromResult(File.Exists(profilePath));
    }

    private static string GetProfileDirectory()
    {
        var userConfigDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(userConfigDir, ".andy", "permissions");
    }

    // A profile name maps directly onto a file name, so it must be a single, safe path token.
    // Letters, digits, '-', '_' and '.' only; ".." is rejected to block traversal.
    private static readonly System.Text.RegularExpressions.Regex ProfileNameRegex =
        new("^[A-Za-z0-9_.-]+$", System.Text.RegularExpressions.RegexOptions.Compiled);

    private string GetProfilePath(string profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName)
            || profileName.Contains("..", StringComparison.Ordinal)
            || !ProfileNameRegex.IsMatch(profileName))
        {
            throw new ArgumentException(
                $"Invalid profile name '{profileName}'. Allowed characters: letters, digits, '-', '_', '.' (no path separators or '..').",
                nameof(profileName));
        }

        var baseDir = Path.GetFullPath(_profileDirectory);
        var profilePath = Path.GetFullPath(Path.Combine(baseDir, $"{profileName}.json"));

        // Defense in depth: the resolved file must live directly inside the profile directory.
        if (!string.Equals(Path.GetDirectoryName(profilePath), baseDir.TrimEnd(Path.DirectorySeparatorChar), StringComparison.Ordinal))
        {
            throw new ArgumentException($"Invalid profile name '{profileName}'.", nameof(profileName));
        }

        return profilePath;
    }

    private static ToolPermissions CreateDefaultPermissions()
    {
        return new ToolPermissions
        {
            FileSystemAccess = true,
            NetworkAccess = true,
            ProcessExecution = false,
            EnvironmentAccess = true,
            ProfileName = "default",
            Description = "Default permission profile with standard access rights",
            CreatedAt = DateTimeOffset.UtcNow,
            ModifiedAt = DateTimeOffset.UtcNow,
            BlockedPaths = new HashSet<string>
            {
                "~/.ssh",
                "~/.aws",
                "~/.config/gcloud",
                "/etc/passwd",
                "/etc/shadow",
                "/etc/sudoers"
            }
        };
    }
}
