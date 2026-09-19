using CShells.Lifecycle;
using Microsoft.Extensions.DependencyInjection;

namespace CShells.Tests.Unit.Lifecycle;

public class ShellInitializerOrderingPlannerTests
{
    private static readonly ShellDescriptor Shell = ShellDescriptor.Create("test", 1);
    private readonly ShellInitializerOrderingPlanner planner = new();

    [Fact(DisplayName = "Planner orders initializers by phase then order then registration index")]
    public void Plan_PhaseOrderAndNumericOrder_ReturnsDeterministicSequence()
    {
        var prepareLate = new PrepareLateInitializer();
        var start = new StartInitializer();
        var prepareEarly = new PrepareEarlyInitializer();
        var defaultInitializer = new DefaultInitializer();

        var plan = planner.Plan(
            Shell,
            [start, defaultInitializer, prepareLate, prepareEarly],
            [
                Registration<PrepareLateInitializer>(LifecyclePhase.Prepare, 20),
                Registration<StartInitializer>(LifecyclePhase.Start, 0),
                Registration<PrepareEarlyInitializer>(LifecyclePhase.Prepare, 10),
                Registration<DefaultInitializer>(LifecyclePhase.Default, 0)
            ]);

        Assert.Equal(
            [
                typeof(PrepareEarlyInitializer),
                typeof(PrepareLateInitializer),
                typeof(DefaultInitializer),
                typeof(StartInitializer)
            ],
            plan.Entries.Select(e => e.InitializerType));
    }

    [Fact(DisplayName = "Planner maps unordered initializers to Default phase and preserves registration order")]
    public void Plan_UnorderedInitializers_UseDefaultPhaseAndRegistrationOrder()
    {
        var first = new FirstInitializer();
        var second = new SecondInitializer();

        var plan = planner.Plan(Shell, [first, second], []);

        Assert.Equal([typeof(FirstInitializer), typeof(SecondInitializer)], plan.Entries.Select(e => e.InitializerType));
        Assert.All(plan.Entries, e => Assert.Equal(LifecyclePhase.Default, e.Phase));
        Assert.Empty(plan.Diagnostics);
    }

    [Fact(DisplayName = "Planner applies attribute metadata for legacy registrations")]
    public void Plan_AttributeMetadata_OrdersLegacyRegistrations()
    {
        var attributed = new AttributedPrepareInitializer();
        var legacy = new FirstInitializer();

        var plan = planner.Plan(Shell, [legacy, attributed], []);

        Assert.Equal([typeof(AttributedPrepareInitializer), typeof(FirstInitializer)], plan.Entries.Select(e => e.InitializerType));
        Assert.Equal(LifecyclePhase.Prepare, plan.Entries[0].Phase);
        Assert.False(plan.Entries[0].IsExplicit);
    }

    [Fact(DisplayName = "Planner gives explicit registration metadata precedence over attributes")]
    public void Plan_ExplicitMetadata_OverridesAttributeMetadata()
    {
        var attributed = new AttributedPrepareInitializer();

        var plan = planner.Plan(
            Shell,
            [attributed],
            [Registration<AttributedPrepareInitializer>(LifecyclePhase.Start, 5)]);

        var entry = Assert.Single(plan.Entries);
        Assert.Equal(LifecyclePhase.Start, entry.Phase);
        Assert.Equal(5, entry.Order);
        Assert.True(entry.IsExplicit);
    }

    [Fact(DisplayName = "AddShellInitializer registers transient initializer and metadata")]
    public void AddShellInitializer_RegistersTransientInitializerAndMetadata()
    {
        var services = new ServiceCollection();

        services.AddShellInitializer<FirstInitializer>(LifecyclePhase.Prepare, 42);

        var initializerDescriptor = Assert.Single(services, d => d.ServiceType == typeof(IShellInitializer));
        var concreteDescriptor = Assert.Single(services, d => d.ServiceType == typeof(FirstInitializer));
        Assert.Equal(ServiceLifetime.Transient, initializerDescriptor.Lifetime);
        Assert.Equal(ServiceLifetime.Transient, concreteDescriptor.Lifetime);

        using var provider = services.BuildServiceProvider();
        var initializer = Assert.Single(provider.GetServices<IShellInitializer>());
        var registration = Assert.Single(provider.GetServices<ShellInitializerRegistration>());

        Assert.IsType<FirstInitializer>(initializer);
        Assert.Equal(typeof(FirstInitializer), registration.InitializerType);
        Assert.Equal(LifecyclePhase.Prepare, registration.Phase);
        Assert.Equal(42, registration.Order);
        Assert.Equal(0, registration.RegistrationIndex);
        Assert.True(registration.IsExplicit);
    }

    [Fact(DisplayName = "AddShellInitializer without order registers default metadata without explicit ordering")]
    public void AddShellInitializer_NoArgs_RegistersDefaultMetadataWithoutExplicitOrdering()
    {
        var services = new ServiceCollection();

        services.AddShellInitializer<FirstInitializer>();

        using var provider = services.BuildServiceProvider();
        var registration = Assert.Single(provider.GetServices<ShellInitializerRegistration>());

        Assert.Equal(typeof(FirstInitializer), registration.InitializerType);
        Assert.Equal(LifecyclePhase.Default, registration.Phase);
        Assert.Equal(0, registration.Order);
        Assert.Equal(0, registration.RegistrationIndex);
        Assert.False(registration.IsExplicit);
    }

    [Fact(DisplayName = "AddShellInitializer without order does not override attribute metadata")]
    public void AddShellInitializer_NoArgs_DoesNotOverrideAttributeMetadata()
    {
        var services = new ServiceCollection();
        services.AddShellInitializer<AttributedPrepareInitializer>();

        using var provider = services.BuildServiceProvider();
        var initializers = provider.GetServices<IShellInitializer>().ToList();
        var registrations = provider.GetServices<ShellInitializerRegistration>().ToList();

        var plan = planner.Plan(Shell, initializers, registrations);

        var entry = Assert.Single(plan.Entries);
        Assert.Equal(LifecyclePhase.Prepare, entry.Phase);
        Assert.False(entry.IsExplicit);
    }

    [Fact(DisplayName = "AddShellInitializer preserves an initializer already registered by the consumer")]
    public void AddShellInitializer_ExistingRegistration_LifetimeIsPreserved()
    {
        var services = new ServiceCollection();
        services.AddSingleton<FirstInitializer>();

        services.AddShellInitializer<FirstInitializer>(LifecyclePhase.Prepare, -100);

        var concreteDescriptor = Assert.Single(services, d => d.ServiceType == typeof(FirstInitializer));
        Assert.Equal(ServiceLifetime.Singleton, concreteDescriptor.Lifetime);

        using var provider = services.BuildServiceProvider();
        var initializer = provider.GetRequiredService<FirstInitializer>();

        // Every resolution path, and every repeat enumeration, must hand back that one
        // instance; a stateful initializer is only correct if the lifecycle shares it.
        Assert.Same(initializer, provider.GetRequiredService<FirstInitializer>());
        Assert.Same(initializer, Assert.Single(provider.GetServices<IShellInitializer>()));
        Assert.Same(initializer, Assert.Single(provider.GetServices<IShellInitializer>()));
    }

    [Fact(DisplayName = "Planner only emits equal-order diagnostics for explicitly ordered ties")]
    public void Plan_EqualOrderDiagnostics_RequireExplicitOrdering()
    {
        var explicitPlan = planner.Plan(
            Shell,
            [new FirstInitializer(), new SecondInitializer()],
            [
                Registration<FirstInitializer>(LifecyclePhase.Prepare, 0),
                Registration<SecondInitializer>(LifecyclePhase.Prepare, 0)
            ]);
        var defaultPlan = planner.Plan(
            Shell,
            [new FirstInitializer(), new SecondInitializer()],
            [
                new ShellInitializerRegistration(typeof(FirstInitializer), LifecyclePhase.Default, 0, -1, IsExplicit: false, Source: "default"),
                new ShellInitializerRegistration(typeof(SecondInitializer), LifecyclePhase.Default, 0, -1, IsExplicit: false, Source: "default")
            ]);

        Assert.Single(explicitPlan.Diagnostics);
        Assert.Empty(defaultPlan.Diagnostics);
    }

    [Fact(DisplayName = "Planner matches explicit metadata by DI registration index before runtime type")]
    public void Plan_MetadataRegistrationIndex_MatchesDecoratedInitializer()
    {
        var plan = planner.Plan(
            Shell,
            [new WrapperInitializer()],
            [Registration<FirstInitializer>(LifecyclePhase.Prepare, 5, registrationIndex: 0)]);

        var entry = Assert.Single(plan.Entries);
        Assert.Equal(typeof(WrapperInitializer), entry.InitializerType);
        Assert.Equal(LifecyclePhase.Prepare, entry.Phase);
        Assert.Equal(5, entry.Order);
    }

    [Fact(DisplayName = "Planner fails for undefined explicit lifecycle phase")]
    public void Plan_UndefinedExplicitLifecyclePhase_Throws()
    {
        var ex = Assert.Throws<ShellInitializerOrderException>(() =>
            planner.Plan(
                Shell,
                [new FirstInitializer()],
                [Registration<FirstInitializer>((LifecyclePhase)(-1), 0, registrationIndex: 0)]));

        Assert.Contains("-1", ex.Message);
        Assert.Contains(nameof(FirstInitializer), ex.Message);
    }

    [Fact(DisplayName = "Planner fails for undefined attribute lifecycle phase")]
    public void Plan_UndefinedAttributeLifecyclePhase_Throws()
    {
        var ex = Assert.Throws<ShellInitializerOrderException>(() =>
            planner.Plan(Shell, [new InvalidPhaseAttributeInitializer()], []));

        Assert.Contains("-1", ex.Message);
        Assert.Contains(nameof(LifecycleOrderAttribute), ex.Message);
    }

    [Fact(DisplayName = "Planner fails when explicit metadata does not match resolved initializers")]
    public void Plan_UnmatchedExplicitMetadata_Throws()
    {
        var ex = Assert.Throws<ShellInitializerOrderException>(() =>
            planner.Plan(
                Shell,
                [new FirstInitializer()],
                [Registration<SecondInitializer>(LifecyclePhase.Prepare, 0)]));

        Assert.Contains(nameof(SecondInitializer), ex.Message);
        Assert.Contains(Shell.Name, ex.Message);
    }

    [Fact(DisplayName = "Planner fails when explicit metadata type is not an initializer")]
    public void Plan_NonInitializerExplicitMetadata_Throws()
    {
        var ex = Assert.Throws<ShellInitializerOrderException>(() =>
            planner.Plan(
                Shell,
                [new FirstInitializer()],
                [
                    new ShellInitializerRegistration(
                        typeof(string),
                        LifecyclePhase.Prepare,
                        Order: 0,
                        RegistrationIndex: -1,
                        IsExplicit: true,
                        Source: "invalid metadata")
                ]));

        Assert.Contains("System.String", ex.Message);
        Assert.Contains(nameof(IShellInitializer), ex.Message);
        Assert.Contains(Shell.Name, ex.Message);
    }

    private static ShellInitializerRegistration Registration<TInitializer>(
        LifecyclePhase phase,
        int order,
        int registrationIndex = -1)
        where TInitializer : IShellInitializer =>
        new(typeof(TInitializer), phase, order, registrationIndex, IsExplicit: true, Source: typeof(TInitializer).Name);

    private sealed class FirstInitializer : IShellInitializer
    {
        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class SecondInitializer : IShellInitializer
    {
        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class WrapperInitializer : IShellInitializer
    {
        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class DefaultInitializer : IShellInitializer
    {
        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class PrepareEarlyInitializer : IShellInitializer
    {
        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class PrepareLateInitializer : IShellInitializer
    {
        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class StartInitializer : IShellInitializer
    {
        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    [LifecycleOrder(LifecyclePhase.Prepare, 0)]
    private sealed class AttributedPrepareInitializer : IShellInitializer
    {
        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    [LifecycleOrder((LifecyclePhase)(-1), 0)]
    private sealed class InvalidPhaseAttributeInitializer : IShellInitializer
    {
        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
