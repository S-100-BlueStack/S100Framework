using System.Net.Http;
using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Exceptions;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureLayerClientPercentileStatisticsTests
{
    [Fact]
    public async Task QueryStatisticsAsync_SendsPercentileStatisticParameters_WhenLayerSupportsPercentiles() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<string>();

        var layerClient = new FeatureServiceClient(
            new HttpClient(new StubHttpMessageHandler(request => {
                var uri = request.RequestUri!.AbsoluteUri;
                requestUris.Add(uri);

                if (IsLayerMetadataRequest(request)) {
                    return CreateLayerMetadataResponse(supportsPercentileStatistics: true);
                }

                if (uri.Contains("/FeatureServer/0/query?", StringComparison.OrdinalIgnoreCase)) {
                    return StubHttpMessageHandler.Json("""
                    {
                      "features": [
                        {
                          "attributes": {
                            "P90_DEPTH": 17.25
                          }
                        }
                      ]
                    }
                    """);
                }

                throw new InvalidOperationException($"Unexpected request: {uri}");
            })),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            }).GetLayerClient(0);

        var rows = await layerClient.QueryStatisticsAsync(
            new FeatureStatisticsQuery {
                Statistics = [
                    new StatisticDefinition(
                        "DEPTH",
                        "P90_DEPTH",
                        StatisticType.PercentileDiscrete,
                        new StatisticPercentileParameters(0.9, StatisticPercentileOrder.Desc))
                ]
            },
            cancellationToken);

        Assert.Single(rows);
        Assert.Equal(17.25m, rows[0].GetRequiredDecimal("P90_DEPTH"));

        var queryRequest = Assert.Single(
            requestUris,
            uri => uri.Contains("/FeatureServer/0/query?", StringComparison.OrdinalIgnoreCase));

        var decoded = Uri.UnescapeDataString(queryRequest);

        Assert.Contains("\"statisticType\":\"PERCENTILE_DISC\"", decoded, StringComparison.Ordinal);
        Assert.Contains("\"onStatisticField\":\"DEPTH\"", decoded, StringComparison.Ordinal);
        Assert.Contains("\"outStatisticFieldName\":\"P90_DEPTH\"", decoded, StringComparison.Ordinal);
        Assert.Contains("\"statisticParameters\":{\"value\":0.9,\"orderBy\":\"DESC\"}", decoded, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryStatisticsAsync_Throws_WhenLayerDoesNotSupportPercentileStatistics() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<string>();

        var layerClient = new FeatureServiceClient(
            new HttpClient(new StubHttpMessageHandler(request => {
                requestUris.Add(request.RequestUri!.AbsoluteUri);

                if (IsLayerMetadataRequest(request)) {
                    return CreateLayerMetadataResponse(supportsPercentileStatistics: false);
                }

                throw new InvalidOperationException("The statistics query should not be executed.");
            })),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            }).GetLayerClient(0);

        var exception = await Assert.ThrowsAsync<FeatureServiceCapabilityException>(() =>
            layerClient.QueryStatisticsAsync(
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

        var layerClient = new FeatureServiceClient(
            new HttpClient(new StubHttpMessageHandler(_ => throw new InvalidOperationException("The HTTP request should not be executed."))),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            }).GetLayerClient(0);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            layerClient.QueryStatisticsAsync(
                new FeatureStatisticsQuery {
                    Statistics = [
                        new StatisticDefinition(
                            "DEPTH",
                            "P90_DEPTH",
                            StatisticType.PercentileContinuous)
                    ]
                },
                cancellationToken));

        Assert.Contains("PercentileParameters", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QueryStatisticsAsync_Throws_WhenPercentileIsCombinedWithHavingClause() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var layerClient = new FeatureServiceClient(
            new HttpClient(new StubHttpMessageHandler(_ => throw new InvalidOperationException("The HTTP request should not be executed."))),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            }).GetLayerClient(0);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            layerClient.QueryStatisticsAsync(
                new FeatureStatisticsQuery {
                    GroupByFields = ["PLANNAME"],
                    HavingClause = "COUNT(OBJECTID) > 1",
                    Statistics = [
                        new StatisticDefinition(
                            "DEPTH",
                            "P90_DEPTH",
                            StatisticType.PercentileDiscrete,
                            new StatisticPercentileParameters(0.9))
                    ]
                },
                cancellationToken));

        Assert.Contains("Percentile", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("HavingClause", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLayerMetadataRequest(HttpRequestMessage request) {
        return request.RequestUri?.AbsolutePath.EndsWith(
            "/FeatureServer/0",
            StringComparison.OrdinalIgnoreCase) == true;
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
            "supportsPaginationOnAggregatedQueries": true,
            "supportsPercentileStatistics": {{supportsPercentileStatistics.ToString().ToLowerInvariant()}}
          },
          "fields": [
            { "name": "OBJECTID", "type": "esriFieldTypeOID", "nullable": false },
            { "name": "PLANNAME", "type": "esriFieldTypeString", "nullable": true },
            { "name": "DEPTH", "type": "esriFieldTypeDouble", "nullable": true }
          ],
          "relationships": [],
          "extent": {
            "spatialReference": { "wkid": 4326, "latestWkid": 4326 }
          }
        }
        """);
    }
}