using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureLayerClientSchemaContingentValuesTests
{
    [Fact]
    public async Task GetSchemaAsync_MapsHasContingentValuesDefinition() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (request.RequestUri!.AbsolutePath.EndsWith(
                "/FeatureServer/0",
                StringComparison.OrdinalIgnoreCase)) {
                return StubHttpMessageHandler.Json("""
                {
                  "id": 0,
                  "name": "Layer 0",
                  "geometryType": "esriGeometryPoint",
                  "objectIdField": "OBJECTID",
                  "maxRecordCount": 1000,
                  "hasContingentValuesDefinition": true,
                  "advancedQueryCapabilities": {
                    "supportsPagination": true
                  },
                  "fields": [
                    { "name": "OBJECTID", "type": "esriFieldTypeOID", "nullable": false }
                  ],
                  "relationships": [],
                  "extent": {
                    "spatialReference": { "wkid": 4326, "latestWkid": 4326 }
                  }
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var schema = await client.GetLayerClient(0).GetSchemaAsync(cancellationToken);

        Assert.True(schema.HasContingentValuesDefinition);
    }

    private static FeatureServiceClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> handler) {
        return new FeatureServiceClient(
            new HttpClient(new StubHttpMessageHandler(handler)),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });
    }
}