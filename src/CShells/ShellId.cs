namespace CShells;

/// <summary>
/// A value-type identifier for shells.
/// </summary>
public readonly record struct ShellId(string Name)
{
    /// <summary>
    /// Gets the name of the shell.
    /// </summary>
    public string Name { get; } = Name ?? throw new ArgumentNullException(nameof(Name));

    /// <summary>
    /// Returns the string representation of the <see cref="ShellId"/>.
    /// </summary>
    /// <returns>The name of the shell.</returns>
    public override string ToString() => Name;

    /// <summary>
    /// Determines whether the specified <see cref="ShellId"/> is equal to the current <see cref="ShellId"/>.
    /// </summary>
    /// <param name="other">The <see cref="ShellId"/> to compare with the current <see cref="ShellId"/>.</param>
    /// <returns><c>true</c> if the specified <see cref="ShellId"/> is equal to the current <see cref="ShellId"/>; otherwise, <c>false</c>.</returns>
    public bool Equals(ShellId other) => string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Returns the hash code for this <see cref="ShellId"/>.
    /// </summary>
    /// <returns>A 32-bit signed integer hash code.</returns>
    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Name);
}
