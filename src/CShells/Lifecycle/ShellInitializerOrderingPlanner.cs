namespace CShells.Lifecycle;

internal sealed class ShellInitializerOrderingPlanner
{
    public InitializerOrderingPlan Plan(
        ShellDescriptor shell,
        IReadOnlyList<IShellInitializer> initializers,
        IReadOnlyList<ShellInitializerRegistration> initializerRegistrations)
    {
        Guard.Against.Null(initializers);
        Guard.Against.Null(initializerRegistrations);

        var metadata = initializerRegistrations
            .Select(r => new LifecycleOrderingPlanner.ComponentMetadata(
                r.InitializerType, r.Phase, r.Order, r.RegistrationIndex, r.IsExplicit, r.Source))
            .ToList();

        var plan = LifecycleOrderingPlanner.Plan(
            shell,
            initializers,
            metadata,
            LifecycleOrderingPlanner.Direction.Startup,
            componentNoun: "initializer",
            errorFactory: static (s, m) => new ShellInitializerOrderException(s, m));

        return new InitializerOrderingPlan(
            [.. plan.Entries.Select(e => new InitializerOrderingEntry(
                e.Component, e.ComponentType, e.Phase, e.Order, e.RegistrationIndex, e.IsExplicit, e.Source))],
            [.. plan.Diagnostics.Select(d => new InitializerOrderingDiagnostic(d.Message, d.ComponentTypes))]);
    }

    internal sealed record InitializerOrderingPlan(
        IReadOnlyList<InitializerOrderingEntry> Entries,
        IReadOnlyList<InitializerOrderingDiagnostic> Diagnostics);

    internal sealed record InitializerOrderingEntry(
        IShellInitializer Initializer,
        Type InitializerType,
        LifecyclePhase Phase,
        int Order,
        int RegistrationIndex,
        bool IsExplicit,
        string Source);

    internal sealed record InitializerOrderingDiagnostic(
        string Message,
        IReadOnlyList<Type> InitializerTypes);
}
