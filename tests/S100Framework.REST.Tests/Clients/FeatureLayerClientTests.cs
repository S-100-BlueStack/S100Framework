using S100Framework.REST.Abstractions;
using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureLayerClientTests
{
    [Fact]
    public async Task QueryAsync_UsesObjectIdFallback_WhenPaginationIsNotSupported() {
        var requests = new List<string>();

        var handler = new StubHttpMessageHandler(request => {
            var uri = request.RequestUri!.AbsoluteUri;
            requests.Add(uri);

            if (uri.Contains("/FeatureServer/0?")) {
                return StubHttpMessageHandler.Json("""
                {
                  "id": 0,
                  "name": "DepthAreas",
                  "geometryType": "esriGeometryPoint",
                  "objectIdField": "OBJECTID",
                  "maxRecordCount": 2,
                  "advancedQueryCapabilities": {
                    "supportsPagination": false
                  },
                  "fields": [
                    { "name": "OBJECTID", "type": "esriFieldTypeOID", "nullable": false }
                  ],
                  "extent": {
                    "spatialReference": { "wkid": 4326, "latestWkid": 4326 }
                  }
                }
                """);
            }

            if (uri.Contains("returnIdsOnly=true")) {
                return StubHttpMessageHandler.Json("""
                {
                  "objectIdFieldName": "OBJECTID",
                  "objectIds": [1, 2, 3]
                }
                """);
            }

            if (uri.Contains("objectIds=1%2C2")) {
                return StubHttpMessageHandler.Json("""
                {
                  "objectIdFieldName": "OBJECTID",
                  "features": [
                    { "attributes": { "OBJECTID": 1 }, "geometry": { "x": 10, "y": 20 } },
                    { "attributes": { "OBJECTID": 2 }, "geometry": { "x": 11, "y": 21 } }
                  ]
                }
                """);
            }

            if (uri.Contains("objectIds=3")) {
                return StubHttpMessageHandler.Json("""
                {
                  "objectIdFieldName": "OBJECTID",
                  "features": [
                    { "attributes": { "OBJECTID": 3 }, "geometry": { "x": 12, "y": 22 } }
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

        Assert.Equal(3, results.Count);
        Assert.Equal(new long[] { 1, 2, 3 }, results.Select(x => x.ObjectId).Cast<long>().ToArray());

        Assert.Contains(requests, request => request.Contains("returnIdsOnly=true"));
        Assert.DoesNotContain(requests, request => request.Contains("resultOffset="));
        Assert.DoesNotContain(requests, request => request.Contains("resultRecordCount="));
    }
}