namespace CShells.AspNetCore.Routing;

/// <summary>
/// Metadata attached to endpoints to identify which shell they belong to.
/// </summary>
public class ShellEndpointMetadata
{
    /// <summary>
    /// Gets the ID of the shell that owns this endpoint.
    /// </summary>
    public ShellId ShellId { get; }

    /// <summary>
    /// Gets the shell settings for this endpoint.
    /// </summary>
    public ShellSettings ShellSettings { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ShellEndpointMetadata"/> class.
    /// </summary>
    /// <param name="shellId">The shell ID.</param>
    /// <param name="shellSettings">The shell settings.</param>
    public ShellEndpointMetadata(ShellId shellId, ShellSettings shellSettings)
    {
        ShellId = shellId;
        ShellSettings = shellSettings;
    }
}
