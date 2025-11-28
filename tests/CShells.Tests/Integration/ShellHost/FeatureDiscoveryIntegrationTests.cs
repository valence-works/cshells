namespace CShells.Tests.Integration.ShellHost;

/// <summary>
/// Integration tests for feature discovery using real feature startup classes.
/// </summary>
public class FeatureDiscoveryIntegrationTests
{
    [Fact(DisplayName = "FeatureDiscovery discovers features from test assembly")]
    public void FeatureDiscovery_DiscoversFeaturesFromTestAssembly()
    {
        // Arrange
        var assembly = typeof(TestFixtures).Assembly;

        // Act
        var features = CShells.FeatureDiscovery.DiscoverFeatures([assembly]).ToList();

        // Assert
        Assert.Contains(features, f => f.Id == "Core");
        Assert.Contains(features, f => f.Id == "Weather");

        var weatherFeature = features.Single(f => f.Id == "Weather");
        Assert.Contains("Core", weatherFeature.Dependencies);
    }

    [Fact(DisplayName = "FeatureDiscovery Core feature has correct startup type")]
    public void FeatureDiscovery_CoreFeature_HasCorrectStartupType()
    {
        // Arrange
        var assembly = typeof(TestFixtures).Assembly;

        // Act
        var features = CShells.FeatureDiscovery.DiscoverFeatures([assembly]).ToList();

        // Assert
        var coreFeature = features.Single(f => f.Id == "Core");
        Assert.Equal(typeof(TestFixtures.CoreFeatureStartup), coreFeature.StartupType);
    }

    [Fact(DisplayName = "FeatureDiscovery Weather feature has dependency on Core")]
    public void FeatureDiscovery_WeatherFeature_HasDependencyOnCore()
    {
        // Arrange
        var assembly = typeof(TestFixtures).Assembly;

        // Act
        var features = CShells.FeatureDiscovery.DiscoverFeatures([assembly]).ToList();

        // Assert
        var weatherFeature = features.Single(f => f.Id == "Weather");
        Assert.Equal(typeof(TestFixtures.WeatherFeatureStartup), weatherFeature.StartupType);
        Assert.Single(weatherFeature.Dependencies);
        Assert.Equal("Core", weatherFeature.Dependencies[0]);
    }
}
