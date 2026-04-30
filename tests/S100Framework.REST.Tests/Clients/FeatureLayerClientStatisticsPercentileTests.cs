using System.Net.Http;
using System.Text.Json;
using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Exceptions;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureLayerClientStatisticsPercentileTests
{
    [Fact]
    public async Task QueryStatisticsAsync_SendsPercentileContinuousParameters_WhenSupported() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<string>();

        var client = CreateClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;
            requestUris.Add(uri);

            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse(supportsPercentileStatistics: true);
            }

            if (IsLayerQueryRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "features": [
                    {
                      "attributes": {
                        "P90_DEPTH": 123.45
                      }
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        var rows = await client.GetLayerClient(0).QueryStatisticsAsync(
            new FeatureStatisticsQuery {
                Statistics = [
                    new StatisticDefinition(
                        "DEPTH",
                        "P90_DEPTH",
                        StatisticType.PercentileContinuous,
                        new StatisticPercentileParameters(
                            0.9,
                            StatisticPercentileOrder.Desc))
                ]
            },
            cancellationToken);

        var row = Assert.Single(rows);
        Assert.Equal(123.45, row.GetRequiredDouble("P90_DEPTH"));

        var queryRequest = Assert.Single(
            requestUris,
            uri => uri.Contains("/FeatureServer/0/query?", StringComparison.OrdinalIgnoreCase));

        var outStatisticsJson = GetDecodedQueryValue(queryRequest, "outStatistics");

        using var document = JsonDocument.Parse(outStatisticsJson);
        var statistic = Assert.Single(document.RootElement.EnumerateArray());

        Assert.Equal("PERCENTILE_CONT", statistic.GetProperty("statisticType").GetString());
        Assert.Equal("DEPTH", statistic.GetProperty("onStatisticField").GetString());
        Assert.Equal("P90_DEPTH", statistic.GetProperty("outStatisticFieldName").GetString());

        var parameters = statistic.GetProperty("statisticParameters");
        Assert.Equal(0.9, parameters.GetProperty("value").GetDouble());
        Assert.Equal("DESC", parameters.GetProperty("orderBy").GetString());
    }

    [Fact]
    public async Task QueryStatisticsAsync_SendsPercentileDiscreteParameters_WhenSupported() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<string>();

        var client = CreateClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;
            requestUris.Add(uri);

            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse(supportsPercentileStatistics: true);
            }

            if (IsLayerQueryRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "features": [
                    {
                      "attributes": {
                        "P50_DEPTH": 77.0
                      }
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        var rows = await client.GetLayerClient(0).QueryStatisticsAsync(
            new FeatureStatisticsQuery {
                Statistics = [
                    new StatisticDefinition(
                        "DEPTH",
                        "P50_DEPTH",
                        StatisticType.PercentileDiscrete,
                        new StatisticPercentileParameters(0.5))
                ]
            },
            cancellationToken);

        var row = Assert.Single(rows);
        Assert.Equal(77.0, row.GetRequiredDouble("P50_DEPTH"));

        var queryRequest = Assert.Single(
            requestUris,
            uri => uri.Contains("/FeatureServer/0/query?", StringComparison.OrdinalIgnoreCase));

        var outStatisticsJson = GetDecodedQueryValue(queryRequest, "outStatistics");

        using var document = JsonDocument.Parse(outStatisticsJson);
        var statistic = Assert.Single(document.RootElement.EnumerateArray());

        Assert.Equal("PERCENTILE_DISC", statistic.GetProperty("statisticType").GetString());

        var parameters = statistic.GetProperty("statisticParameters");
        Assert.Equal(0.5, parameters.GetProperty("value").GetDouble());
        Assert.Equal("ASC", parameters.GetProperty("orderBy").GetString());
    }

    [Fact]
    public async Task QueryStatisticsAsync_Throws_WhenPercentileStatisticsAreNotSupported() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<string>();

        var client = CreateClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;
            requestUris.Add(uri);

            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse(supportsPercentileStatistics: false);
            }

            throw new InvalidOperationException("Statistics query should not be executed.");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceCapabilityException>(() =>
            client.GetLayerClient(0).QueryStatisticsAsync(
                new FeatureStatisticsQuery {
                    Statistics = [
                        new StatisticDefinition(
                            "DEPTH",
                            "P90_DEPTH",
                            StatisticType.PercentileContinuous,
                            new StatisticPercentileParameters(0.9))
                    ]
                },
                cancellationToken));

        Assert.Contains("percentile statistics", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single(requestUris);
    }

    [Fact]
    public async Task QueryStatisticsAsync_Throws_WhenPercentileParametersAreMissing() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetLayerClient(0).QueryStatisticsAsync(
                new FeatureStatisticsQuery {
                    Statistics = [
                        new StatisticDefinition(
                            "DEPTH",
                            "P90_DEPTH",
                            StatisticType.PercentileContinuous)
                    ]
                },
                cancellationToken));

        Assert.Contains("PercentileParameters", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryStatisticsAsync_Throws_WhenPercentileValueIsOutsideAllowedRange() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetLayerClient(0).QueryStatisticsAsync(
                new FeatureStatisticsQuery {
                    Statistics = [
                        new StatisticDefinition(
                            "DEPTH",
                            "P90_DEPTH",
                            StatisticType.PercentileContinuous,
                            new StatisticPercentileParameters(1.1))
                    ]
                },
                cancellationToken));

        Assert.Contains("between 0 and 1", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QueryStatisticsAsync_Throws_WhenPercentileParametersAreUsedWithNonPercentileStatistic() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetLayerClient(0).QueryStatisticsAsync(
                new FeatureStatisticsQuery {
                    Statistics = [
                        new StatisticDefinition(
                            "DEPTH",
                            "AVG_DEPTH",
                            StatisticType.Average,
                            new StatisticPercentileParameters(0.5))
                    ]
                },
                cancellationToken));

        Assert.Contains("PercentileParameters", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryStatisticsAsync_Throws_WhenPercentileStatisticsUseHavingClause() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetLayerClient(0).QueryStatisticsAsync(
                new FeatureStatisticsQuery {
                    GroupByFields = ["CATEGORY"],
                    HavingClause = "COUNT(OBJECTID) > 1",
                    Statistics = [
                        new StatisticDefinition(
                            "DEPTH",
                            "P90_DEPTH",
                            StatisticType.PercentileContinuous,
                            new StatisticPercentileParameters(0.9))
                    ]
                },
                cancellationToken));

        Assert.Contains("Percentile statistics", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("HavingClause", exception.Message, StringComparison.Ordinal);
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

    private static bool IsLayerQueryRequest(HttpRequestMessage request) {
        return request.RequestUri?.AbsolutePath.EndsWith(
            "/FeatureServer/0/query",
            StringComparison.OrdinalIgnoreCase) == true;
    }

    private static string GetDecodedQueryValue(string uri, string name) {
        var query = new Uri(uri).Query.TrimStart('?');

        foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries)) {
            var pair = part.Split('=', 2);

            if (Uri.UnescapeDataString(pair[0]) == name) {
                return pair.Length == 2
                    ? Uri.UnescapeDataString(pair[1])
                    : string.Empty;
            }
        }

        throw new InvalidOperationException($"Query parameter '{name}' was not found.");
    }

    private static HttpResponseMessage CreateLayerMetadataResponse(bool supportsPercentileStatistics) {
        return StubHttpMessageHandler.Json($$"""
        {
          "id": 0,
          "name": "Layer 0",
          "geometryType": "esriGeometryPolygon",
          "objectIdField": "OBJECTID",
          "maxRecordCount": 1000,
          "advancedQueryCapabilities": {
            "supportsPagination": true,
            "supportsPercentileStatistics": {{supportsPercentileStatistics.ToString().ToLowerInvariant()}}
          },
          "fields": [
            { "name": "OBJECTID", "type": "esriFieldTypeOID", "nullable": false },
            { "name": "DEPTH", "type": "esriFieldTypeDouble", "nullable": true },
            { "name": "CATEGORY", "type": "esriFieldTypeString", "nullable": true }
          ],
          "relationships": [],
          "extent": {
            "spatialReference": {
              "wkid": 4326,
              "latestWkid": 4326
            }
          }
        }
        """);
    }
}