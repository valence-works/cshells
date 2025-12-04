using Microsoft.Extensions.DependencyInjection;

namespace CShells.Features;

/// <summary>
/// Defines the contract for a shell feature.
/// </summary>
/// <remarks>
/// <para>
/// Implementations of <see cref="IShellFeature"/> are instantiated using the application's
/// root <see cref="IServiceProvider"/> (via <see cref="ActivatorUtilities.CreateInstance"/>),
/// NOT from a shell-scoped provider. This means:
/// </para>
/// <list type="bullet">
///   <item>
///     <description>
///     Constructors may only depend on root-level services (e.g., logging, configuration, options)
///     and optionally <see cref="ShellSettings"/> which is passed explicitly during activation.
///     </description>
///   </item>
///   <item>
///     <description>
///     Constructors must NOT depend on services registered by other <see cref="IShellFeature"/>
///     implementations in their <see cref="ConfigureServices"/> methods.
///     </description>
///   </item>
///   <item>
///     <description>
///     All shell-scoped services must be registered inside <see cref="ConfigureServices"/>,
///     not consumed by the feature's constructor.
///     </description>
///   </item>
/// </list>
/// </remarks>
public interface IShellFeature
{
    /// <summary>
    /// Configures the services for the shell feature.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <remarks>
    /// This method is called in topological order based on feature dependencies.
    /// The order ensures that dependencies are configured before dependents,
    /// but this is purely for registration order - features should not consume
    /// services from other features during configuration.
    /// If a feature needs access to <see cref="ShellSettings"/>, it should inject
    /// it via its constructor.
    /// </remarks>
    void ConfigureServices(IServiceCollection services);
}
