namespace CShells.Lifecycle;

/// <summary>
/// Semantic phase used to order shell lifecycle components.
/// </summary>
/// <remarks>
/// Initializer execution is ordered by phase first, then by numeric order within the phase.
/// <see cref="IShellTerminator"/> instances execute phases mirror-reversed
/// (<see cref="Start"/> first, <see cref="Prepare"/> last, descending order within a phase).
/// Existing unordered <see cref="IShellInitializer"/> and <see cref="IShellTerminator"/>
/// registrations run in <see cref="Default"/> to preserve compatibility.
/// </remarks>
public enum LifecyclePhase
{
    /// <summary>Preparation work that must complete before default startup work.</summary>
    Prepare = 0,

    /// <summary>Compatibility phase for unordered initializers.</summary>
    Default = 1000,

    /// <summary>Runtime startup work that should run after preparation and default work.</summary>
    Start = 2000
}
