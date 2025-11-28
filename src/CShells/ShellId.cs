namespace CShells;

/// <summary>
/// A value-type identifier for shells.
/// </summary>
public readonly struct ShellId : IEquatable<ShellId>
{
    /// <summary>
    /// Gets the name of the shell.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ShellId"/> struct.
    /// </summary>
    /// <param name="name">The name of the shell.</param>
    public ShellId(string name)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    /// <summary>
    /// Determines whether the specified <see cref="ShellId"/> is equal to the current <see cref="ShellId"/>.
    /// </summary>
    /// <param name="other">The <see cref="ShellId"/> to compare with the current <see cref="ShellId"/>.</param>
    /// <returns><c>true</c> if the specified <see cref="ShellId"/> is equal to the current <see cref="ShellId"/>; otherwise, <c>false</c>.</returns>
    public bool Equals(ShellId other) => string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Determines whether the specified object is equal to the current <see cref="ShellId"/>.
    /// </summary>
    /// <param name="obj">The object to compare with the current <see cref="ShellId"/>.</param>
    /// <returns><c>true</c> if the specified object is equal to the current <see cref="ShellId"/>; otherwise, <c>false</c>.</returns>
    public override bool Equals(object? obj) => obj is ShellId other && Equals(other);

    /// <summary>
    /// Returns the hash code for this <see cref="ShellId"/>.
    /// </summary>
    /// <returns>A 32-bit signed integer hash code.</returns>
    public override int GetHashCode() => Name is null ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(Name);

    /// <summary>
    /// Returns the string representation of the <see cref="ShellId"/>.
    /// </summary>
    /// <returns>The name of the shell.</returns>
    public override string ToString() => Name ?? string.Empty;

    /// <summary>
    /// Determines whether two <see cref="ShellId"/> instances are equal.
    /// </summary>
    /// <param name="left">The first <see cref="ShellId"/> to compare.</param>
    /// <param name="right">The second <see cref="ShellId"/> to compare.</param>
    /// <returns><c>true</c> if the two <see cref="ShellId"/> instances are equal; otherwise, <c>false</c>.</returns>
    public static bool operator ==(ShellId left, ShellId right) => left.Equals(right);

    /// <summary>
    /// Determines whether two <see cref="ShellId"/> instances are not equal.
    /// </summary>
    /// <param name="left">The first <see cref="ShellId"/> to compare.</param>
    /// <param name="right">The second <see cref="ShellId"/> to compare.</param>
    /// <returns><c>true</c> if the two <see cref="ShellId"/> instances are not equal; otherwise, <c>false</c>.</returns>
    public static bool operator !=(ShellId left, ShellId right) => !left.Equals(right);
}
