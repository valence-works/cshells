namespace CShells.Features;

/// <summary>
/// Marker interface indicating that a shell feature infers its dependencies from the specified feature type.
/// When a feature implements this interface, the shell system will automatically include the dependencies
/// of the specified base feature as dependencies of this feature.
/// </summary>
/// <typeparam name="TBaseFeature">The feature type from which to infer dependencies.</typeparam>
public interface IInfersDependenciesFrom<TBaseFeature> where TBaseFeature : IShellFeature
{
}
