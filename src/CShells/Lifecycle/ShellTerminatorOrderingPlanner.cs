namespace CShells.Lifecycle;

internal sealed class ShellTerminatorOrderingPlanner
{
    public TerminatorOrderingPlan Plan(
        ShellDescriptor shell,
        IReadOnlyList<IShellTerminator> terminators,
        IReadOnlyList<ShellTerminatorRegistration> terminatorRegistrations)
    {
        Guard.Against.Null(terminators);
        Guard.Against.Null(terminatorRegistrations);

        var metadata = terminatorRegistrations
            .Select(r => new LifecycleOrderingPlanner.ComponentMetadata(
                r.TerminatorType, r.Phase, r.Order, r.RegistrationIndex, r.IsExplicit, r.Source))
            .ToList();

        var plan = LifecycleOrderingPlanner.Plan(
            shell,
            terminators,
            metadata,
            LifecycleOrderingPlanner.Direction.Teardown,
            componentNoun: "terminator",
            errorFactory: static (s, m) => new ShellTerminatorOrderException(s, m));

        return new TerminatorOrderingPlan(
            [.. plan.Entries.Select(e => new TerminatorOrderingEntry(
                e.Component, e.ComponentType, e.Phase, e.Order, e.RegistrationIndex, e.IsExplicit, e.Source))],
            [.. plan.Diagnostics.Select(d => new TerminatorOrderingDiagnostic(d.Message, d.ComponentTypes))]);
    }

    internal sealed record TerminatorOrderingPlan(
        IReadOnlyList<TerminatorOrderingEntry> Entries,
        IReadOnlyList<TerminatorOrderingDiagnostic> Diagnostics);

    internal sealed record TerminatorOrderingEntry(
        IShellTerminator Terminator,
        Type TerminatorType,
        LifecyclePhase Phase,
        int Order,
        int RegistrationIndex,
        bool IsExplicit,
        string Source);

    internal sealed record TerminatorOrderingDiagnostic(
        string Message,
        IReadOnlyList<Type> TerminatorTypes);
}
