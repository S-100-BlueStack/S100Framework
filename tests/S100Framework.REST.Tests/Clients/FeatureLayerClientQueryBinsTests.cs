using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureLayerClientQueryBinsTests
{
    [Fact]
    public async Task QueryBinsAsync_SendsBinAndStatisticsParameters() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<Uri>();

        var client = CreateClient(request => {
            requestUris.Add(request.RequestUri!);

            if (IsQueryBinsRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "exceededTransferLimit": false,
                  "features": [
                    {
                      "attributes": {
                        "bin": 1,
                        "lowerBoundary": 0,
                        "upperBoundary": 100,
                        "frequency": 14,
                        "AVG_DEPTH": 22.5
                      }
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var layerClient = client.GetLayerClient(0);

        var result = await layerClient.QueryBinsAsync(
            new QueryBinsRequest {
                Where = "STATUS = 'Active'",
                BinJson = """
                {
                  "type": "fixedIntervalBin",
                  "onField": "DEPTH",
                  "parameters": {
                    "interval": 100,
                    "start": 0,
                    "end": 1000
                  }
                }
                """,
                Statistics = [
                    new StatisticDefinition(
                        OnStatisticField: "DEPTH",
                        OutStatisticFieldName: "AVG_DEPTH",
                        StatisticType: StatisticType.Average)
                ],
                BinOrder = QueryBinsOrder.Descending,
                LowerBoundaryAlias = "lowerBoundary",
                UpperBoundaryAlias = "upperBoundary"
            },
            cancellationToken);

        Assert.False(result.ExceededTransferLimit);

        var row = Assert.Single(result.Rows);
        Assert.Equal(1L, row.Attributes["bin"]);
        Assert.Equal(14L, row.Attributes["frequency"]);
        Assert.Equal(22.5m, row.Attributes["AVG_DEPTH"]);

        var requestUri = Assert.Single(requestUris);
        var query = ParseQuery(requestUri);

        Assert.Equal("json", query["f"]);
        Assert.Equal("STATUS = 'Active'", query["where"]);
        Assert.Equal("DESC", query["binOrder"]);
        Assert.Equal("lowerBoundary", query["lowerBoundaryAlias"]);
        Assert.Equal("upperBoundary", query["upperBoundaryAlias"]);
        Assert.Contains("\"type\": \"fixedIntervalBin\"", query["bin"], StringComparison.Ordinal);
        Assert.Contains("\"outStatisticFieldName\":\"AVG_DEPTH\"", query["outStatistics"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryBinsAsync_MapsStackedAttributes() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsQueryBinsRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "stackFieldNames": [
                    "CATEGORY",
                    "frequency"
                  ],
                  "features": [
                    {
                      "attributes": {
                        "bin": 1,
                        "lowerBoundary": 0,
                        "upperBoundary": 100
                      },
                      "stackedAttributes": [
                        {
                          "CATEGORY": "A",
                          "frequency": 7
                        },
                        {
                          "CATEGORY": "B",
                          "frequency": 3
                        }
                      ]
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var layerClient = client.GetLayerClient(0);

        var result = await layerClient.QueryBinsAsync(
            new QueryBinsRequest {
                BinJson = """
                {
                  "type": "autoIntervalBin",
                  "onField": "DEPTH",
                  "parameters": {
                    "numberOfBins": 2
                  },
                  "stackBy": {
                    "value": "CATEGORY",
                    "type": "field"
                  }
                }
                """
            },
            cancellationToken);

        Assert.Equal(["CATEGORY", "frequency"], result.StackFieldNames);

        var row = Assert.Single(result.Rows);
        Assert.Equal(2, row.StackedAttributes.Count);
        Assert.Equal("A", row.StackedAttributes[0]["CATEGORY"]);
        Assert.Equal(7L, row.StackedAttributes[0]["frequency"]);
        Assert.Equal("B", row.StackedAttributes[1]["CATEGORY"]);
        Assert.Equal(3L, row.StackedAttributes[1]["frequency"]);
    }

    [Fact]
    public async Task QueryBinsAsync_ReturnsEmptyRows_WhenFeaturesPropertyIsMissing() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsQueryBinsRequest(request)) {
                return StubHttpMessageHandler.Json("{}");
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var result = await client.GetLayerClient(0).QueryBinsAsync(
            new QueryBinsRequest {
                BinJson = """
                {
                  "type": "autoIntervalBin",
                  "onField": "DEPTH",
                  "parameters": {
                    "numberOfBins": 2
                  }
                }
                """
            },
            cancellationToken);

        Assert.Empty(result.Rows);
        Assert.Empty(result.StackFieldNames);
        Assert.Null(result.ExceededTransferLimit);
    }

    [Fact]
    public async Task QueryBinsAsync_ReturnsEmptyRows_WhenFeaturesPropertyIsNull() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsQueryBinsRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "features": null,
                  "stackFieldNames": null
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var result = await client.GetLayerClient(0).QueryBinsAsync(
            new QueryBinsRequest {
                BinJson = """
                {
                  "type": "autoIntervalBin",
                  "onField": "DEPTH",
                  "parameters": {
                    "numberOfBins": 2
                  }
                }
                """
            },
            cancellationToken);

        Assert.Empty(result.Rows);
        Assert.Empty(result.StackFieldNames);
    }

    [Fact]
    public async Task QueryBinsAsync_IgnoresNullFeatureItems() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsQueryBinsRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "features": [
                    null,
                    {
                      "attributes": {
                        "bin": 1,
                        "frequency": 14
                      }
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var result = await client.GetLayerClient(0).QueryBinsAsync(
            new QueryBinsRequest {
                BinJson = """
                {
                  "type": "autoIntervalBin",
                  "onField": "DEPTH",
                  "parameters": {
                    "numberOfBins": 2
                  }
                }
                """
            },
            cancellationToken);

        var row = Assert.Single(result.Rows);

        Assert.Equal(1L, row.Attributes["bin"]);
        Assert.Equal(14L, row.Attributes["frequency"]);
    }

    [Fact]
    public async Task QueryBinsAsync_FiltersBlankStackFieldNamesAndNullStackedAttributes() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsQueryBinsRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "stackFieldNames": [
                    null,
                    "",
                    "   ",
                    "CATEGORY",
                    "frequency"
                  ],
                  "features": [
                    {
                      "attributes": {
                        "bin": 1
                      },
                      "stackedAttributes": [
                        null,
                        {
                          "CATEGORY": "A",
                          "frequency": 7
                        }
                      ]
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var result = await client.GetLayerClient(0).QueryBinsAsync(
            new QueryBinsRequest {
                BinJson = """
                {
                  "type": "autoIntervalBin",
                  "onField": "DEPTH",
                  "parameters": {
                    "numberOfBins": 2
                  },
                  "stackBy": {
                    "value": "CATEGORY",
                    "type": "field"
                  }
                }
                """
            },
            cancellationToken);

        Assert.Equal(["CATEGORY", "frequency"], result.StackFieldNames);

        var row = Assert.Single(result.Rows);
        var stackedAttributes = Assert.Single(row.StackedAttributes);

        Assert.Equal("A", stackedAttributes["CATEGORY"]);
        Assert.Equal(7L, stackedAttributes["frequency"]);
    }

    [Fact]
    public async Task QueryBinsAsync_ReturnsEmptyAttributes_WhenFeatureOmitsAttributes() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsQueryBinsRequest(request)) {
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

        var result = await client.GetLayerClient(0).QueryBinsAsync(
            new QueryBinsRequest {
                BinJson = """
                {
                  "type": "autoIntervalBin",
                  "onField": "DEPTH",
                  "parameters": {
                    "numberOfBins": 2
                  }
                }
                """
            },
            cancellationToken);

        var row = Assert.Single(result.Rows);

        Assert.Empty(row.Attributes);
        Assert.Empty(row.StackedAttributes);
    }

    [Fact]
    public async Task QueryBinsAsync_Throws_WhenBinJsonIsInvalid() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var layerClient = client.GetLayerClient(0);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            layerClient.QueryBinsAsync(
                new QueryBinsRequest {
                    BinJson = "not-json"
                },
                cancellationToken));

        Assert.Contains("BinJson", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryBinsAsync_Throws_WhenBinOrderIsInvalid() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetLayerClient(0).QueryBinsAsync(
                new QueryBinsRequest {
                    BinJson = """
                {
                  "type": "autoIntervalBin",
                  "onField": "DEPTH",
                  "parameters": {
                    "numberOfBins": 2
                  }
                }
                """,
                    BinOrder = (QueryBinsOrder)999
                },
                cancellationToken));

        Assert.Contains("BinOrder", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryBinsAsync_Throws_WhenStatisticsContainNullValue() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetLayerClient(0).QueryBinsAsync(
                new QueryBinsRequest {
                    BinJson = """
                {
                  "type": "autoIntervalBin",
                  "onField": "DEPTH",
                  "parameters": {
                    "numberOfBins": 2
                  }
                }
                """,
                    Statistics = [
                        null!
                    ]
                },
                cancellationToken));

        Assert.Contains("Statistics", exception.Message, StringComparison.Ordinal);
        Assert.Contains("null", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QueryBinsAsync_Throws_WhenStatisticTypeIsInvalid() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetLayerClient(0).QueryBinsAsync(
                new QueryBinsRequest {
                    BinJson = """
                {
                  "type": "autoIntervalBin",
                  "onField": "DEPTH",
                  "parameters": {
                    "numberOfBins": 2
                  }
                }
                """,
                    Statistics = [
                        new StatisticDefinition(
                        OnStatisticField: "DEPTH",
                        OutStatisticFieldName: "BROKEN_STAT",
                        StatisticType: (StatisticType)999)
                    ]
                },
                cancellationToken));

        Assert.Contains("StatisticType", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryBinsAsync_Throws_WhenPercentileOrderByIsInvalid() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetLayerClient(0).QueryBinsAsync(
                new QueryBinsRequest {
                    BinJson = """
                {
                  "type": "autoIntervalBin",
                  "onField": "DEPTH",
                  "parameters": {
                    "numberOfBins": 2
                  }
                }
                """,
                    Statistics = [
                        new StatisticDefinition(
                        OnStatisticField: "DEPTH",
                        OutStatisticFieldName: "P90_DEPTH",
                        StatisticType: StatisticType.PercentileContinuous,
                        PercentileParameters: new StatisticPercentileParameters(
                            0.9,
                            (StatisticPercentileOrder)999))
                    ]
                },
                cancellationToken));

        Assert.Contains("OrderBy", exception.Message, StringComparison.Ordinal);
        Assert.Contains("percentile order", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static FeatureServiceClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> handler) {
        return new FeatureServiceClient(
            new HttpClient(new StubHttpMessageHandler(handler)),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });
    }

    private static bool IsQueryBinsRequest(HttpRequestMessage request) {
        return request.RequestUri?.AbsolutePath.EndsWith(
            "/FeatureServer/0/queryBins",
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