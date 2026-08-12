namespace CShells.Tests.EndToEnd;

/// <summary>
/// End-to-end tests for the <c>IMiddlewareShellFeature</c> seam via the Workbench sample's
/// RequestStamp feature (enabled for Acme only). The feature mounts an <c>IMiddleware</c>-style
/// middleware — resolved from the shell scope through <c>IMiddlewareFactory</c> — that stamps
/// responses with an <c>X-Shell-Stamp</c> header. Workbench shells activate lazily, so the first
/// request exercises the dynamic (post-startup) middleware registration path.
/// </summary>
[Collection("Workbench")]
public class ShellMiddlewareFeatureTests(WorkbenchApplicationFactory factory)
{
    private const string StampHeader = "X-Shell-Stamp";
    private readonly HttpClient _client = factory.CreateClient();

    [Fact(DisplayName = "Middleware feature runs for a lazily (dynamically) activated shell")]
    public async Task MiddlewareFeature_RunsOnLazilyActivatedShell()
    {
        var response = await _client.GetAsync("/acme/");

        response.EnsureSuccessStatusCode();
        Assert.Equal("Acme", Assert.Single(response.Headers.GetValues(StampHeader)));
    }

    [Fact(DisplayName = "Middleware feature does not run for shells that do not enable it")]
    public async Task MiddlewareFeature_DoesNotRunForOtherShells()
    {
        var response = await _client.GetAsync("/contoso/");

        response.EnsureSuccessStatusCode();
        Assert.False(response.Headers.Contains(StampHeader));
    }

}

/// <summary>
/// Reload coverage for the middleware seam. Uses its own application instance (not the shared
/// "Workbench" collection fixture) because reloading Acme advances its generation, which would
/// leak into tests that assert on generation numbers.
/// </summary>
public class ShellMiddlewareFeatureReloadTests
{
    [Fact(DisplayName = "Middleware feature still runs after a shell reload (new generation)")]
    public async Task MiddlewareFeature_RunsAfterReload()
    {
        await using var factory = new WorkbenchApplicationFactory();
        var client = factory.CreateClient();

        (await client.GetAsync("/acme/")).EnsureSuccessStatusCode(); // activate generation 1

        var reload = await client.PostAsync("/_admin/shells/reload/Acme", null);
        reload.EnsureSuccessStatusCode();

        var response = await client.GetAsync("/acme/");

        response.EnsureSuccessStatusCode();
        Assert.Equal("Acme", Assert.Single(response.Headers.GetValues("X-Shell-Stamp")));
    }
}
