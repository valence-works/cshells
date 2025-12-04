namespace CShells;

/// <summary>
/// A value-type identifier for shells.
/// </summary>
public readonly record struct ShellId
{
    public ShellId(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }

    /// <summary>
    /// Gets the name of the shell.
    /// </summary>
    public string Name { get; }

    public override string ToString() => Name;

    public bool Equals(ShellId other) => string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase);

    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Name);

    /// <summary>
    /// Implicitly converts a string to a <see cref="ShellId"/>.
    /// </summary>
    public static implicit operator ShellId(string name) => new(name);

    /// <summary>
    /// Implicitly converts a <see cref="ShellId"/> to a string.
    /// </summary>
    public static implicit operator string(ShellId id) => id.Name;
}
