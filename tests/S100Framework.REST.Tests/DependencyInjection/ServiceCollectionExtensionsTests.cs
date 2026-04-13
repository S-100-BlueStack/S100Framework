using Microsoft.Extensions.DependencyInjection;
using S100Framework.REST.Abstractions;
using S100Framework.REST.DependencyInjection;
using Xunit;

namespace S100Framework.REST.Tests.DependencyInjection;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddFeatureServiceClient_RegistersClient() {
        var services = new ServiceCollection();

        services.AddFeatureServiceClient(options => {
            options.ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer");
        });

        using var provider = services.BuildServiceProvider();

        var client = provider.GetService<IFeatureServiceClient>();

        Assert.NotNull(client);
    }
}