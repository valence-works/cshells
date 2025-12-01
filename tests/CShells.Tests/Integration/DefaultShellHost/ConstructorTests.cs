using CShells.Configuration;
using CShells.Hosting;
using CShells.Tests.TestHelpers;

namespace CShells.Tests.Integration.DefaultShellHost;

/// <summary>
/// Tests for <see cref="DefaultShellHost"/> constructor validation.
/// </summary>
[Collection(nameof(DefaultShellHostCollection))]
public class ConstructorTests(DefaultShellHostFixture fixture)
{
    [Theory(DisplayName = "Constructor guard clauses throw ArgumentNullException")]
    [InlineData(true, false, false, false, false, "shellSettingsCache")]
    [InlineData(false, true, false, false, false, "assemblies")]
    [InlineData(false, false, true, false, false, "rootProvider")]
    [InlineData(false, false, false, true, false, "rootServicesAccessor")]
    [InlineData(false, false, false, false, true, "featureFactory")]
    public void Constructor_GuardClauses_ThrowArgumentNullException(
        bool nullCache,
        bool nullAssemblies,
        bool nullRootProvider,
        bool nullAccessor,
        bool nullFactory,
        string expectedParam)
    {
        var cache = nullCache ? null : new ShellSettingsCache();
        var assemblies = nullAssemblies ? null : Array.Empty<System.Reflection.Assembly>();
        var rootProvider = nullRootProvider ? null : fixture.RootProvider;
        var accessor = nullAccessor ? null : fixture.RootAccessor;
        var factory = nullFactory ? null : fixture.FeatureFactory;

        var exception = Assert.ThrowsAny<ArgumentException>(() =>
            new Hosting.DefaultShellHost(cache!, assemblies!, rootProvider!, accessor!, factory!));
        Assert.Equal(expectedParam, exception.ParamName);
    }
}
