using CShells.Configuration;
using CShells.Hosting;
using CShells.Tests.Integration.ShellHost;
using CShells.Tests.TestHelpers;

namespace CShells.Tests.Integration.DefaultShellHost;

/// <summary>
/// Tests for <see cref="DefaultShellHost"/> constructor validation.
/// </summary>
[Collection(nameof(DefaultShellHostCollection))]
public class ConstructorTests(DefaultShellHostFixture fixture)
{
    [Theory(DisplayName = "Constructor guard clauses throw ArgumentNullException")]
    [InlineData(true, false, false, false, false, false, "shellSettingsCache")]
    [InlineData(false, true, false, false, false, false, "assemblies")]
    [InlineData(false, false, true, false, false, false, "rootProvider")]
    [InlineData(false, false, false, true, false, false, "rootServicesAccessor")]
    [InlineData(false, false, false, false, true, false, "featureFactory")]
    [InlineData(false, false, false, false, false, true, "exclusionRegistry")]
    public void Constructor_GuardClauses_ThrowArgumentNullException(
        bool nullCache,
        bool nullAssemblies,
        bool nullRootProvider,
        bool nullAccessor,
        bool nullFactory,
        bool nullExclusionRegistry,
        string expectedParam)
    {
        var cache = nullCache ? null : new ShellSettingsCache();
        var assemblies = nullAssemblies ? null : Array.Empty<System.Reflection.Assembly>();
        var rootProvider = nullRootProvider ? null : fixture.RootProvider;
        var accessor = nullAccessor ? null : fixture.RootAccessor;
        var factory = nullFactory ? null : fixture.FeatureFactory;
        var exclusionRegistry = nullExclusionRegistry ? null : new ShellServiceExclusionRegistry([]);

        var exception = Assert.ThrowsAny<ArgumentException>(() =>
            new Hosting.DefaultShellHost(cache!, assemblies!, rootProvider!, accessor!, factory!, exclusionRegistry!));
        Assert.Equal(expectedParam, exception.ParamName);
    }
}
