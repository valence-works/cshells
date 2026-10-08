namespace CShells.Nuplane.Internal;

/// <summary>Private bridge shared between the root observer and shell-provider observer aliases.</summary>
internal sealed class CoordinatorHolder(NuplaneRefreshCoordinator coordinator)
{
    public NuplaneRefreshCoordinator Coordinator { get; } = coordinator;
}
