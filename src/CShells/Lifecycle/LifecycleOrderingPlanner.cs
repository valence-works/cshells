using System.Reflection;

namespace CShells.Lifecycle;

/// <summary>
/// Shared ordering core for shell lifecycle components. Matches resolved component instances
/// to registration metadata (explicit registration first, then <see cref="LifecycleOrderAttribute"/>,
/// then legacy defaults), validates the result, and sorts by the requested direction:
/// <see cref="Direction.Startup"/> ascends phase/order/registration-index;
/// <see cref="Direction.Teardown"/> is the exact mirror (descending on all three keys).
/// </summary>
internal static class LifecycleOrderingPlanner
{
    internal enum Direction
    {
        Startup,
        Teardown,
    }

    internal sealed record ComponentMetadata(
        Type ComponentType,
        LifecyclePhase Phase,
        int Order,
        int RegistrationIndex,
        bool IsExplicit,
        string Source);

    internal sealed record Entry<TComponent>(
        TComponent Component,
        Type ComponentType,
        LifecyclePhase Phase,
        int Order,
        int RegistrationIndex,
        bool IsExplicit,
        string Source);

    internal sealed record Diagnostic(
        string Message,
        IReadOnlyList<Type> ComponentTypes);

    internal sealed record OrderingPlan<TComponent>(
        IReadOnlyList<Entry<TComponent>> Entries,
        IReadOnlyList<Diagnostic> Diagnostics);

    public static OrderingPlan<TComponent> Plan<TComponent>(
        ShellDescriptor shell,
        IReadOnlyList<TComponent> components,
        IReadOnlyList<ComponentMetadata> registrations,
        Direction direction,
        string componentNoun,
        Func<ShellDescriptor, string, Exception> errorFactory)
        where TComponent : class
    {
        Guard.Against.Null(components);
        Guard.Against.Null(registrations);

        foreach (var registration in registrations)
        {
            if (!typeof(TComponent).IsAssignableFrom(registration.ComponentType))
            {
                throw errorFactory(
                    shell,
                    $"ordering metadata source '{registration.Source}' references type '{registration.ComponentType.FullName}' which does not implement {typeof(TComponent).Name}.");
            }

            ValidatePhase(shell, registration.Phase, registration.Source, errorFactory);
        }

        var pendingRegistrations = registrations.ToList();

        var entries = new List<Entry<TComponent>>(components.Count);

        for (var i = 0; i < components.Count; i++)
        {
            var component = components[i];
            var type = component.GetType();
            var metadata = ResolveMetadata(shell, registrationIndex: i, type, pendingRegistrations, errorFactory);
            entries.Add(new Entry<TComponent>(
                component,
                type,
                metadata.Phase,
                metadata.Order,
                RegistrationIndex: i,
                metadata.IsExplicit,
                metadata.Source));
        }

        var unmatched = pendingRegistrations
            .Select(r => r.ComponentType.FullName ?? r.ComponentType.Name)
            .ToList();

        if (unmatched.Count > 0)
            throw errorFactory(
                shell,
                $"ordering metadata was registered for {componentNoun} type(s) that were not resolved from DI: {string.Join(", ", unmatched)}.");

        var tieBreak = direction == Direction.Startup
            ? "DI registration index will be used as the deterministic tie-break."
            : "reverse DI registration index will be used as the deterministic tie-break.";

        var duplicateGroups = entries
            .GroupBy(e => (e.Phase, e.Order))
            .Where(g => g.Count() > 1 && g.Any(e => e.IsExplicit))
            .Select(g => new Diagnostic(
                $"Multiple {componentNoun}s share phase '{g.Key.Phase}' and order {g.Key.Order}; {tieBreak}",
                [.. g.Select(e => e.ComponentType)]))
            .ToList();

        var ordered = direction == Direction.Startup
            ? entries.OrderBy(e => e.Phase).ThenBy(e => e.Order).ThenBy(e => e.RegistrationIndex).ToList()
            : entries.OrderByDescending(e => e.Phase).ThenByDescending(e => e.Order).ThenByDescending(e => e.RegistrationIndex).ToList();

        return new OrderingPlan<TComponent>(ordered, duplicateGroups);
    }

    private static ComponentMetadata ResolveMetadata(
        ShellDescriptor shell,
        int registrationIndex,
        Type componentType,
        List<ComponentMetadata> pendingRegistrations,
        Func<ShellDescriptor, string, Exception> errorFactory)
    {
        var registration = TakeFirst(pendingRegistrations, r => r.RegistrationIndex == registrationIndex)
            ?? TakeFirst(pendingRegistrations, r => r.ComponentType == componentType);

        if (registration is not null)
            return ResolveRegistrationMetadata(shell, registration, errorFactory);

        var attribute = componentType.GetCustomAttribute<LifecycleOrderAttribute>();
        if (attribute is not null)
            return ResolveAttributeMetadata(shell, componentType, attribute, errorFactory);

        return new ComponentMetadata(
            componentType,
            LifecyclePhase.Default,
            Order: 0,
            RegistrationIndex: -1,
            IsExplicit: false,
            Source: "Legacy DI registration");
    }

    private static ComponentMetadata ResolveRegistrationMetadata(
        ShellDescriptor shell,
        ComponentMetadata registration,
        Func<ShellDescriptor, string, Exception> errorFactory)
    {
        if (!registration.IsExplicit)
        {
            var attribute = registration.ComponentType.GetCustomAttribute<LifecycleOrderAttribute>();
            if (attribute is not null)
                return ResolveAttributeMetadata(shell, registration.ComponentType, attribute, errorFactory);
        }

        ValidatePhase(shell, registration.Phase, registration.Source, errorFactory);
        return registration;
    }

    private static ComponentMetadata ResolveAttributeMetadata(
        ShellDescriptor shell,
        Type componentType,
        LifecycleOrderAttribute attribute,
        Func<ShellDescriptor, string, Exception> errorFactory)
    {
        var source = $"{nameof(LifecycleOrderAttribute)} on {componentType.FullName}";
        ValidatePhase(shell, attribute.Phase, source, errorFactory);
        return new ComponentMetadata(
            componentType,
            attribute.Phase,
            attribute.Order,
            RegistrationIndex: -1,
            IsExplicit: false,
            Source: source);
    }

    private static ComponentMetadata? TakeFirst(
        List<ComponentMetadata> registrations,
        Func<ComponentMetadata, bool> predicate)
    {
        var index = registrations.FindIndex(r => predicate(r));
        if (index < 0)
            return null;

        var registration = registrations[index];
        registrations.RemoveAt(index);
        return registration;
    }

    private static void ValidatePhase(
        ShellDescriptor shell,
        LifecyclePhase phase,
        string source,
        Func<ShellDescriptor, string, Exception> errorFactory)
    {
        if (!Enum.IsDefined(typeof(LifecyclePhase), phase))
            throw errorFactory(shell, $"ordering metadata source '{source}' uses undefined lifecycle phase value '{(int)phase}'.");
    }
}
