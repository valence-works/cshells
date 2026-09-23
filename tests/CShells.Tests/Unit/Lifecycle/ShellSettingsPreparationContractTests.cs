using CShells.Lifecycle;

namespace CShells.Tests.Unit.Lifecycle;

public class ShellSettingsPreparationContractTests
{
    [Fact(DisplayName = "Preparation context owns its configuration and feature graph snapshots")]
    public void Context_SourceCollectionsChange_PreservesSnapshot()
    {
        var configuration = new Dictionary<string, string?> { ["Feature:Value"] = "authored" };
        var dependencies = new[] { "Dependency" };
        var descriptor = new ShellFeaturePreparationDescriptor("Feature", dependencies, null, true);
        var orderedFeatures = new[] { descriptor };
        var enabled = new[] { "Dependency", "Feature" };
        var requested = new[] { "Feature", "Unknown" };
        var implicitFeatures = new[] { "Dependency" };
        var unknown = new[] { "Unknown" };
        var disabled = new[] { "Disabled" };
        var resets = new[] { "Reset" };
        var context = new ShellSettingsPreparationContext("shell", configuration, enabled, disabled,
            resets, orderedFeatures, requested, implicitFeatures, unknown);

        configuration["Feature:Value"] = "changed";
        dependencies[0] = "ChangedDependency";
        orderedFeatures[0] = new ShellFeaturePreparationDescriptor("Replacement", [], null, false);
        foreach (var ids in new[] { enabled, requested, implicitFeatures, unknown, disabled, resets })
            ids[0] = "Changed";

        Assert.Equal("authored", context.ConfigurationData["feature:value"]);
        Assert.Same(descriptor, Assert.Single(context.OrderedFeatures));
        Assert.Equal("Dependency", Assert.Single(descriptor.Dependencies));
        Assert.Equal(["Dependency", "Feature"], context.EnabledFeatureIds);
        Assert.Equal(["Feature", "Unknown"], context.RequestedFeatureIds);
        Assert.Equal("Dependency", Assert.Single(context.ImplicitFeatureIds));
        Assert.Equal("Unknown", Assert.Single(context.UnknownFeatureIds));
        Assert.Equal("Disabled", Assert.Single(context.DisabledFeatureIds));
        Assert.Equal("Reset", Assert.Single(context.FeatureSettingResetIds));
        Assert.Throws<NotSupportedException>(() =>
            ((IDictionary<string, string?>)context.ConfigurationData)["Feature:Value"] = "mutated");
        Assert.Throws<NotSupportedException>(() => ((IList<string>)descriptor.Dependencies)[0] = "mutated");
        Assert.Throws<NotSupportedException>(() => ((IList<string>)context.EnabledFeatureIds)[0] = "mutated");
    }

    [Fact(DisplayName = "Preparation result owns an immutable patch including removals")]
    public void Result_SourcePatchChanges_PreservesRequestedChanges()
    {
        var changes = new Dictionary<string, string?>
        {
            ["Feature:Value"] = "prepared",
            ["Feature:Remove"] = null
        };
        var result = new ShellSettingsPreparationResult(changes);

        changes.Clear();

        Assert.Equal("prepared", result.ConfigurationData["feature:value"]);
        Assert.True(result.ConfigurationData.ContainsKey("feature:remove"));
        Assert.Null(result.ConfigurationData["Feature:Remove"]);
        Assert.Throws<NotSupportedException>(() =>
            ((IDictionary<string, string?>)result.ConfigurationData)["Feature:Value"] = "mutated");
    }
}
