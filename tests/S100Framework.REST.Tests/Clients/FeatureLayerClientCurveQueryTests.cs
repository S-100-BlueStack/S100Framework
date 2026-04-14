using S100Framework.REST.Abstractions;
using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureLayerClientCurveQueryTests
{
    [Fact]
    public async Task QueryAsync_SendsReturnTrueCurvesFalse_ByDefault() {
        var requestUris = new List<string>();

        var handler = new StubHttpMessageHandler(request => {
            var uri = request.RequestUri!.AbsoluteUri;
            requestUris.Add(uri);

            if (uri.Contains("/FeatureServer/0?")) {
                return StubHttpMessageHandler.Json("""
                {
                  "id": 0,
                  "name": "CurveLayer",
                  "geometryType": "esriGeometryPolyline",
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
                  "supportsTopFeaturesQuery": false
                }
                """);
            }

            if (uri.Contains("/FeatureServer/0/query?")) {
                return StubHttpMessageHandler.Json("""
                {
                  "objectIdFieldName": "OBJECTID",
                  "features": [
                    {
                      "attributes": { "OBJECTID": 1 },
                      "geometry": {
                        "paths": [
                          [[10,20],[11,21]]
                        ]
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

        var results = new List<FeatureRecord>();

        await foreach (var feature in layerClient.QueryAsync(new FeatureQuery())) {
            results.Add(feature);
        }

        Assert.Single(results);

        var queryRequest = Assert.Single(requestUris.Where(uri => uri.Contains("/FeatureServer/0/query?")));
        Assert.Contains("returnTrueCurves=false", queryRequest);
    }

    [Fact]
    public async Task QueryAsync_SendsReturnTrueCurvesTrue_WhenEnabled() {
        var requestUris = new List<string>();

        var handler = new StubHttpMessageHandler(request => {
            var uri = request.RequestUri!.AbsoluteUri;
            requestUris.Add(uri);

            if (uri.Contains("/FeatureServer/0?")) {
                return StubHttpMessageHandler.Json("""
                {
                  "id": 0,
                  "name": "CurveLayer",
                  "geometryType": "esriGeometryPolyline",
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
                  "supportsTopFeaturesQuery": false
                }
                """);
            }

            if (uri.Contains("/FeatureServer/0/query?")) {
                return StubHttpMessageHandler.Json("""
                {
                  "objectIdFieldName": "OBJECTID",
                  "features": [
                    {
                      "attributes": { "OBJECTID": 1 },
                      "geometry": {
                        "paths": [
                          [[10,20],[11,21]]
                        ]
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
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer"),
                ReturnTrueCurves = true
            });

        var layerClient = serviceClient.GetLayerClient(0);

        var results = new List<FeatureRecord>();

        await foreach (var feature in layerClient.QueryAsync(new FeatureQuery())) {
            results.Add(feature);
        }

        Assert.Single(results);

        var queryRequest = Assert.Single(requestUris.Where(uri => uri.Contains("/FeatureServer/0/query?")));
        Assert.Contains("returnTrueCurves=true", queryRequest);
    }
}