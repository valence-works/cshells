namespace CShells.Features.Validation;

/// <summary>
/// Validates feature configuration using multiple validators in sequence.
/// This allows combining DataAnnotations, FluentValidation, and custom validators.
/// </summary>
public class CompositeFeatureConfigurationValidator : IFeatureConfigurationValidator
{
    private readonly IEnumerable<IFeatureConfigurationValidator> _validators;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompositeFeatureConfigurationValidator"/> class.
    /// </summary>
    /// <param name="validators">The validators to execute in sequence.</param>
    public CompositeFeatureConfigurationValidator(IEnumerable<IFeatureConfigurationValidator> validators)
    {
        _validators = Guard.Against.Null(validators);
    }

    /// <inheritdoc />
    public void Validate(object target, string contextName)
    {
        var allErrors = new List<string>();

        foreach (var validator in _validators)
        {
            try
            {
                validator.Validate(target, contextName);
            }
            catch (FeatureConfigurationValidationException ex)
            {
                allErrors.AddRange(ex.ValidationErrors);
            }
        }

        if (allErrors.Count > 0)
        {
            throw new FeatureConfigurationValidationException(contextName, allErrors);
        }
    }
}
