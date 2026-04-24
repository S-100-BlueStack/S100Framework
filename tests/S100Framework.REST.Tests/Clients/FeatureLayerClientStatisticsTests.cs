using NetTopologySuite.Geometries;
using S100Framework.REST.Abstractions;
using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Exceptions;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureLayerClientStatisticsTests
{
    [Fact]
    public async Task QueryStatisticsAsync_SendsStatisticsParameters_AndMapsRows() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<string>();

        var handler = new StubHttpMessageHandler(request => {
            var uri = request.RequestUri!.AbsoluteUri;
            requestUris.Add(uri);

            if (uri.Contains("/FeatureServer/0/query?")) {
                return StubHttpMessageHandler.Json("""
                {
                  "features": [
                    {
                      "attributes": {
                        "PLANNAME": "Plan A",
                        "PLAN_COUNT": 2,
                        "MAX_AOIID": 99
                      }
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        IFeatureServiceClient serviceClient = new FeatureServiceClient(
            new HttpClient(handler),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });

        var layerClient = serviceClient.GetLayerClient(0);
        var query = new FeatureStatisticsQuery {
            Where = "1=1",
            GroupByFields = ["PLANNAME"],
            HavingClause = "COUNT(OBJECTID) > 1",
            OrderBy = "PLANNAME",
            SpatialFilter = FeatureSpatialFilter.FromEnvelope(
                new Envelope(10, 30, 20, 40),
                inSrid: 4326,
                spatialRelationship: SpatialRelationship.Intersects),
            Statistics = [
                new StatisticDefinition("OBJECTID", "PLAN_COUNT", StatisticType.Count),
                new StatisticDefinition("AOIID", "MAX_AOIID", StatisticType.Max)
            ]
        };

        var rows = await layerClient.QueryStatisticsAsync(query, cancellationToken);

        Assert.Single(rows);
        Assert.Equal("Plan A", rows[0].GetRequiredString("PLANNAME"));
        Assert.Equal(2L, rows[0].GetRequiredInt64("PLAN_COUNT"));
        Assert.Equal(99L, rows[0].GetRequiredInt64("MAX_AOIID"));

        var queryRequest = Assert.Single(requestUris, uri => uri.Contains("/FeatureServer/0/query?"));
        var decoded = Uri.UnescapeDataString(queryRequest);

        Assert.Contains("outStatistics=", decoded);
        Assert.Contains("\"statisticType\":\"count\"", decoded);
        Assert.Contains("\"outStatisticFieldName\":\"PLAN_COUNT\"", decoded);
        Assert.Contains("\"statisticType\":\"max\"", decoded);
        Assert.Contains("groupByFieldsForStatistics=PLANNAME", decoded);
        Assert.Contains("havingClause=COUNT(OBJECTID) > 1", decoded);
        Assert.Contains("orderByFields=PLANNAME", decoded);
        Assert.Contains("geometryType=esriGeometryEnvelope", decoded);
        Assert.Contains("spatialRel=esriSpatialRelIntersects", decoded);
    }

    [Fact]
    public async Task QueryStatisticsAsync_SendsPaginationParameters_WhenAggregatedPaginationIsSupported() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<string>();

        var handler = new StubHttpMessageHandler(request => {
            var uri = request.RequestUri!.AbsoluteUri;
            requestUris.Add(uri);

            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse(supportsPaginationOnAggregatedQueries: true);
            }

            if (uri.Contains("/FeatureServer/0/query?")) {
                return StubHttpMessageHandler.Json("""
                {
                  "features": [
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

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        var layerClient = new FeatureServiceClient(
            new HttpClient(handler),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            }).GetLayerClient(0);

        var rows = await layerClient.QueryStatisticsAsync(
            new FeatureStatisticsQuery {
                GroupByFields = ["PLANNAME"],
                ResultOffset = 10,
                ResultRecordCount = 25,
                Statistics = [
                    new StatisticDefinition("OBJECTID", "PLAN_COUNT", StatisticType.Count)
                ]
            },
            cancellationToken);

        Assert.Single(rows);

        var queryRequest = Assert.Single(requestUris, uri => uri.Contains("/FeatureServer/0/query?"));
        var decoded = Uri.UnescapeDataString(queryRequest);

        Assert.Contains("resultOffset=10", decoded);
        Assert.Contains("resultRecordCount=25", decoded);
        Assert.Contains("groupByFieldsForStatistics=PLANNAME", decoded);
    }

    [Fact]
    public async Task QueryStatisticsAsync_Throws_WhenAggregatedPaginationIsNotSupported() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<string>();

        var layerClient = new FeatureServiceClient(
            new HttpClient(new StubHttpMessageHandler(request => {
                requestUris.Add(request.RequestUri!.AbsoluteUri);

                if (IsLayerMetadataRequest(request)) {
                    return CreateLayerMetadataResponse(supportsPaginationOnAggregatedQueries: false);
                }

                throw new InvalidOperationException("The statistics query should not be executed.");
            })),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            }).GetLayerClient(0);

        var exception = await Assert.ThrowsAsync<FeatureServiceCapabilityException>(() =>
            layerClient.QueryStatisticsAsync(
                new FeatureStatisticsQuery {
                    GroupByFields = ["PLANNAME"],
                    ResultOffset = 10,
                    ResultRecordCount = 25,
                    Statistics = [
                        new StatisticDefinition("OBJECTID", "PLAN_COUNT", StatisticType.Count)
                    ]
                },
                cancellationToken));

        Assert.Contains("pagination on aggregated queries", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single(requestUris);
    }

    [Fact]
    public async Task QueryStatisticsAsync_Throws_WhenPaginationIsRequestedWithoutGrouping() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var layerClient = new FeatureServiceClient(
            new HttpClient(new StubHttpMessageHandler(_ => throw new InvalidOperationException("The HTTP request should not be executed."))),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            }).GetLayerClient(0);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            layerClient.QueryStatisticsAsync(
                new FeatureStatisticsQuery {
                    ResultOffset = 10,
                    Statistics = [
                        new StatisticDefinition("OBJECTID", "PLAN_COUNT", StatisticType.Count)
                    ]
                },
                cancellationToken));

        Assert.Contains("GroupByFields", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QueryStatisticsAsync_Throws_WhenHavingClauseIsProvidedWithoutGrouping() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var layerClient = new FeatureServiceClient(
            new HttpClient(new StubHttpMessageHandler(_ => throw new InvalidOperationException("The HTTP request should not be executed."))),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            }).GetLayerClient(0);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            layerClient.QueryStatisticsAsync(
                new FeatureStatisticsQuery {
                    HavingClause = "COUNT(OBJECTID) > 1",
                    Statistics = [
                        new StatisticDefinition("OBJECTID", "PLAN_COUNT", StatisticType.Count)
                    ]
                },
                cancellationToken));

        Assert.Contains("HavingClause", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("GroupByFields", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QueryStatisticsAsync_Throws_WhenGroupByFieldsContainEmptyValue() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var layerClient = new FeatureServiceClient(
            new HttpClient(new StubHttpMessageHandler(_ => throw new InvalidOperationException("The HTTP request should not be executed."))),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            }).GetLayerClient(0);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            layerClient.QueryStatisticsAsync(
                new FeatureStatisticsQuery {
                    GroupByFields = ["PLANNAME", ""],
                    Statistics = [
                        new StatisticDefinition("OBJECTID", "PLAN_COUNT", StatisticType.Count)
                    ]
                },
                cancellationToken));

        Assert.Contains("GroupByFields", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QueryStatisticsAsync_Throws_WhenOrderByIsWhitespace() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var layerClient = new FeatureServiceClient(
            new HttpClient(new StubHttpMessageHandler(_ => throw new InvalidOperationException("The HTTP request should not be executed."))),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            }).GetLayerClient(0);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            layerClient.QueryStatisticsAsync(
                new FeatureStatisticsQuery {
                    OrderBy = "   ",
                    Statistics = [
                        new StatisticDefinition("OBJECTID", "PLAN_COUNT", StatisticType.Count)
                    ]
                },
                cancellationToken));

        Assert.Contains("OrderBy", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QueryStatisticsAsync_Throws_WhenStatisticAliasesAreDuplicated() {
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
                        new StatisticDefinition("OBJECTID", "STAT_VALUE", StatisticType.Count),
                        new StatisticDefinition("AOIID", "STAT_VALUE", StatisticType.Max)
                    ]
                },
                cancellationToken));

        Assert.Contains("Duplicate statistic alias", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QueryStatisticsAsync_Throws_WhenNoStatisticsAreProvided() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var layerClient = new FeatureServiceClient(
            new HttpClient(new StubHttpMessageHandler(_ => throw new InvalidOperationException("The HTTP request should not be executed."))),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            }).GetLayerClient(0);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            layerClient.QueryStatisticsAsync(new FeatureStatisticsQuery(), cancellationToken));

        Assert.Contains("statistic definition", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLayerMetadataRequest(HttpRequestMessage request) {
        return request.RequestUri?.AbsolutePath.EndsWith(
            "/FeatureServer/0",
            StringComparison.OrdinalIgnoreCase) == true;
    }

    private static HttpResponseMessage CreateLayerMetadataResponse(bool supportsPaginationOnAggregatedQueries) {
        return StubHttpMessageHandler.Json($$"""
        {
          "id": 0,
          "name": "Layer 0",
          "geometryType": "esriGeometryPolygon",
          "objectIdField": "OBJECTID",
          "maxRecordCount": 1000,
          "advancedQueryCapabilities": {
            "supportsPagination": true,
            "supportsPaginationOnAggregatedQueries": {{supportsPaginationOnAggregatedQueries.ToString().ToLowerInvariant()}}
          },
          "fields": [
            { "name": "OBJECTID", "type": "esriFieldTypeOID", "nullable": false },
            { "name": "PLANNAME", "type": "esriFieldTypeString", "nullable": true },
            { "name": "AOIID", "type": "esriFieldTypeInteger", "nullable": true }
          ],
          "relationships": [],
          "extent": {
            "spatialReference": { "wkid": 4326, "latestWkid": 4326 }
          }
        }
        """);
    }
}