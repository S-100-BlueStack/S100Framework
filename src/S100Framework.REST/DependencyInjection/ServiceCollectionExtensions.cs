using Microsoft.Extensions.DependencyInjection;
using S100Framework.REST.Abstractions;
using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;

namespace S100Framework.REST.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFeatureServiceClient(
        this IServiceCollection services,
        Action<FeatureServiceClientOptions> configure) {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new FeatureServiceClientOptions();
        configure(options);
        options.Validate();

        services.AddSingleton(options);

        services.AddHttpClient<IFeatureServiceClient, FeatureServiceClient>(client => {
            // The library controls request timeout per operation.
            // Leaving HttpClient.Timeout infinite avoids double-timeout behavior.
            client.Timeout = Timeout.InfiniteTimeSpan;
        });

        return services;
    }
}