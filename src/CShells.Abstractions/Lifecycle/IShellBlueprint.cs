namespace CShells.Lifecycle;

/// <summary>
/// A re-invocable recipe for composing a single named shell's <see cref="ShellSettings"/>.
/// </summary>
/// <remarks>
/// Blueprints hold no runtime state. Each blueprint is vended by an
/// <see cref="IShellBlueprintProvider"/> — typically the built-in in-memory provider populated
/// via <c>CShellsBuilder.AddShell</c>, the <see cref="Microsoft.Extensions.Configuration.IConfiguration"/>-backed provider, or a
/// storage-backed provider. The registry invokes <see cref="ComposeAsync"/> on every
/// activation and reload.
/// </remarks>
public interface IShellBlueprint
{
    /// <summary>The shell name this blueprint produces settings for.</summary>
    string Name { get; }

    /// <summary>
    /// Static metadata copied onto every generation's <see cref="ShellDescriptor.Metadata"/>.
    /// May be empty; never null.
    /// </summary>
    IReadOnlyDictionary<string, string> Metadata { get; }

    /// <summary>
    /// Composes a fresh <see cref="ShellSettings"/> for this shell name. Must be re-invocable
    /// and side-effect-free.
    /// </summary>
    /// <returns>
    /// A <see cref="ShellSettings"/> whose <c>Id.Name</c> equals <see cref="Name"/>. The
    /// registry validates this.
    /// </returns>
    Task<ShellSettings> ComposeAsync(CancellationToken cancellationToken = default);
}
