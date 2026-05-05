using S100Framework.REST.Abstractions;
using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureLayerClientStatisticsHardeningTests
{
    [Fact]
    public async Task QueryStatisticsAsync_ReturnsEmptyRows_WhenFeaturesPropertyIsMissing() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var layerClient = CreateLayerClient(request => {
            if (IsQueryRequest(request)) {
                return StubHttpMessageHandler.Json("{}");
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var rows = await layerClient.QueryStatisticsAsync(
            CreateQuery(),
            cancellationToken);

        Assert.Empty(rows);
    }

    [Fact]
    public async Task QueryStatisticsAsync_ReturnsEmptyRows_WhenFeaturesPropertyIsNull() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var layerClient = CreateLayerClient(request => {
            if (IsQueryRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "features": null
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var rows = await layerClient.QueryStatisticsAsync(
            CreateQuery(),
            cancellationToken);

        Assert.Empty(rows);
    }

    [Fact]
    public async Task QueryStatisticsAsync_IgnoresNullFeatureItems() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var layerClient = CreateLayerClient(request => {
            if (IsQueryRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "features": [
                    null,
                    {
                      "attributes": {
                        "PLANNAME": "Plan A",
                        "PLAN_COUNT": 2
                      }
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var rows = await layerClient.QueryStatisticsAsync(
            CreateQuery(),
            cancellationToken);

        var row = Assert.Single(rows);

        Assert.Equal("Plan A", row.GetRequiredString("PLANNAME"));
        Assert.Equal(2L, row.GetRequiredInt64("PLAN_COUNT"));
    }

    [Fact]
    public async Task QueryStatisticsAsync_ReturnsEmptyAttributes_WhenFeatureOmitsAttributes() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var layerClient = CreateLayerClient(request => {
            if (IsQueryRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "features": [
                    {
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var rows = await layerClient.QueryStatisticsAsync(
            CreateQuery(),
            cancellationToken);

        var row = Assert.Single(rows);

        Assert.Empty(row.Attributes);
    }

    [Fact]
    public async Task QueryStatisticsAsync_Throws_WhenStatisticsCollectionIsNull() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var layerClient = CreateLayerClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            layerClient.QueryStatisticsAsync(
                new FeatureStatisticsQuery {
                    Statistics = null!
                },
                cancellationToken));

        Assert.Contains("statistic definition", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QueryStatisticsAsync_Throws_WhenStatisticsContainNullValue() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var layerClient = CreateLayerClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            layerClient.QueryStatisticsAsync(
                new FeatureStatisticsQuery {
                    Statistics = [
                        null!
                    ]
                },
                cancellationToken));

        Assert.Contains("Statistics", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryStatisticsAsync_Throws_WhenStatisticTypeIsInvalid() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var layerClient = CreateLayerClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            layerClient.QueryStatisticsAsync(
                new FeatureStatisticsQuery {
                    Statistics = [
                        new StatisticDefinition(
                            "OBJECTID",
                            "PLAN_COUNT",
                            (StatisticType)999)
                    ]
                },
                cancellationToken));

        Assert.Contains("StatisticType", exception.Message, StringComparison.Ordinal);
    }

    private static FeatureStatisticsQuery CreateQuery() {
        return new FeatureStatisticsQuery {
            GroupByFields = ["PLANNAME"],
            Statistics = [
                new StatisticDefinition(
                    "OBJECTID",
                    "PLAN_COUNT",
                    StatisticType.Count)
            ]
        };
    }

    private static IFeatureLayerClient CreateLayerClient(
        Func<HttpRequestMessage, HttpResponseMessage> handler) {
        return new FeatureServiceClient(
            new HttpClient(new StubHttpMessageHandler(handler)),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            }).GetLayerClient(0);
    }

    private static bool IsQueryRequest(HttpRequestMessage request) {
        return request.RequestUri?.AbsolutePath.EndsWith(
            "/FeatureServer/0/query",
            StringComparison.OrdinalIgnoreCase) == true;
    }
}