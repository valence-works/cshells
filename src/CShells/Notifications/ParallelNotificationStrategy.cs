namespace CShells.Notifications;

/// <summary>
/// Executes notification handlers in parallel using Task.WhenAll for best performance.
/// </summary>
public class ParallelNotificationStrategy : INotificationStrategy
{
    /// <inheritdoc />
    public async Task ExecuteAsync<TNotification>(
        IEnumerable<INotificationHandler<TNotification>> handlers,
        TNotification notification,
        CancellationToken cancellationToken)
        where TNotification : INotification
    {
        var handlerTasks = handlers.Select(handler => handler.HandleAsync(notification, cancellationToken));
        await Task.WhenAll(handlerTasks);
    }
}
