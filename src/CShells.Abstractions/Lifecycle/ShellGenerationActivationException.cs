namespace CShells.Lifecycle;

/// <summary>
/// Indicates that a shell generation could not be published during its activation transition.
/// </summary>
/// <remarks>
/// Lifecycle subscribers normally cannot disrupt a transition because subscriber isolation is a
/// framework invariant. Endpoint publication is the explicit exception: a rejected candidate must
/// abort the candidate generation so a previous active generation can continue serving traffic.
/// </remarks>
public sealed class ShellGenerationActivationException : InvalidOperationException
{
    /// <summary>Initializes a new exception for the rejected shell generation.</summary>
    /// <param name="descriptor">The rejected generation descriptor.</param>
    /// <param name="innerException">The publication or mapping failure.</param>
    public ShellGenerationActivationException(ShellDescriptor descriptor, Exception innerException)
        : base($"Shell generation '{Guard.Against.Null(descriptor)}' could not be published.", Guard.Against.Null(innerException))
    {
        Descriptor = descriptor;
    }

    /// <summary>Gets the rejected generation descriptor.</summary>
    public ShellDescriptor Descriptor { get; }
}
