namespace CShells.SampleApp.Features.Core;

/// <summary>
/// Time service interface for getting current time.
/// </summary>
public interface ITimeService
{
    /// <summary>
    /// Gets the current time.
    /// </summary>
    DateTime GetCurrentTime();
}