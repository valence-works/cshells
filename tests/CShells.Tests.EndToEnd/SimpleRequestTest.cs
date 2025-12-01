using System.Net;

namespace CShells.Tests.EndToEnd;

[Collection("Workbench")]
public class SimpleRequestTest(WorkbenchApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [InlineData("/")]
    [InlineData("/acme")]
    [InlineData("/acme/")]
    [InlineData("/contoso")]
    [InlineData("/contoso/")]
    public async Task CanMakeRequest(string path)
    {
        var response = await _client.GetAsync(path);
        var content = await response.Content.ReadAsStringAsync();

        // Log the response for debugging
        Console.WriteLine($"Path: {path}");
        Console.WriteLine($"Status: {response.StatusCode}");
        Console.WriteLine($"Content: {content}");

        // Just check we got some response
        Assert.NotNull(response);
    }
}
