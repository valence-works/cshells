using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CShells.Notifications;

/// <summary>
/// Default implementation of <see cref="INotificationPublisher"/> that resolves handlers from DI.
/// </summary>
public class DefaultNotificationPublisher(IServiceProvider serviceProvider, ILogger<DefaultNotificationPublisher>? logger = null) : INotificationPublisher
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ILogger<DefaultNotificationPublisher> _logger = logger ?? NullLogger<DefaultNotificationPublisher>.Instance;
    private readonly INotificationStrategy _defaultStrategy = new ParallelNotificationStrategy();

    /// <inheritdoc />
    public async Task PublishAsync<TNotification>(
        TNotification notification,
        INotificationStrategy? strategy = null,
        CancellationToken cancellationToken = default)
        where TNotification : INotification
    {
        ArgumentNullException.ThrowIfNull(notification);

        var executionStrategy = strategy ?? _defaultStrategy;

        _logger.LogDebug("Publishing notification of type {NotificationType} using {StrategyType}",
            typeof(TNotification).Name, executionStrategy.GetType().Name);

        var handlers = _serviceProvider.GetServices<INotificationHandler<TNotification>>().ToList();

        if (handlers.Count == 0)
        {
            _logger.LogDebug("No handlers registered for notification type {NotificationType}", typeof(TNotification).Name);
            return;
        }

        _logger.LogDebug("Executing {HandlerCount} handler(s) for notification type {NotificationType}",
            handlers.Count, typeof(TNotification).Name);

        try
        {
            await executionStrategy.ExecuteAsync(handlers, notification, cancellationToken);
            _logger.LogDebug("Completed publishing notification of type {NotificationType}", typeof(TNotification).Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing notification {NotificationType}", typeof(TNotification).Name);
            throw;
        }
    }
}
