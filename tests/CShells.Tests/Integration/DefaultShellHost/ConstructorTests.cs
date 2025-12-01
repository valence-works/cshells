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
    public static object?[][] GuardClauseData() =>
    [
        [null, Array.Empty<System.Reflection.Assembly>(), "shellSettingsCache"],
        [new ShellSettingsCache(), null, "assemblies"]
    ];

    [Theory(DisplayName = "Constructor guard clauses throw ArgumentNullException")]
    [MemberData(nameof(GuardClauseData))]
    public void Constructor_GuardClauses_ThrowArgumentNullException(ShellSettingsCache? cache, IEnumerable<System.Reflection.Assembly>? assemblies, string expectedParam)
    {
        var exception = Assert.ThrowsAny<ArgumentException>(() => new Hosting.DefaultShellHost(cache!, assemblies!, fixture.RootProvider, fixture.RootAccessor, fixture.FeatureFactory));
        Assert.Equal(expectedParam, exception.ParamName);
    }
}
