namespace CShells.Notifications;

/// <summary>
/// Publishes notifications to all registered handlers.
/// </summary>
public interface INotificationPublisher
{
    /// <summary>
    /// Publishes a notification to all registered handlers.
    /// </summary>
    /// <typeparam name="TNotification">The type of notification.</typeparam>
    /// <param name="notification">The notification to publish.</param>
    /// <param name="strategy">The execution strategy for handlers. If null, uses parallel execution.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task PublishAsync<TNotification>(
        TNotification notification,
        INotificationStrategy? strategy = null,
        CancellationToken cancellationToken = default)
        where TNotification : INotification;
}
