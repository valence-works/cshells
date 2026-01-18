using CShells.Hosting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;

namespace CShells.AspNetCore.Hosting;

/// <summary>
/// Provides ASP.NET Core-specific service types that should not be copied to shell service collections.
/// </summary>
/// <remarks>
/// <para>
/// This provider ensures that authentication and authorization providers are excluded from being
/// copied from the root service provider to shell service providers. This allows each shell to
/// register its own authentication schemes and authorization policies.
/// </para>
/// <para>
/// Excluded types:
/// <list type="bullet">
/// <item><description><see cref="IAuthenticationSchemeProvider"/> - Enables per-shell authentication schemes (JWT, API Key, etc.)</description></item>
/// <item><description><see cref="IAuthorizationPolicyProvider"/> - Enables per-shell authorization policies (e.g., FastEndpoints epPolicy:* policies)</description></item>
/// </list>
/// </para>
/// <para>
/// These exclusions work in conjunction with <see cref="ShellAuthenticationSchemeProvider"/> and
/// <see cref="ShellAuthorizationPolicyProvider"/>, which bridge the root middleware to shell-specific providers.
/// </para>
/// </remarks>
public class AspNetCoreShellServiceExclusionProvider : IShellServiceExclusionProvider
{
    /// <inheritdoc />
    public IEnumerable<Type> GetExcludedServiceTypes()
    {
        // IAuthorizationPolicyProvider must not be copied so each shell can have its own
        // with its own set of policies (e.g., FastEndpoints epPolicy:* policies)
        yield return typeof(IAuthorizationPolicyProvider);

        // IAuthenticationSchemeProvider must not be copied so each shell can have its own
        // with its own set of schemes (e.g., JWT, API Key)
        yield return typeof(IAuthenticationSchemeProvider);
    }
}
