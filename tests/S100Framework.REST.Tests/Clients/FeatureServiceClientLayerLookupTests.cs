using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureServiceClientLayerLookupTests
{
    [Fact]
    public async Task GetLayerClientAsync_ResolvesLayerByName() {
        var handler = new StubHttpMessageHandler(request => {
            var uri = request.RequestUri!.AbsoluteUri;

            if (uri.EndsWith("/FeatureServer?f=json", StringComparison.Ordinal)) {
                return StubHttpMessageHandler.Json("""
                {
                  "layers": [
                    { "id": 0, "name": "Facilities" },
                    { "id": 1, "name": "Harbors" }
                  ],
                  "tables": []
                }
                """);
            }

            if (uri.Contains("/FeatureServer/1?")) {
                return StubHttpMessageHandler.Json("""
                {
                  "id": 1,
                  "name": "Harbors",
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
                  "hasAttachments": false,
                  "supportsQueryAttachments": false,
                  "supportsAttachmentsResizing": false,
                  "supportsTopFeaturesQuery": false,
                  "extent": {
                    "spatialReference": { "wkid": 4326, "latestWkid": 4326 }
                  }
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        var client = new FeatureServiceClient(
            new HttpClient(handler),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });

        var layerClient = await client.GetLayerClientAsync("harbors");
        var schema = await layerClient.GetSchemaAsync();

        Assert.Equal("Harbors", schema.Name);
        Assert.Equal(1, schema.LayerId);
    }

    [Fact]
    public async Task GetLayerClientAsync_Throws_WhenLayerNameDoesNotExist() {
        var handler = new StubHttpMessageHandler(request => {
            var uri = request.RequestUri!.AbsoluteUri;

            if (uri.EndsWith("/FeatureServer?f=json", StringComparison.Ordinal)) {
                return StubHttpMessageHandler.Json("""
                {
                  "layers": [
                    { "id": 0, "name": "Facilities" }
                  ],
                  "tables": []
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        var client = new FeatureServiceClient(
            new HttpClient(handler),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetLayerClientAsync("Missing"));

        Assert.Contains("Missing", exception.Message);
    }
}