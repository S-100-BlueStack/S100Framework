using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureLayerClientTopFeaturesCurveTests
{
    [Fact]
    public async Task QueryTopFeaturesAsync_SendsReturnTrueCurvesFlag() {
        var requestUris = new List<string>();

        var handler = new StubHttpMessageHandler(request => {
            var uri = request.RequestUri!.AbsoluteUri;
            requestUris.Add(uri);

            if (uri.Contains("/FeatureServer/0?")) {
                return StubHttpMessageHandler.Json("""
                {
                  "id": 0,
                  "name": "TopLayer",
                  "geometryType": "esriGeometryPoint",
                  "objectIdField": "OBJECTID",
                  "maxRecordCount": 1000,
                  "advancedQueryCapabilities": {
                    "supportsPagination": true
                  },
                  "fields": [
                    { "name": "OBJECTID", "type": "esriFieldTypeOID", "nullable": false }
                  ],
                  "relationships": [],
                  "extent": {
                    "spatialReference": { "wkid": 4326, "latestWkid": 4326 }
                  },
                  "hasAttachments": false,
                  "supportsQueryAttachments": false,
                  "supportsAttachmentsResizing": false,
                  "supportsTopFeaturesQuery": true
                }
                """);
            }

            if (uri.Contains("/FeatureServer/0/queryTopFeatures?")) {
                return StubHttpMessageHandler.Json("""
                {
                  "objectIdFieldName": "OBJECTID",
                  "features": [
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

        var result = await layerClient.QueryTopFeaturesAsync(
            new TopFeaturesQuery {
                OutFields = ["OBJECTID"],
                TopFilter = new TopFeaturesFilter {
                    GroupByFields = ["REGION"],
                    OrderByFields = ["SCORE DESC"],
                    TopCount = 1
                }
            });

        Assert.Single(result);

        var requestUri = Assert.Single(requestUris.Where(x => x.Contains("/queryTopFeatures?")));
        Assert.Contains("returnTrueCurves=true", requestUri);
    }
}