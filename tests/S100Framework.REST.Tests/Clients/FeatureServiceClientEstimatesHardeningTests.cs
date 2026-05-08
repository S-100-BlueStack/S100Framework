using System.Text.Json;
using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Exceptions;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureServiceClientEstimatesHardeningTests
{
    [Fact]
    public async Task GetEstimatesAsync_ReturnsEmptyList_WhenLayerEstimatesPropertyIsMissing() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceGetEstimatesRequest(request)) {
                return StubHttpMessageHandler.Json("{}");
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var estimates = await client.GetEstimatesAsync(cancellationToken);

        Assert.Empty(estimates);
    }

    [Fact]
    public async Task GetEstimatesAsync_ReturnsEmptyList_WhenLayerEstimatesPropertyIsNull() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceGetEstimatesRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "layerEstimates": null
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var estimates = await client.GetEstimatesAsync(cancellationToken);

        Assert.Empty(estimates);
    }

    [Fact]
    public async Task GetEstimatesAsync_IgnoresNullLayerEstimateItems() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceGetEstimatesRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "layerEstimates": [
                    null,
                    {
                      "layerId": 2,
                      "count": 15
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var estimate = Assert.Single(await client.GetEstimatesAsync(cancellationToken));

        Assert.Equal(2, estimate.LayerId);
        Assert.Equal(15, estimate.Count);
        Assert.Null(estimate.Extent);
    }

    [Fact]
    public async Task GetEstimatesAsync_Throws_WhenLayerEstimateOmitsLayerId() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceGetEstimatesRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "layerEstimates": [
                    {
                      "count": 15
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceException>(() =>
            client.GetEstimatesAsync(cancellationToken));

        Assert.Contains("layer ID", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetEstimatesAsync_ReturnsNullExtent_WhenExtentIsPartial() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceGetEstimatesRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "layerEstimates": [
                    {
                      "layerId": 2,
                      "count": 15,
                      "extent": {
                        "xmin": 1,
                        "ymin": 2,
                        "xmax": 3
                      }
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var estimate = Assert.Single(await client.GetEstimatesAsync(cancellationToken));

        Assert.Equal(2, estimate.LayerId);
        Assert.Equal(15, estimate.Count);
        Assert.Null(estimate.Extent);
    }

    [Fact]
    public async Task LayerGetEstimatesAsync_ReturnsNullExtent_WhenExtentIsPartial() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsLayerGetEstimatesRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "count": 931,
                  "extent": {
                    "xmin": 10,
                    "ymin": 20,
                    "xmax": 30
                  }
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var estimate = await client.GetLayerClient(0).GetEstimatesAsync(cancellationToken);

        Assert.Equal(0, estimate.LayerId);
        Assert.Equal(931, estimate.Count);
        Assert.Null(estimate.Extent);
    }

    [Fact]
    public async Task LayerGetEstimatesAsync_AllowsMissingCountAndExtent() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsLayerGetEstimatesRequest(request)) {
                return StubHttpMessageHandler.Json("{}");
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var estimate = await client.GetLayerClient(0).GetEstimatesAsync(cancellationToken);

        Assert.Equal(0, estimate.LayerId);
        Assert.Null(estimate.Count);
        Assert.Null(estimate.Extent);
    }

    private static FeatureServiceClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> handler) {
        return new FeatureServiceClient(
            new HttpClient(new StubHttpMessageHandler(handler)),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });
    }

    private static bool IsServiceGetEstimatesRequest(HttpRequestMessage request) {
        return request.RequestUri?.AbsolutePath.EndsWith(
            "/FeatureServer/getEstimates",
            StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool IsLayerGetEstimatesRequest(HttpRequestMessage request) {
        return request.RequestUri?.AbsolutePath.EndsWith(
            "/FeatureServer/0/getEstimates",
            StringComparison.OrdinalIgnoreCase) == true;
    }
}