namespace CShells.Notifications;

/// <summary>
/// Notification published when all shells are reloaded from the provider.
/// </summary>
/// <param name="AllShells">All shell settings after the reload.</param>
public record ShellsReloadedNotification(IReadOnlyCollection<ShellSettings> AllShells) : INotification;
