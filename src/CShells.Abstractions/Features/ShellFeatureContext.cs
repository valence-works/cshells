using JetBrains.Annotations;

namespace CShells.Features;

/// <summary>
/// Provides contextual information about the shell and available features during feature instantiation.
/// </summary>
/// <remarks>
/// <para>
/// This context object can be injected into <see cref="IShellFeature"/> constructors to access
/// shell-related metadata that is not available through regular dependency injection.
/// </para>
/// <para>
/// Features can choose to inject either:
/// </para>
/// <list type="bullet">
///   <item>
///     <description><see cref="ShellSettings"/> directly for simple features that only need settings.</description>
///   </item>
///   <item>
///     <description><see cref="ShellFeatureContext"/> for features that need access to additional metadata.</description>
///   </item>
/// </list>
/// <example>
/// <code>
/// // Simple feature - inject ShellSettings directly
/// public class SimpleFeature(ILogger&lt;SimpleFeature&gt; logger, ShellSettings settings) : IShellFeature
/// {
///     public void ConfigureServices(IServiceCollection services) { }
/// }
///
/// // Complex feature - inject ShellFeatureContext
/// public class ComplexFeature(ILogger&lt;ComplexFeature&gt; logger, ShellFeatureContext context) : IShellFeature
/// {
///     public void ConfigureServices(IServiceCollection services)
///     {
///         var allFeatures = context.AllFeatures;
///         var settings = context.Settings;
///         // ...
///     }
/// }
/// </code>
/// </example>
/// </remarks>
[PublicAPI]
public class ShellFeatureContext(ShellSettings settings, IEnumerable<ShellFeatureDescriptor> allFeatures)
{
    /// <summary>
    /// Gets the shell settings for the current shell.
    /// </summary>
    public ShellSettings Settings { get; } = settings;

    /// <summary>
    /// Gets all discovered feature descriptors in the application.
    /// </summary>
    /// <remarks>
    /// This collection includes all features discovered from assemblies, not just the features
    /// enabled for the current shell. Use <see cref="Settings"/>.<see cref="ShellSettings.EnabledFeatures"/>
    /// to determine which features are enabled for this shell.
    /// </remarks>
    public IReadOnlyCollection<ShellFeatureDescriptor> AllFeatures { get; } = allFeatures.ToList();
}
