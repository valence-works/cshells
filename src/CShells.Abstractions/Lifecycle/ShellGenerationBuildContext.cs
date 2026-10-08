namespace CShells.Lifecycle;

/// <summary>
/// Immutable identity reserved by CShells for one attempted shell generation.
/// </summary>
/// <param name="Descriptor">The descriptor assigned from the blueprint before composition.</param>
/// <param name="ShellId">The shell identifier created from the same blueprint name.</param>
/// <remarks>
/// The descriptor's metadata is an immutable snapshot. A generation is reserved even if composition or a later
/// build phase fails. Its value is never reused for that shell name within the owning registry, including after
/// unregistering and recreating that name. A new registry starts its own generation sequence.
/// </remarks>
public sealed record ShellGenerationBuildContext(ShellDescriptor Descriptor, ShellId ShellId);
