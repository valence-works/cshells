namespace CShells.Notifications;

/// <summary>
/// Defines the execution strategy for notification handlers.
/// </summary>
public interface INotificationStrategy
{
    /// <summary>
    /// Executes the notification handlers according to the strategy.
    /// </summary>
    /// <typeparam name="TNotification">The type of notification.</typeparam>
    /// <param name="handlers">The handlers to execute.</param>
    /// <param name="notification">The notification to publish.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task ExecuteAsync<TNotification>(
        IEnumerable<INotificationHandler<TNotification>> handlers,
        TNotification notification,
        CancellationToken cancellationToken)
        where TNotification : INotification;
}
