namespace CShells.AspNetCore.Routing;

/// <summary>
/// Metadata attached to endpoints to identify which shell they belong to.
/// </summary>
/// <param name="ShellId">The ID of the shell that owns this endpoint.</param>
/// <param name="ShellSettings">The shell settings for this endpoint.</param>
public record ShellEndpointMetadata(ShellId ShellId, ShellSettings ShellSettings);
