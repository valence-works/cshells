using System.Collections.ObjectModel;

namespace CShells.Lifecycle;

/// <summary>
/// Immutable view of a shell's final feature composition immediately before feature construction.
/// </summary>
public sealed class ShellSettingsPreparationContext
{
    /// <summary>Creates a detached view of the composed shell settings and resolved feature graph.</summary>
    /// <param name="shellId">The shell identity.</param>
    /// <param name="configurationData">Scalar values from the composed shell configuration map.</param>
    /// <param name="enabledFeatureIds">Available feature IDs in dependency order.</param>
    /// <param name="disabledFeatureIds">Explicitly disabled IDs.</param>
    /// <param name="featureSettingResetIds">IDs with reset settings.</param>
    /// <param name="orderedFeatures">Descriptors for the ordered available features.</param>
    /// <param name="requestedFeatureIds">IDs requested before dependency expansion, including unknown IDs.</param>
    /// <param name="implicitFeatureIds">IDs added by dependency expansion.</param>
    /// <param name="unknownFeatureIds">Requested IDs absent from the runtime catalog.</param>
    public ShellSettingsPreparationContext(
        ShellId shellId,
        IReadOnlyDictionary<string, string?> configurationData,
        IReadOnlyList<string> enabledFeatureIds,
        IReadOnlyList<string> disabledFeatureIds,
        IReadOnlyList<string> featureSettingResetIds,
        IReadOnlyList<ShellFeaturePreparationDescriptor> orderedFeatures,
        IReadOnlyList<string> requestedFeatureIds,
        IReadOnlyList<string> implicitFeatureIds,
        IReadOnlyList<string> unknownFeatureIds)
    {
        Guard.Against.NullOrWhiteSpace(shellId.Name);
        ShellId = shellId;
        ConfigurationData = SnapshotDictionary(configurationData);
        EnabledFeatureIds = SnapshotList(enabledFeatureIds);
        DisabledFeatureIds = SnapshotList(disabledFeatureIds);
        FeatureSettingResetIds = SnapshotList(featureSettingResetIds);
        OrderedFeatures = SnapshotList(orderedFeatures);
        RequestedFeatureIds = SnapshotList(requestedFeatureIds);
        ImplicitFeatureIds = SnapshotList(implicitFeatureIds);
        UnknownFeatureIds = SnapshotList(unknownFeatureIds);
    }

    /// <summary>Gets the shell identity.</summary>
    public ShellId ShellId { get; }

    /// <summary>
    /// Gets the scalar projection of the composed shell configuration map.
    /// Values use the same <c>ToString()</c> conversion as shell configuration binding; root
    /// <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> fallback values are not included.
    /// The dictionary is an immutable snapshot; mutating the source settings cannot change it.
    /// </summary>
    public IReadOnlyDictionary<string, string?> ConfigurationData { get; }

    /// <summary>Gets the ordered, available features after dependency expansion.</summary>
    public IReadOnlyList<ShellFeaturePreparationDescriptor> OrderedFeatures { get; }

    /// <summary>Gets the feature IDs after dependency expansion.</summary>
    public IReadOnlyList<string> EnabledFeatureIds { get; }

    /// <summary>Gets the explicitly disabled feature IDs.</summary>
    public IReadOnlyList<string> DisabledFeatureIds { get; }

    /// <summary>Gets the feature IDs whose lower-priority settings were reset.</summary>
    public IReadOnlyList<string> FeatureSettingResetIds { get; }

    /// <summary>Gets the feature IDs originally requested by the composed shell settings.</summary>
    public IReadOnlyList<string> RequestedFeatureIds { get; }

    /// <summary>Gets the feature IDs added by dependency expansion.</summary>
    public IReadOnlyList<string> ImplicitFeatureIds { get; }

    /// <summary>Gets requested feature IDs that were not available in the runtime catalog.</summary>
    public IReadOnlyList<string> UnknownFeatureIds { get; }

    private static IReadOnlyDictionary<string, string?> SnapshotDictionary(IReadOnlyDictionary<string, string?> values)
    {
        Guard.Against.Null(values);
        return new ReadOnlyDictionary<string, string?>(
            values.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<T> SnapshotList<T>(IReadOnlyList<T> values)
    {
        Guard.Against.Null(values);
        return new ReadOnlyCollection<T>(values.ToArray());
    }
}

/// <summary>
/// Immutable descriptor for an ordered feature during shell settings preparation.
/// </summary>
public sealed class ShellFeaturePreparationDescriptor
{
    /// <summary>Creates an immutable descriptor without exposing feature configurator delegates.</summary>
    /// <param name="id">The stable feature ID.</param>
    /// <param name="dependencies">Declared dependency IDs.</param>
    /// <param name="startupType">The discovered startup type, when present.</param>
    /// <param name="hasConfigurator">Whether a code-first configurator will run after binding.</param>
    public ShellFeaturePreparationDescriptor(
        string id,
        IReadOnlyList<string> dependencies,
        Type? startupType,
        bool hasConfigurator)
    {
        Id = Guard.Against.NullOrWhiteSpace(id);
        Dependencies = new ReadOnlyCollection<string>(Guard.Against.Null(dependencies).ToArray());
        StartupType = startupType;
        HasConfigurator = hasConfigurator;
    }

    /// <summary>Gets the feature identity.</summary>
    public string Id { get; }

    /// <summary>Gets the feature's declared dependencies.</summary>
    public IReadOnlyList<string> Dependencies { get; }

    /// <summary>Gets the feature startup type, when one was discovered.</summary>
    public Type? StartupType { get; }

    /// <summary>Gets whether a code-first configurator is registered for this feature.</summary>
    public bool HasConfigurator { get; }
}
