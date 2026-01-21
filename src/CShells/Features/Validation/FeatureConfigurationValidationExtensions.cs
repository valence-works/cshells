using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CShells.Features.Validation;

/// <summary>
/// Extension methods for registering feature configuration validators.
/// </summary>
public static class FeatureConfigurationValidationExtensions
{
    /// <summary>
    /// Adds DataAnnotations-based feature configuration validation.
    /// This is the default validator and is registered automatically if no validator is configured.
    /// </summary>
    public static IServiceCollection AddDataAnnotationsFeatureValidation(this IServiceCollection services)
    {
        services.TryAddSingleton<IFeatureConfigurationValidator, DataAnnotationsFeatureConfigurationValidator>();
        return services;
    }

    /// <summary>
    /// Adds FluentValidation-based feature configuration validation.
    /// Requires FluentValidation to be referenced and validators to be registered.
    /// </summary>
    public static IServiceCollection AddFluentFeatureValidation(this IServiceCollection services)
    {
        services.TryAddSingleton<IFeatureConfigurationValidator, FluentValidationFeatureConfigurationValidator>();
        return services;
    }

    /// <summary>
    /// Adds multiple validators that will be executed in sequence.
    /// This allows combining DataAnnotations, FluentValidation, and custom validators.
    /// </summary>
    /// <example>
    /// <code>
    /// services.AddCompositeFeatureValidation(validators =>
    /// {
    ///     validators.Add(new DataAnnotationsFeatureConfigurationValidator());
    ///     validators.Add(sp => new FluentValidationFeatureConfigurationValidator(sp));
    ///     validators.Add(new CustomValidator());
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddCompositeFeatureValidation(
        this IServiceCollection services,
        Action<CompositeFeatureValidationBuilder> configure)
    {
        Guard.Against.Null(configure);

        var builder = new CompositeFeatureValidationBuilder(services);
        configure(builder);

        services.TryAddSingleton<IFeatureConfigurationValidator>(sp =>
        {
            var validators = builder.Build(sp);
            return new CompositeFeatureConfigurationValidator(validators);
        });

        return services;
    }

    /// <summary>
    /// Adds a custom feature configuration validator.
    /// </summary>
    public static IServiceCollection AddCustomFeatureValidation<TValidator>(this IServiceCollection services)
        where TValidator : class, IFeatureConfigurationValidator
    {
        services.TryAddSingleton<IFeatureConfigurationValidator, TValidator>();
        return services;
    }
}

/// <summary>
/// Builder for composite feature validation configuration.
/// </summary>
public class CompositeFeatureValidationBuilder
{
    private readonly IServiceCollection _services;
    private readonly List<Func<IServiceProvider, IFeatureConfigurationValidator>> _validatorFactories = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="CompositeFeatureValidationBuilder"/> class.
    /// </summary>
    internal CompositeFeatureValidationBuilder(IServiceCollection services)
    {
        _services = services;
    }

    /// <summary>
    /// Adds a validator instance.
    /// </summary>
    public CompositeFeatureValidationBuilder Add(IFeatureConfigurationValidator validator)
    {
        Guard.Against.Null(validator);
        _validatorFactories.Add(_ => validator);
        return this;
    }

    /// <summary>
    /// Adds a validator using a factory function that receives the service provider.
    /// </summary>
    public CompositeFeatureValidationBuilder Add(Func<IServiceProvider, IFeatureConfigurationValidator> factory)
    {
        Guard.Against.Null(factory);
        _validatorFactories.Add(factory);
        return this;
    }

    /// <summary>
    /// Adds a validator of the specified type, resolved from the service provider.
    /// </summary>
    public CompositeFeatureValidationBuilder Add<TValidator>()
        where TValidator : class, IFeatureConfigurationValidator
    {
        _services.TryAddSingleton<TValidator>();
        _validatorFactories.Add(sp => sp.GetRequiredService<TValidator>());
        return this;
    }

    /// <summary>
    /// Builds the list of validators.
    /// </summary>
    internal IEnumerable<IFeatureConfigurationValidator> Build(IServiceProvider serviceProvider)
    {
        return _validatorFactories.Select(factory => factory(serviceProvider)).ToList();
    }
}
