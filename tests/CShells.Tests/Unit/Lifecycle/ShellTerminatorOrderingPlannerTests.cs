using CShells.Lifecycle;
using Microsoft.Extensions.DependencyInjection;

namespace CShells.Tests.Unit.Lifecycle;

public class ShellTerminatorOrderingPlannerTests
{
    private static readonly ShellDescriptor Shell = ShellDescriptor.Create("test", 1);
    private readonly ShellTerminatorOrderingPlanner planner = new();

    [Fact(DisplayName = "Planner orders terminators mirror-reversed: Start phase first, Prepare last, descending order within a phase")]
    public void Plan_PhaseOrderAndNumericOrder_ReturnsMirrorReversedSequence()
    {
        var prepareLate = new PrepareLateTerminator();
        var start = new StartTerminator();
        var prepareEarly = new PrepareEarlyTerminator();
        var defaultTerminator = new DefaultTerminator();

        var plan = planner.Plan(
            Shell,
            [start, defaultTerminator, prepareLate, prepareEarly],
            [
                Registration<PrepareLateTerminator>(LifecyclePhase.Prepare, 20),
                Registration<StartTerminator>(LifecyclePhase.Start, 0),
                Registration<PrepareEarlyTerminator>(LifecyclePhase.Prepare, 10),
                Registration<DefaultTerminator>(LifecyclePhase.Default, 0)
            ]);

        Assert.Equal(
            [
                typeof(StartTerminator),
                typeof(DefaultTerminator),
                typeof(PrepareLateTerminator),
                typeof(PrepareEarlyTerminator)
            ],
            plan.Entries.Select(e => e.TerminatorType));
    }

    [Fact(DisplayName = "Planner maps unordered terminators to Default phase and reverses registration order")]
    public void Plan_UnorderedTerminators_UseDefaultPhaseAndReverseRegistrationOrder()
    {
        var first = new FirstTerminator();
        var second = new SecondTerminator();

        var plan = planner.Plan(Shell, [first, second], []);

        Assert.Equal([typeof(SecondTerminator), typeof(FirstTerminator)], plan.Entries.Select(e => e.TerminatorType));
        Assert.All(plan.Entries, e => Assert.Equal(LifecyclePhase.Default, e.Phase));
        Assert.Empty(plan.Diagnostics);
    }

    [Fact(DisplayName = "Planner applies attribute metadata for legacy registrations, teardown-ordered")]
    public void Plan_AttributeMetadata_OrdersLegacyRegistrationsMirrorReversed()
    {
        var attributedStart = new AttributedStartTerminator();
        var attributedPrepare = new AttributedPrepareTerminator();
        var legacy = new FirstTerminator();

        var plan = planner.Plan(Shell, [attributedPrepare, legacy, attributedStart], []);

        // Start tears down first, Prepare last; the legacy terminator sits in Default between them.
        Assert.Equal(
            [typeof(AttributedStartTerminator), typeof(FirstTerminator), typeof(AttributedPrepareTerminator)],
            plan.Entries.Select(e => e.TerminatorType));
        Assert.False(plan.Entries[0].IsExplicit);
    }

    [Fact(DisplayName = "Planner gives explicit registration metadata precedence over attributes")]
    public void Plan_ExplicitMetadata_OverridesAttributeMetadata()
    {
        var attributed = new AttributedPrepareTerminator();

        var plan = planner.Plan(
            Shell,
            [attributed],
            [Registration<AttributedPrepareTerminator>(LifecyclePhase.Start, 5)]);

        var entry = Assert.Single(plan.Entries);
        Assert.Equal(LifecyclePhase.Start, entry.Phase);
        Assert.Equal(5, entry.Order);
        Assert.True(entry.IsExplicit);
    }

    [Fact(DisplayName = "Equal phase/order ties run in reverse DI registration order and emit a diagnostic")]
    public void Plan_EqualPhaseAndOrder_ReverseRegistrationIndexTieBreak()
    {
        var first = new FirstTerminator();
        var second = new SecondTerminator();

        var plan = planner.Plan(
            Shell,
            [first, second],
            [
                Registration<FirstTerminator>(LifecyclePhase.Prepare, 0, registrationIndex: 0),
                Registration<SecondTerminator>(LifecyclePhase.Prepare, 0, registrationIndex: 1)
            ]);

        Assert.Equal([typeof(SecondTerminator), typeof(FirstTerminator)], plan.Entries.Select(e => e.TerminatorType));
        var diagnostic = Assert.Single(plan.Diagnostics);
        Assert.Contains("reverse DI registration index", diagnostic.Message);
    }

    [Fact(DisplayName = "Planner only emits equal-order diagnostics for explicitly ordered ties")]
    public void Plan_EqualOrderDiagnostics_RequireExplicitOrdering()
    {
        var defaultPlan = planner.Plan(
            Shell,
            [new FirstTerminator(), new SecondTerminator()],
            [
                new ShellTerminatorRegistration(typeof(FirstTerminator), LifecyclePhase.Default, 0, -1, IsExplicit: false, Source: "default"),
                new ShellTerminatorRegistration(typeof(SecondTerminator), LifecyclePhase.Default, 0, -1, IsExplicit: false, Source: "default")
            ]);

        Assert.Empty(defaultPlan.Diagnostics);
    }

    [Fact(DisplayName = "Planner fails when explicit metadata does not match resolved terminators")]
    public void Plan_UnmatchedExplicitMetadata_Throws()
    {
        var ex = Assert.Throws<ShellTerminatorOrderException>(() =>
            planner.Plan(
                Shell,
                [new FirstTerminator()],
                [Registration<SecondTerminator>(LifecyclePhase.Prepare, 0)]));

        Assert.Contains(nameof(SecondTerminator), ex.Message);
        Assert.Contains(Shell.Name, ex.Message);
    }

    [Fact(DisplayName = "Planner fails when explicit metadata type is not a terminator")]
    public void Plan_NonTerminatorExplicitMetadata_Throws()
    {
        var ex = Assert.Throws<ShellTerminatorOrderException>(() =>
            planner.Plan(
                Shell,
                [new FirstTerminator()],
                [
                    new ShellTerminatorRegistration(
                        typeof(string),
                        LifecyclePhase.Prepare,
                        Order: 0,
                        RegistrationIndex: -1,
                        IsExplicit: true,
                        Source: "invalid metadata")
                ]));

        Assert.Contains("System.String", ex.Message);
        Assert.Contains(nameof(IShellTerminator), ex.Message);
        Assert.Contains(Shell.Name, ex.Message);
    }

    [Fact(DisplayName = "Planner fails for undefined explicit lifecycle phase")]
    public void Plan_UndefinedExplicitLifecyclePhase_Throws()
    {
        var ex = Assert.Throws<ShellTerminatorOrderException>(() =>
            planner.Plan(
                Shell,
                [new FirstTerminator()],
                [Registration<FirstTerminator>((LifecyclePhase)(-1), 0, registrationIndex: 0)]));

        Assert.Contains("-1", ex.Message);
        Assert.Contains(nameof(FirstTerminator), ex.Message);
    }

    [Fact(DisplayName = "AddShellTerminator registers transient terminator and metadata")]
    public void AddShellTerminator_RegistersTransientTerminatorAndMetadata()
    {
        var services = new ServiceCollection();

        services.AddShellTerminator<FirstTerminator>(LifecyclePhase.Start, 42);

        var terminatorDescriptor = Assert.Single(services, d => d.ServiceType == typeof(IShellTerminator));
        var concreteDescriptor = Assert.Single(services, d => d.ServiceType == typeof(FirstTerminator));
        Assert.Equal(ServiceLifetime.Transient, terminatorDescriptor.Lifetime);
        Assert.Equal(ServiceLifetime.Transient, concreteDescriptor.Lifetime);

        using var provider = services.BuildServiceProvider();
        var terminator = Assert.Single(provider.GetServices<IShellTerminator>());
        var registration = Assert.Single(provider.GetServices<ShellTerminatorRegistration>());

        Assert.IsType<FirstTerminator>(terminator);
        Assert.Equal(typeof(FirstTerminator), registration.TerminatorType);
        Assert.Equal(LifecyclePhase.Start, registration.Phase);
        Assert.Equal(42, registration.Order);
        Assert.Equal(0, registration.RegistrationIndex);
        Assert.True(registration.IsExplicit);
    }

    [Fact(DisplayName = "AddShellTerminator without order registers default metadata without explicit ordering")]
    public void AddShellTerminator_NoArgs_RegistersDefaultMetadataWithoutExplicitOrdering()
    {
        var services = new ServiceCollection();

        services.AddShellTerminator<FirstTerminator>();

        using var provider = services.BuildServiceProvider();
        var registration = Assert.Single(provider.GetServices<ShellTerminatorRegistration>());

        Assert.Equal(typeof(FirstTerminator), registration.TerminatorType);
        Assert.Equal(LifecyclePhase.Default, registration.Phase);
        Assert.Equal(0, registration.Order);
        Assert.Equal(0, registration.RegistrationIndex);
        Assert.False(registration.IsExplicit);
    }

    [Fact(DisplayName = "AddShellTerminator without order does not override attribute metadata")]
    public void AddShellTerminator_NoArgs_DoesNotOverrideAttributeMetadata()
    {
        var services = new ServiceCollection();
        services.AddShellTerminator<AttributedPrepareTerminator>();

        using var provider = services.BuildServiceProvider();
        var terminators = provider.GetServices<IShellTerminator>().ToList();
        var registrations = provider.GetServices<ShellTerminatorRegistration>().ToList();

        var plan = planner.Plan(Shell, terminators, registrations);

        var entry = Assert.Single(plan.Entries);
        Assert.Equal(LifecyclePhase.Prepare, entry.Phase);
        Assert.False(entry.IsExplicit);
    }

    private static ShellTerminatorRegistration Registration<TTerminator>(
        LifecyclePhase phase,
        int order,
        int registrationIndex = -1)
        where TTerminator : IShellTerminator =>
        new(typeof(TTerminator), phase, order, registrationIndex, IsExplicit: true, Source: typeof(TTerminator).Name);

    private sealed class FirstTerminator : IShellTerminator
    {
        public Task TerminateAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class SecondTerminator : IShellTerminator
    {
        public Task TerminateAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class DefaultTerminator : IShellTerminator
    {
        public Task TerminateAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class PrepareEarlyTerminator : IShellTerminator
    {
        public Task TerminateAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class PrepareLateTerminator : IShellTerminator
    {
        public Task TerminateAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class StartTerminator : IShellTerminator
    {
        public Task TerminateAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    [LifecycleOrder(LifecyclePhase.Prepare, 0)]
    private sealed class AttributedPrepareTerminator : IShellTerminator
    {
        public Task TerminateAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    [LifecycleOrder(LifecyclePhase.Start, 0)]
    private sealed class AttributedStartTerminator : IShellTerminator
    {
        public Task TerminateAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
