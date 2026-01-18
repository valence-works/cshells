using CShells.Configuration;
using CShells.Resolution;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CShells.DependencyInjection;

/// <summary>
/// Extension methods for configuring the CShellsBuilder.
/// </summary>
public static class CShellsBuilderExtensions
{
    /// <param name="builder">The CShells builder.</param>
    extension(CShellsBuilder builder)
    {
        /// <summary>
        /// Configures CShells to use a specific shell settings provider.
        /// </summary>
        /// <typeparam name="TProvider">The type of the shell settings provider.</typeparam>
        /// <returns>The updated CShells builder.</returns>
        public CShellsBuilder WithProvider<TProvider>()
            where TProvider : class, IShellSettingsProvider
        {
            Guard.Against.Null(builder);
            builder.Services.AddSingleton<IShellSettingsProvider, TProvider>();
            return builder;
        }

        /// <summary>
        /// Configures CShells to use a specific shell settings provider instance.
        /// </summary>
        /// <param name="provider">The shell settings provider instance.</param>
        /// <returns>The updated CShells builder.</returns>
        public CShellsBuilder WithProvider(IShellSettingsProvider provider)
        {
            Guard.Against.Null(builder);
            Guard.Against.Null(provider);
            builder.Services.AddSingleton(provider);
            return builder;
        }

        /// <summary>
        /// Configures CShells to use a specific shell settings provider factory.
        /// </summary>
        /// <param name="factory">The factory function to create the shell settings provider.</param>
        /// <returns>The updated CShells builder.</returns>
        public CShellsBuilder WithProvider(Func<IServiceProvider, IShellSettingsProvider> factory)
        {
            Guard.Against.Null(builder);
            Guard.Against.Null(factory);
            builder.Services.AddSingleton(factory);
            return builder;
        }

        /// <summary>
        /// Configures CShells to use the configuration-based shell settings provider.
        /// </summary>
        /// <param name="configuration">The application configuration.</param>
        /// <param name="sectionName">The configuration section name (default: "CShells").</param>
        /// <returns>The updated CShells builder.</returns>
        public CShellsBuilder WithConfigurationProvider(IConfiguration configuration,
            string sectionName = CShellsOptions.SectionName)
        {
            Guard.Against.Null(builder);
            Guard.Against.Null(configuration);

            builder.Services.AddSingleton<IShellSettingsProvider>(
                _ => new ConfigurationShellSettingsProvider(configuration, sectionName));

            return builder;
        }

        /// <summary>
        /// Configures the shell resolver strategy pipeline with explicit control over which strategies
        /// are registered and their execution order.
        /// </summary>
        /// <param name="configure">Configuration action for the resolver pipeline.</param>
        /// <returns>The updated CShells builder.</returns>
        /// <remarks>
        /// When this method is called, it replaces the default resolver strategy registration behavior.
        /// Use this for advanced scenarios where you need full control over the resolver pipeline.
        /// For common scenarios, consider using convenience methods like <c>WithWebRouting</c> or <c>WithDefaultResolver</c>.
        /// </remarks>
        /// <example>
        /// <code>
        /// builder.AddCShells(shells => shells
        ///     .ConfigureResolverPipeline(pipeline => pipeline
        ///         .Use&lt;WebRoutingShellResolver&gt;(order: 0)
        ///         .Use&lt;ClaimsBasedResolver&gt;(order: 50)
        ///         .UseFallback&lt;DefaultShellResolverStrategy&gt;()
        ///     )
        /// );
        /// </code>
        /// </example>
        public CShellsBuilder ConfigureResolverPipeline(Action<ResolverPipelineBuilder> configure)
        {
            Guard.Against.Null(builder);
            Guard.Against.Null(configure);

            var pipelineBuilder = new ResolverPipelineBuilder(builder.Services);
            configure(pipelineBuilder);
            pipelineBuilder.Build();

            return builder;
        }

        /// <summary>
        /// Configures the shell resolver to use the default fallback strategy.
        /// This strategy always resolves to a shell with Id "Default".
        /// </summary>
        /// <returns>The updated CShells builder.</returns>
        /// <remarks>
        /// This is a convenience method that configures the resolver pipeline with just the <see cref="DefaultShellResolverStrategy"/>.
        /// It's typically used in non-web scenarios where simple default shell resolution is sufficient.
        /// </remarks>
        public CShellsBuilder WithDefaultResolver()
        {
            Guard.Against.Null(builder);

            return builder.ConfigureResolverPipeline(pipeline => pipeline
                .Use<DefaultShellResolverStrategy>());
        }
    }
}
