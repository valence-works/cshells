namespace CShells.Lifecycle;

/// <summary>
/// Prepares scalar configuration after global defaults and feature dependencies have been resolved,
/// but before any feature is constructed or configured.
/// </summary>
/// <remarks>
/// CShells invokes at most one preparer per shell-generation build. The preparer returns a scalar
/// set/remove patch; the shell identity, effective feature set, dependency order, and code-first
/// configurators are framework-owned.
/// </remarks>
public interface IShellSettingsPreparer
{
    /// <summary>Prepares the immutable pre-binding shell view.</summary>
    /// <param name="context">The final shell composition before feature side effects.</param>
    /// <param name="cancellationToken">The cancellation token for the current shell build.</param>
    /// <returns>The scalar configuration patch to apply before feature binding.</returns>
    Task<ShellSettingsPreparationResult> PrepareAsync(
        ShellSettingsPreparationContext context,
        CancellationToken cancellationToken = default);
}
