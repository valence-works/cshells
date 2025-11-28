using FluentAssertions;

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
        var features = FeatureDiscovery.DiscoverFeatures([assembly]).ToList();

        // Assert
        features.Should().Contain(f => f.Id == "Core");
        features.Should().Contain(f => f.Id == "Weather");

        var weatherFeature = features.Single(f => f.Id == "Weather");
        weatherFeature.Dependencies.Should().Contain("Core");
    }

    [Fact(DisplayName = "FeatureDiscovery Core feature has correct startup type")]
    public void FeatureDiscovery_CoreFeature_HasCorrectStartupType()
    {
        // Arrange
        var assembly = typeof(TestFixtures).Assembly;

        // Act
        var features = FeatureDiscovery.DiscoverFeatures([assembly]).ToList();

        // Assert
        var coreFeature = features.Single(f => f.Id == "Core");
        coreFeature.StartupType.Should().Be(typeof(TestFixtures.CoreFeatureStartup));
    }

    [Fact(DisplayName = "FeatureDiscovery Weather feature has dependency on Core")]
    public void FeatureDiscovery_WeatherFeature_HasDependencyOnCore()
    {
        // Arrange
        var assembly = typeof(TestFixtures).Assembly;

        // Act
        var features = FeatureDiscovery.DiscoverFeatures([assembly]).ToList();

        // Assert
        var weatherFeature = features.Single(f => f.Id == "Weather");
        weatherFeature.StartupType.Should().Be(typeof(TestFixtures.WeatherFeatureStartup));
        weatherFeature.Dependencies.Should().ContainSingle().Which.Should().Be("Core");
    }
}
