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