namespace CShells.Configuration;

/// <summary>
/// Provides cached, synchronous access to shell settings loaded from an <see cref="IShellSettingsProvider"/>.
/// This cache is populated at startup and provides a consistent view of shells for runtime resolution.
/// </summary>
public interface IShellSettingsCache
{
    /// <summary>
    /// Gets all cached shell settings.
    /// </summary>
    /// <returns>A read-only collection of shell settings.</returns>
    IReadOnlyCollection<ShellSettings> GetAll();

    /// <summary>
    /// Gets a shell setting by its identifier.
    /// </summary>
    /// <param name="id">The shell identifier.</param>
    /// <returns>The shell settings if found; otherwise, null.</returns>
    ShellSettings? GetById(ShellId id);
}
