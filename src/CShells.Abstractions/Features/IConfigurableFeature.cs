namespace CShells.Features;

/// <summary>
/// Marker interface for features that can be configured from IConfiguration.
/// Features implementing this interface will have their Configure method called automatically
/// after the options are bound from configuration.
/// </summary>
/// <typeparam name="TOptions">The type of options to bind from configuration.</typeparam>
/// <remarks>
/// A feature can implement multiple IConfigurableFeature&lt;T&gt; interfaces to bind multiple configuration sections.
/// The configuration section name is determined by:
/// 1. The feature name (from [ShellFeature] attribute or type name)
/// 2. For nested options, the options type name (minus "Options" suffix if present)
/// </remarks>
public interface IConfigurableFeature<in TOptions> : IShellFeature where TOptions : class
{
    /// <summary>
    /// Configures the feature with the bound options from configuration.
    /// This method is called automatically after options are bound from IConfiguration.
    /// </summary>
    /// <param name="options">The options instance bound from configuration.</param>
    void Configure(TOptions options);
}
