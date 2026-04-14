using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureLayerClientRelatedRecordsCurveTests
{
    [Fact]
    public async Task QueryRelatedRecordsAsync_SendsReturnTrueCurvesFlag() {
        var requestUris = new List<string>();

        var handler = new StubHttpMessageHandler(request => {
            var uri = request.RequestUri!.AbsoluteUri;
            requestUris.Add(uri);

            if (uri.Contains("/FeatureServer/0/queryRelatedRecords?")) {
                return StubHttpMessageHandler.Json("""
                {
                  "geometryType": "esriGeometryPoint",
                  "spatialReference": {
                    "wkid": 4326,
                    "latestWkid": 4326
                  },
                  "fields": [
                    { "name": "OBJECTID", "type": "esriFieldTypeOID", "nullable": false }
                  ],
                  "relatedRecordGroups": [
                    {
                      "objectId": 100,
                      "relatedRecords": [
                        {
                          "attributes": {
                            "OBJECTID": 1
                          },
                          "geometry": {
                            "x": 10,
                            "y": 20
                          }
                        }
                      ]
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
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer"),
                ReturnTrueCurves = true
            }).GetLayerClient(0);

        var groups = await layerClient.QueryRelatedRecordsAsync(
            new RelatedRecordsQuery {
                ObjectIds = [100],
                RelationshipId = 1,
                OutFields = ["OBJECTID"],
                ReturnGeometry = true
            });

        Assert.Single(groups);

        var requestUri = Assert.Single(requestUris);
        Assert.Contains("returnTrueCurves=true", requestUri);
    }
}