namespace CShells.SampleApp.Features.Greeting;

/// <summary>
/// Implementation of the greeting service that includes the shell name in greetings.
/// </summary>
public class GreetingService(ShellSettings shellSettings) : IGreetingService
{
    /// <inheritdoc />
    public string GetGreeting()
    {
        return $"Hello from the {shellSettings.Id.Name} shell!";
    }
}