using System.ComponentModel.DataAnnotations;

namespace CShells.Features.Validation;

/// <summary>
/// Validates feature configuration using DataAnnotations attributes.
/// </summary>
public class DataAnnotationsFeatureConfigurationValidator : IFeatureConfigurationValidator
{
    /// <inheritdoc />
    public void Validate(object target, string contextName)
    {
        Guard.Against.Null(target);
        Guard.Against.Null(contextName);

        var validationContext = new ValidationContext(target);
        var validationResults = new List<ValidationResult>();

        var isValid = Validator.TryValidateObject(target, validationContext, validationResults, validateAllProperties: true);

        if (!isValid)
        {
            var errors = validationResults
                .Select(r => r.ErrorMessage ?? "Validation error")
                .ToList();

            throw new FeatureConfigurationValidationException(contextName, errors);
        }
    }
}
