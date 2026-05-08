using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureLayerClientQueryDateBinsTests
{
    [Fact]
    public async Task QueryDateBinsAsync_SendsDateBinParameters() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<Uri>();

        var client = CreateClient(request => {
            requestUris.Add(request.RequestUri!);

            if (IsQueryDateBinsRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "exceededTransferLimit": false,
                  "features": [
                    {
                      "attributes": {
                        "boundary": 1609459200000,
                        "item_count": 79,
                        "avg_value": 300.4
                      }
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var layerClient = client.GetLayerClient(0);

        var result = await layerClient.QueryDateBinsAsync(
            new QueryDateBinsRequest {
                BinField = "created_at",
                BinJson = """
                {
                  "calendarBin": {
                    "unit": "day",
                    "timezone": "Europe/Copenhagen"
                  }
                }
                """,
                Statistics = [
                    new StatisticDefinition(
                        OnStatisticField: "OBJECTID",
                        OutStatisticFieldName: "item_count",
                        StatisticType: StatisticType.Count),
                    new StatisticDefinition(
                        OnStatisticField: "VALUE",
                        OutStatisticFieldName: "avg_value",
                        StatisticType: StatisticType.Average)
                ],
                Where = "STATUS = 'Active'",
                TimeExtent = new FeatureTimeExtent(
                    Start: new DateTimeOffset(2021, 01, 01, 0, 0, 0, TimeSpan.Zero),
                    End: new DateTimeOffset(2021, 02, 01, 0, 0, 0, TimeSpan.Zero)),
                BinOrder = QueryBinsOrder.Descending,
                ReturnCentroid = false,
                ResultOffset = 10,
                ResultRecordCount = 20,
                ReturnExceededLimitFeatures = false,
                BinBoundaryAlias = "boundary"
            },
            cancellationToken);

        Assert.False(result.ExceededTransferLimit);

        var row = Assert.Single(result.Rows);
        Assert.Equal(1609459200000L, row.Attributes["boundary"]);
        Assert.Equal(79L, row.Attributes["item_count"]);
        Assert.Equal(300.4m, row.Attributes["avg_value"]);
        Assert.Null(row.Centroid);

        var requestUri = Assert.Single(requestUris);
        var query = ParseQuery(requestUri);

        Assert.Equal("json", query["f"]);
        Assert.Equal("created_at", query["binField"]);
        Assert.Equal("STATUS = 'Active'", query["where"]);
        Assert.Equal("1609459200000,1612137600000", query["time"]);
        Assert.Equal("DESC", query["binOrder"]);
        Assert.Equal("false", query["returnCentroid"]);
        Assert.Equal("10", query["resultOffset"]);
        Assert.Equal("20", query["resultRecordCount"]);
        Assert.Equal("false", query["returnExceededLimitFeatures"]);
        Assert.Equal("boundary", query["binBoundaryAlias"]);
        Assert.Contains("\"calendarBin\"", query["bin"], StringComparison.Ordinal);
        Assert.Contains("\"unit\": \"day\"", query["bin"], StringComparison.Ordinal);
        Assert.Contains("\"outStatisticFieldName\":\"item_count\"", query["outStatistics"], StringComparison.Ordinal);
        Assert.Contains("\"outStatisticFieldName\":\"avg_value\"", query["outStatistics"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryDateBinsAsync_MapsCentroid_WhenReturned() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsQueryDateBinsRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "geometryType": "esriGeometryPoint",
                  "features": [
                    {
                      "centroid": {
                        "x": 12.5,
                        "y": 55.7
                      },
                      "attributes": {
                        "boundary": 1609459200000,
                        "item_count": 42
                      }
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var layerClient = client.GetLayerClient(0);

        var result = await layerClient.QueryDateBinsAsync(
            new QueryDateBinsRequest {
                BinField = "created_at",
                BinJson = """
                {
                  "calendarBin": {
                    "unit": "month"
                  }
                }
                """,
                Statistics = [
                    new StatisticDefinition(
                        OnStatisticField: "OBJECTID",
                        OutStatisticFieldName: "item_count",
                        StatisticType: StatisticType.Count)
                ],
                ReturnCentroid = true
            },
            cancellationToken);

        Assert.Equal("esriGeometryPoint", result.GeometryType);

        var row = Assert.Single(result.Rows);
        Assert.NotNull(row.Centroid);
        Assert.Equal(12.5m, row.Centroid!["x"]);
        Assert.Equal(55.7m, row.Centroid["y"]);
    }

    [Fact]
    public async Task QueryDateBinsAsync_ReturnsEmptyRows_WhenFeaturesPropertyIsMissing() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsQueryDateBinsRequest(request)) {
                return StubHttpMessageHandler.Json("{}");
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var result = await client.GetLayerClient(0).QueryDateBinsAsync(
            CreateMinimalRequest(),
            cancellationToken);

        Assert.Empty(result.Rows);
        Assert.Null(result.ExceededTransferLimit);
        Assert.Null(result.GeometryType);
    }

    [Fact]
    public async Task QueryDateBinsAsync_ReturnsEmptyRows_WhenFeaturesPropertyIsNull() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsQueryDateBinsRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "features": null
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var result = await client.GetLayerClient(0).QueryDateBinsAsync(
            CreateMinimalRequest(),
            cancellationToken);

        Assert.Empty(result.Rows);
    }

    [Fact]
    public async Task QueryDateBinsAsync_IgnoresNullFeatureItems() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsQueryDateBinsRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "features": [
                    null,
                    {
                      "attributes": {
                        "boundary": 1609459200000,
                        "item_count": 79
                      }
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var result = await client.GetLayerClient(0).QueryDateBinsAsync(
            CreateMinimalRequest(),
            cancellationToken);

        var row = Assert.Single(result.Rows);

        Assert.Equal(1609459200000L, row.Attributes["boundary"]);
        Assert.Equal(79L, row.Attributes["item_count"]);
    }

    [Fact]
    public async Task QueryDateBinsAsync_ReturnsEmptyAttributes_WhenFeatureOmitsAttributes() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsQueryDateBinsRequest(request)) {
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

        var result = await client.GetLayerClient(0).QueryDateBinsAsync(
            CreateMinimalRequest(),
            cancellationToken);

        var row = Assert.Single(result.Rows);

        Assert.Empty(row.Attributes);
        Assert.Null(row.Centroid);
    }

    [Fact]
    public async Task QueryDateBinsAsync_IgnoresNullCentroid() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsQueryDateBinsRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "features": [
                    {
                      "attributes": {
                        "boundary": 1609459200000
                      },
                      "centroid": null
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var result = await client.GetLayerClient(0).QueryDateBinsAsync(
            CreateMinimalRequest(),
            cancellationToken);

        var row = Assert.Single(result.Rows);

        Assert.Equal(1609459200000L, row.Attributes["boundary"]);
        Assert.Null(row.Centroid);
    }

    [Fact]
    public async Task QueryDateBinsAsync_IgnoresNonObjectCentroid() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsQueryDateBinsRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "features": [
                    {
                      "attributes": {
                        "boundary": 1609459200000
                      },
                      "centroid": "not-a-centroid-object"
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var result = await client.GetLayerClient(0).QueryDateBinsAsync(
            CreateMinimalRequest(),
            cancellationToken);

        var row = Assert.Single(result.Rows);

        Assert.Equal(1609459200000L, row.Attributes["boundary"]);
        Assert.Null(row.Centroid);
    }

    [Fact]
    public async Task QueryDateBinsAsync_Throws_WhenBinFieldIsEmpty() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var layerClient = client.GetLayerClient(0);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            layerClient.QueryDateBinsAsync(
                new QueryDateBinsRequest {
                    BinField = " ",
                    BinJson = """
                    {
                      "calendarBin": {
                        "unit": "day"
                      }
                    }
                    """,
                    Statistics = [
                        new StatisticDefinition(
                            OnStatisticField: "OBJECTID",
                            OutStatisticFieldName: "item_count",
                            StatisticType: StatisticType.Count)
                    ]
                },
                cancellationToken));

        Assert.Contains("BinField", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryDateBinsAsync_Throws_WhenStatisticsAreMissing() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var layerClient = client.GetLayerClient(0);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            layerClient.QueryDateBinsAsync(
                new QueryDateBinsRequest {
                    BinField = "created_at",
                    BinJson = """
                    {
                      "calendarBin": {
                        "unit": "day"
                      }
                    }
                    """
                },
                cancellationToken));

        Assert.Contains("statistic", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QueryDateBinsAsync_Throws_WhenBinOrderIsInvalid() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetLayerClient(0).QueryDateBinsAsync(
                CreateMinimalRequest() with {
                    BinOrder = (QueryBinsOrder)999
                },
                cancellationToken));

        Assert.Contains("BinOrder", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryDateBinsAsync_Throws_WhenStatisticsContainNullValue() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetLayerClient(0).QueryDateBinsAsync(
                CreateMinimalRequest() with {
                    Statistics = [
                        null!
                    ]
                },
                cancellationToken));

        Assert.Contains("Statistics", exception.Message, StringComparison.Ordinal);
        Assert.Contains("null", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QueryDateBinsAsync_Throws_WhenStatisticTypeIsInvalid() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetLayerClient(0).QueryDateBinsAsync(
                CreateMinimalRequest() with {
                    Statistics = [
                        new StatisticDefinition(
                        OnStatisticField: "OBJECTID",
                        OutStatisticFieldName: "BROKEN_STAT",
                        StatisticType: (StatisticType)999)
                    ]
                },
                cancellationToken));

        Assert.Contains("StatisticType", exception.Message, StringComparison.Ordinal);
    }

    private static QueryDateBinsRequest CreateMinimalRequest() {
        return new QueryDateBinsRequest {
            BinField = "created_at",
            BinJson = """
            {
              "calendarBin": {
                "unit": "day"
              }
            }
            """,
            Statistics = [
                new StatisticDefinition(
                    OnStatisticField: "OBJECTID",
                    OutStatisticFieldName: "item_count",
                    StatisticType: StatisticType.Count)
            ]
        };
    }

    private static FeatureServiceClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> handler) {
        return new FeatureServiceClient(
            new HttpClient(new StubHttpMessageHandler(handler)),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });
    }

    private static bool IsQueryDateBinsRequest(HttpRequestMessage request) {
        return request.RequestUri?.AbsolutePath.EndsWith(
            "/FeatureServer/0/queryDateBins",
            StringComparison.OrdinalIgnoreCase) == true;
    }

    private static Dictionary<string, string> ParseQuery(Uri uri) {
        return uri.Query
            .TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .ToDictionary(
                parts => Uri.UnescapeDataString(parts[0]),
                parts => parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty,
                StringComparer.Ordinal);
    }
}