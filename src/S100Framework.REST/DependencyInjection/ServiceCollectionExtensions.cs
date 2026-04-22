using Microsoft.Extensions.DependencyInjection;

using S100Framework.REST.Abstractions;

using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;

namespace S100Framework.REST.DependencyInjection;

/// <summary>
/// Provides dependency injection registration helpers for <c>S100Framework.REST</c>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers a configured <see cref="IFeatureServiceClient" /> in the service collection.
    /// </summary>
    /// <param name="services">
    /// The service collection to register into.
    /// </param>
    /// <param name="configure">
    /// A callback used to configure <see cref="FeatureServiceClientOptions" />.
    /// </param>
    /// <returns>
    /// The same <see cref="IServiceCollection" /> instance for chaining.
    /// </returns>
    /// <remarks>
    /// The library manages request timeouts per operation, so the underlying named
    /// <see cref="HttpClient" /> is configured with an infinite timeout to avoid
    /// overlapping timeout behavior.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="services" /> or <paramref name="configure" /> is
    /// <see langword="null" />.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the configured options are invalid.
    /// </exception>
    public static IServiceCollection AddFeatureServiceClient(
        this IServiceCollection services,
        Action<FeatureServiceClientOptions> configure) {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new FeatureServiceClientOptions();

        configure(options);

        options.Validate();
        services.AddSingleton(options);

        services.AddHttpClient(nameof(IFeatureServiceClient), client => {
            // The library controls request timeout per operation.
            // Leaving HttpClient.Timeout infinite avoids overlapping timeout behavior.
            client.Timeout = Timeout.InfiniteTimeSpan;
        });

        services.AddTransient<IFeatureServiceClient>(sp => {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient(nameof(IFeatureServiceClient));
            var authorizer = sp.GetService<IFeatureServiceRequestAuthorizer>();

            return new FeatureServiceClient(httpClient, options, authorizer);
        });

        return services;
    }
}