namespace CShells.SampleApp.Features.Core;

/// <summary>
/// Implementation of the time service.
/// </summary>
public class TimeService : ITimeService
{
    /// <inheritdoc />
    public DateTime GetCurrentTime() => DateTime.UtcNow;
}