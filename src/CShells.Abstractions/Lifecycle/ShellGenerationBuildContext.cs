namespace CShells.Lifecycle;

/// <summary>
/// Immutable identity reserved by CShells for one attempted shell generation.
/// </summary>
/// <param name="Descriptor">The descriptor assigned from the blueprint before composition.</param>
/// <param name="ShellId">The shell identifier created from the same blueprint name.</param>
/// <remarks>
/// The descriptor's metadata is an immutable snapshot. A generation is reserved even if composition or a later
/// build phase fails, so its value is never reused for that shell name.
/// </remarks>
public sealed record ShellGenerationBuildContext(ShellDescriptor Descriptor, ShellId ShellId);
