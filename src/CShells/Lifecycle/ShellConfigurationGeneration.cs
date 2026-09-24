using System.Runtime.CompilerServices;
using Microsoft.Extensions.Primitives;

namespace CShells.Lifecycle;

/// <summary>
/// Carries a configuration blueprint's source change token with the exact composed settings
/// instance without adding configuration-source state to the public settings contract.
/// </summary>
internal static class ShellConfigurationGeneration
{
    private static readonly ConditionalWeakTable<ShellSettings, IChangeToken> SourceTokens = new();

    public static void Attach(ShellSettings settings, IChangeToken sourceToken)
    {
        SourceTokens.Remove(settings);
        SourceTokens.Add(settings, sourceToken);
    }

    public static void Copy(ShellSettings source, ShellSettings target)
    {
        if (SourceTokens.TryGetValue(source, out var sourceToken))
            Attach(target, sourceToken);
    }

    public static void ThrowIfChanged(ShellSettings settings, string boundary)
    {
        if (SourceTokens.TryGetValue(settings, out var sourceToken) && sourceToken.HasChanged)
            throw CreateChangedException(settings.Id.Name, boundary);
    }

    public static InvalidOperationException CreateChangedException(string shellName, string boundary) =>
        new($"Configuration changed {boundary} shell '{shellName}'. Retry activation or reload.");
}
