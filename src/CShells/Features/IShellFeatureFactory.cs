namespace CShells.Features;

/// <summary>
/// Factory for creating shell feature instances with proper dependency injection.
/// </summary>
public interface IShellFeatureFactory
{
    /// <summary>
    /// Creates an instance of the specified feature type.
    /// </summary>
    /// <typeparam name="T">The feature interface type (e.g., <see cref="IShellFeature"/> or <see cref="IWebShellFeature"/>).</typeparam>
    /// <param name="featureType">The concrete feature type to instantiate.</param>
    /// <param name="shellSettings">Optional shell settings to pass to the feature constructor.</param>
    /// <param name="featureContext">Optional feature context to pass to the feature constructor.</param>
    /// <returns>The instantiated feature.</returns>
    /// <remarks>
    /// <para>
    /// This method intelligently handles feature instantiation by:
    /// </para>
    /// <list type="bullet">
    ///   <item>
    ///     <description>Resolving dependencies from the root service provider</description>
    ///   </item>
    ///   <item>
    ///     <description>Automatically passing <paramref name="shellSettings"/> if the constructor accepts it</description>
    ///   </item>
    ///   <item>
    ///     <description>Automatically passing <paramref name="featureContext"/> if the constructor accepts it</description>
    ///   </item>
    ///   <item>
    ///     <description>Supporting features with or without <see cref="ShellSettings"/> or <see cref="ShellFeatureContext"/> dependencies</description>
    ///   </item>
    /// </list>
    /// </remarks>
    T CreateFeature<T>(Type featureType, ShellSettings? shellSettings = null, ShellFeatureContext? featureContext = null) where T : class;
}
