using CShells.DependencyInjection;
using CShells.Features;
using CShells.Lifecycle;

namespace CShells.Hosting;

/// <summary>
/// Default implementation of <see cref="IShellServiceExclusionProvider"/> — the set of root
/// service types whose descriptors must NOT be copied into shell service collections.
/// </summary>
/// <remarks>
/// <para>
/// Excluding a descriptor prevents the root's singleton factory from being re-invoked inside a
/// shell scope (which would create a new instance per shell and cascade through dependencies
/// that aren't present in the shell — e.g., <see cref="IRootServiceCollectionAccessor"/>).
/// </para>
/// <para>
/// The shell-facing <see cref="IShellRegistry"/> is deliberately excluded here AND re-registered
/// in <c>ShellProviderBuilder.RegisterCoreServices</c> as a factory that returns the root
/// singleton. That delegation makes the shell's registry resolution alias back to the same
/// registry instance the host uses, without triggering the root-only construction path.
/// </para>
/// </remarks>
public class DefaultShellServiceExclusionProvider : IShellServiceExclusionProvider
{
    /// <inheritdoc />
    public IEnumerable<Type> GetExcludedServiceTypes()
    {
        // Build-time infrastructure — shells must never observe these directly.
        yield return typeof(IRootServiceCollectionAccessor);
        yield return typeof(IReadOnlyCollection<ShellSettings>);

        // Root-only lifecycle infrastructure — re-registered as root delegations where shells
        // legitimately need access (currently: IShellRegistry only).
        yield return typeof(IShellRegistry);
        yield return typeof(Lifecycle.ShellRegistry);
        yield return typeof(Lifecycle.ShellProviderBuilder);
        yield return typeof(IDrainPolicy);
        yield return typeof(DrainGracePeriod);
        yield return typeof(Lifecycle.ShellLifecycleLogger);
        yield return typeof(IShellLifecycleSubscriber);
        yield return typeof(IShellBlueprint);
        yield return typeof(RuntimeFeatureCatalog);
        // Excluded here AND re-registered as a root delegation in ShellProviderBuilder so shells can read the
        // catalog without copying the root-only assembly-resolver factory.
        yield return typeof(IRuntimeFeatureCatalog);
        yield return typeof(IShellFeatureFactory);
        yield return typeof(IShellServiceExclusionRegistry);
        yield return typeof(IShellServiceExclusionProvider);
    }
}
