namespace CShells.SampleApp.Features.Greeting;

/// <summary>
/// Greeting service interface for generating shell-specific greetings.
/// </summary>
public interface IGreetingService
{
    /// <summary>
    /// Gets a greeting message specific to the current shell.
    /// </summary>
    string GetGreeting();
}