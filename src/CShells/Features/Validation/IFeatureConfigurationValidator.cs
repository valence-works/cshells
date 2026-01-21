namespace CShells.Features.Validation;

/// <summary>
/// Defines the contract for validating feature configuration.
/// Implementations can use DataAnnotations, FluentValidation, or custom validation logic.
/// </summary>
public interface IFeatureConfigurationValidator
{
    /// <summary>
    /// Validates the specified configuration object.
    /// </summary>
    /// <param name="target">The object to validate.</param>
    /// <param name="contextName">The context name for error messages (e.g., feature name).</param>
    /// <exception cref="FeatureConfigurationValidationException">Thrown when validation fails.</exception>
    void Validate(object target, string contextName);
}
