using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CShells.Tests.EndToEnd;

/// <summary>
/// End-to-end tests for path-based shell resolution using WebApplicationFactory.
/// These tests verify that the shell resolution middleware correctly routes requests
/// based on URL path prefixes configured in shell properties.
/// </summary>
[Collection("Workbench")]
public class PathBasedShellResolutionTests(WorkbenchApplicationFactory factory)
{
    private readonly WorkbenchApplicationFactory _factory = factory;
    private readonly HttpClient _client = factory.CreateClient();

    [Fact(DisplayName = "Root path resolves to Default shell")]
    public async Task RootPath_ResolvesToDefaultShell()
    {
        // Act
        var response = await _client.GetAsync("/");

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        var tenant = json.RootElement.GetProperty("tenant").GetString();
        var tenantId = json.RootElement.GetProperty("tenantId").GetString();

        Assert.Equal("Default", tenant);
        Assert.Equal("Default", tenantId);
    }

    [Fact(DisplayName = "/acme path resolves to Acme shell")]
    public async Task AcmePath_ResolvesToAcmeShell()
    {
        // Act
        var response = await _client.GetAsync("/acme/");

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        var tenant = json.RootElement.GetProperty("tenant").GetString();
        var tenantId = json.RootElement.GetProperty("tenantId").GetString();

        Assert.Equal("Acme", tenant);
        Assert.Equal("Acme", tenantId);
    }

    [Fact(DisplayName = "/contoso path resolves to Contoso shell")]
    public async Task ContosoPath_ResolvesToContosoShell()
    {
        // Act
        var response = await _client.GetAsync("/contoso/");

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        var tenant = json.RootElement.GetProperty("tenant").GetString();
        var tenantId = json.RootElement.GetProperty("tenantId").GetString();

        Assert.Equal("Contoso", tenant);
        Assert.Equal("Contoso", tenantId);
    }

    [Theory(DisplayName = "Different tenant paths resolve to correct shells")]
    [InlineData("/", "Default")]
    [InlineData("/acme/", "Acme")]
    [InlineData("/contoso/", "Contoso")]
    public async Task TenantPaths_ResolveToCorrectShells(string path, string expectedTenant)
    {
        // Act
        var response = await _client.GetAsync(path);

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        var tenant = json.RootElement.GetProperty("tenant").GetString();

        Assert.Equal(expectedTenant, tenant);
    }

    [Fact(DisplayName = "Shell properties are correctly configured from JSON")]
    public async Task ShellProperties_AreCorrectlyLoaded()
    {
        // This test verifies that shells were loaded from JSON files
        // by checking that all three tenants respond correctly

        var tasks = new[]
        {
            _client.GetAsync("/"),
            _client.GetAsync("/acme/"),
            _client.GetAsync("/contoso/")
        };

        var responses = await Task.WhenAll(tasks);

        // All should succeed
        foreach (var response in responses)
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}
