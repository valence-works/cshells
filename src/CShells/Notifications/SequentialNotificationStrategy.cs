namespace CShells.Notifications;

/// <summary>
/// Executes notification handlers sequentially in the order they were registered.
/// Use this when handlers have ordering dependencies.
/// </summary>
public class SequentialNotificationStrategy : INotificationStrategy
{
    /// <inheritdoc />
    public async Task ExecuteAsync<TNotification>(
        IEnumerable<INotificationHandler<TNotification>> handlers,
        TNotification notification,
        CancellationToken cancellationToken)
        where TNotification : INotification
    {
        foreach (var handler in handlers)
        {
            await handler.HandleAsync(notification, cancellationToken);
        }
    }
}
