using CShells.Features;

namespace CShells.FastEndpoints.Features;

/// <summary>
/// Marker interface for shell features that contain FastEndpoints.
/// </summary>
/// <remarks>
/// <para>
/// Features implementing this interface indicate that their assembly contains FastEndpoints
/// that should be automatically discovered and registered with shell-scoped routing.
/// </para>
/// <para>
/// The assembly containing the feature's startup type will be scanned for FastEndpoints.
/// All discovered endpoints will be prefixed with the shell's route prefix if configured.
/// </para>
/// <para>
/// This is a pure marker interface with no methods - the feature's assembly is automatically
/// inferred from its <see cref="IShellFeature"/> implementation type.
/// </para>
/// </remarks>
public interface IFastEndpointsShellFeature : IShellFeature
{
    // Marker interface - no methods needed
    // FastEndpoints will be discovered from the assembly containing the feature's startup type
}
