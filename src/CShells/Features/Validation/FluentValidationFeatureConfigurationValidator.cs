using System.Reflection;

namespace CShells.Features.Validation;

/// <summary>
/// Validates feature configuration using FluentValidation validators if available.
/// This validator looks for IValidator&lt;T&gt; implementations in the service provider.
/// </summary>
/// <remarks>
/// To use this validator, ensure that FluentValidation is referenced and validators
/// are registered in the dependency injection container.
/// </remarks>
public class FluentValidationFeatureConfigurationValidator : IFeatureConfigurationValidator
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="FluentValidationFeatureConfigurationValidator"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider to resolve validators.</param>
    public FluentValidationFeatureConfigurationValidator(IServiceProvider serviceProvider)
    {
        _serviceProvider = Guard.Against.Null(serviceProvider);
    }

    /// <inheritdoc />
    public void Validate(object target, string contextName)
    {
        Guard.Against.Null(target);
        Guard.Against.Null(contextName);

        var targetType = target.GetType();
        var validatorType = typeof(IValidator<>).MakeGenericType(targetType);

        // Try to resolve a validator for this type
        var validator = _serviceProvider.GetService(validatorType);
        if (validator == null)
        {
            // No validator registered, skip validation
            return;
        }

        // Use reflection to call Validate method
        var validateMethod = validatorType.GetMethod("Validate", new[] { targetType });
        if (validateMethod == null)
        {
            return;
        }

        var result = validateMethod.Invoke(validator, new[] { target });
        if (result == null)
        {
            return;
        }

        // Check if validation failed
        var isValidProperty = result.GetType().GetProperty("IsValid");
        var isValid = (bool?)isValidProperty?.GetValue(result);

        if (isValid == false)
        {
            var errorsProperty = result.GetType().GetProperty("Errors");
            var errors = errorsProperty?.GetValue(result) as System.Collections.IEnumerable;

            var errorMessages = new List<string>();
            if (errors != null)
            {
                foreach (var error in errors)
                {
                    var errorMessageProperty = error.GetType().GetProperty("ErrorMessage");
                    var errorMessage = errorMessageProperty?.GetValue(error) as string;
                    if (!string.IsNullOrEmpty(errorMessage))
                    {
                        errorMessages.Add(errorMessage);
                    }
                }
            }

            throw new FeatureConfigurationValidationException(contextName, errorMessages);
        }
    }

    /// <summary>
    /// Marker interface to detect FluentValidation IValidator&lt;T&gt; at runtime.
    /// </summary>
    private interface IValidator<T>
    {
    }
}
