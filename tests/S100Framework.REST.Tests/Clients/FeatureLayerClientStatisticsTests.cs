using NetTopologySuite.Geometries;
using S100Framework.REST.Abstractions;
using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureLayerClientStatisticsTests
{
    [Fact]
    public async Task QueryStatisticsAsync_SendsStatisticsParameters_AndMapsRows() {
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
            Statistics =
            [
                new StatisticDefinition("OBJECTID", "PLAN_COUNT", StatisticType.Count),
                new StatisticDefinition("AOIID", "MAX_AOIID", StatisticType.Max)
            ]
        };

        var rows = await layerClient.QueryStatisticsAsync(query);

        Assert.Single(rows);
        Assert.Equal("Plan A", rows[0].GetRequiredString("PLANNAME"));
        Assert.Equal(2L, rows[0].GetRequiredInt64("PLAN_COUNT"));
        Assert.Equal(99L, rows[0].GetRequiredInt64("MAX_AOIID"));

        var queryRequest = Assert.Single(requestUris.Where(uri => uri.Contains("/FeatureServer/0/query?")));
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
    public async Task QueryStatisticsAsync_Throws_WhenNoStatisticsAreProvided() {
        var layerClient = new FeatureServiceClient(
            new HttpClient(new StubHttpMessageHandler(_ =>
                throw new InvalidOperationException("The HTTP request should not be executed."))),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            }).GetLayerClient(0);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            layerClient.QueryStatisticsAsync(new FeatureStatisticsQuery()));

        Assert.Contains("statistic definition", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}