using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Andy.Tools.Core;

/// <summary>
/// Interface for managing tool permission profiles.
/// </summary>
public interface IPermissionProfileService
{
    /// <summary>
    /// Gets the current active permission profile.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The current active permission profile.</returns>
    public Task<ToolPermissions> GetCurrentPermissionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the current active permission profile.
    /// </summary>
    /// <param name="permissions">The permissions to set as active.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the operation.</returns>
    public Task SetCurrentPermissionsAsync(ToolPermissions permissions, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a permission profile by name.
    /// </summary>
    /// <param name="profileName">The name of the profile to load.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The loaded permission profile.</returns>
    /// <exception cref="ArgumentException">Thrown when the profile name is null or empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the profile is not found.</exception>
    public Task<ToolPermissions> LoadProfileAsync(string profileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a permission profile with the given name.
    /// </summary>
    /// <param name="profileName">The name of the profile to save.</param>
    /// <param name="permissions">The permissions to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the operation.</returns>
    /// <exception cref="ArgumentException">Thrown when the profile name is null or empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown when permissions is null.</exception>
    public Task SaveProfileAsync(string profileName, ToolPermissions permissions, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all available permission profiles.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of available profile names.</returns>
    public Task<IList<string>> ListProfilesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a permission profile.
    /// </summary>
    /// <param name="profileName">The name of the profile to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the operation.</returns>
    /// <exception cref="ArgumentException">Thrown when the profile name is null or empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the profile is not found or cannot be deleted.</exception>
    public Task DeleteProfileAsync(string profileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the default permission profile.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The default permission profile.</returns>
    public Task<ToolPermissions> GetDefaultProfileAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets the current permissions to the default profile.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the operation.</returns>
    public Task ResetToDefaultAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a permission profile exists.
    /// </summary>
    /// <param name="profileName">The name of the profile to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the profile exists, false otherwise.</returns>
    public Task<bool> ProfileExistsAsync(string profileName, CancellationToken cancellationToken = default);
}
