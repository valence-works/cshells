namespace CShells.Management;

/// <summary>
/// Provides methods for managing shells at runtime.
/// Supports adding, removing, and updating shells without requiring application restart.
/// </summary>
public interface IShellManager
{
    /// <summary>
    /// Adds a new shell to the system.
    /// </summary>
    /// <param name="settings">The shell settings.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the shell has been added and activated.</returns>
    /// <remarks>
    /// This method will:
    /// <list type="number">
    /// <item>Add the shell settings to the cache</item>
    /// <item>Build the shell context (service provider)</item>
    /// <item>Register the shell's endpoints (for web features)</item>
    /// </list>
    /// </remarks>
    Task AddShellAsync(ShellSettings settings, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a shell from the system.
    /// </summary>
    /// <param name="shellId">The ID of the shell to remove.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the shell has been removed.</returns>
    /// <remarks>
    /// This method will:
    /// <list type="number">
    /// <item>Remove the shell's endpoints</item>
    /// <item>Dispose the shell's service provider</item>
    /// <item>Remove the shell settings from the cache</item>
    /// </list>
    /// </remarks>
    Task RemoveShellAsync(ShellId shellId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing shell's configuration.
    /// </summary>
    /// <param name="settings">The updated shell settings.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the shell has been updated.</returns>
    /// <remarks>
    /// This method will remove the existing shell and add the updated version,
    /// which may cause a brief interruption in request processing for that shell.
    /// </remarks>
    Task UpdateShellAsync(ShellSettings settings, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reloads all shells from the configured shell settings provider.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when all shells have been reloaded.</returns>
    /// <remarks>
    /// This is useful for refreshing shells from external storage (e.g., database, blob storage)
    /// without restarting the application.
    /// </remarks>
    Task ReloadAllShellsAsync(CancellationToken cancellationToken = default);
}
