using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using System.Net.Http;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureLayerClientQueryValidationTests
{
    [Fact]
    public async Task QueryAsync_Throws_WhenPageSizeIsZero() {
        var client = CreateClient(_ => throw new InvalidOperationException("HTTP should not be called."));
        var layerClient = client.GetLayerClient(0);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => {
            await foreach (var _ in layerClient.QueryAsync(new FeatureQuery {
                PageSize = 0
            })) {
            }
        });

        Assert.Contains("PageSize", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QueryAsync_Throws_WhenLimitIsZero() {
        var client = CreateClient(_ => throw new InvalidOperationException("HTTP should not be called."));
        var layerClient = client.GetLayerClient(0);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => {
            await foreach (var _ in layerClient.QueryAsync(new FeatureQuery {
                Limit = 0
            })) {
            }
        });

        Assert.Contains("Limit", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QueryCountAsync_Throws_WhenOrderByIsWhitespace() {
        var client = CreateClient(_ => throw new InvalidOperationException("HTTP should not be called."));
        var layerClient = client.GetLayerClient(0);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            layerClient.QueryCountAsync(new FeatureQuery {
                OrderBy = "   "
            }));

        Assert.Contains("OrderBy", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QueryExtentAsync_Throws_WhenOutSridIsZero() {
        var client = CreateClient(_ => throw new InvalidOperationException("HTTP should not be called."));
        var layerClient = client.GetLayerClient(0);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            layerClient.QueryExtentAsync(new FeatureQuery {
                OutSrid = 0
            }));

        Assert.Contains("OutSrid", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QueryAsync_Throws_WhenOutFieldsContainWhitespaceValue() {
        var client = CreateClient(request => {
            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse();
            }

            throw new InvalidOperationException("Feature query endpoint should not be called.");
        });

        var layerClient = client.GetLayerClient(0);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => {
            await foreach (var _ in layerClient.QueryAsync(new FeatureQuery {
                OutFields = ["NAME", " "]
            })) {
            }
        });

        Assert.Contains("OutFields", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static FeatureServiceClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> handler) {
        return new FeatureServiceClient(
            new HttpClient(new StubHttpMessageHandler(handler)),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });
    }

    private static bool IsLayerMetadataRequest(HttpRequestMessage request) {
        return request.RequestUri?.AbsolutePath.EndsWith(
            "/FeatureServer/0",
            StringComparison.OrdinalIgnoreCase) == true;
    }

    private static HttpResponseMessage CreateLayerMetadataResponse() {
        return StubHttpMessageHandler.Json("""
        {
          "id": 0,
          "name": "Layer 0",
          "geometryType": "esriGeometryPoint",
          "advancedQueryCapabilities": {
            "supportsPagination": true
          },
          "fields": [],
          "relationships": []
        }
        """);
    }
}