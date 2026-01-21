namespace CShells.Features.Validation;

/// <summary>
/// Exception thrown when feature configuration validation fails.
/// </summary>
public class FeatureConfigurationValidationException : Exception
{
    /// <summary>
    /// Gets the context name where validation failed (e.g., feature name or options type name).
    /// </summary>
    public string ContextName { get; }

    /// <summary>
    /// Gets the validation errors.
    /// </summary>
    public IReadOnlyList<string> ValidationErrors { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="FeatureConfigurationValidationException"/> class.
    /// </summary>
    /// <param name="contextName">The context name where validation failed.</param>
    /// <param name="validationErrors">The validation errors.</param>
    public FeatureConfigurationValidationException(string contextName, IReadOnlyList<string> validationErrors)
        : base($"Configuration validation failed for '{contextName}': {string.Join("; ", validationErrors)}")
    {
        ContextName = contextName;
        ValidationErrors = validationErrors;
    }
}
