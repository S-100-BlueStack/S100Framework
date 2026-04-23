using System.Net.Http;
using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureLayerClientUniqueIdSchemaTests
{
    [Fact]
    public async Task GetSchemaAsync_MapsUniqueIdInfo_WhenMetadataExposesIt() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse(includeUniqueIdInfo: true);
            }

            throw new InvalidOperationException("Unexpected request.");
        });

        var schema = await client.GetLayerClient(0).GetSchemaAsync(cancellationToken);

        Assert.True(schema.SupportsUniqueIds);
        Assert.NotNull(schema.UniqueIdInfo);

        var uniqueIdInfo = schema.UniqueIdInfo!;

        Assert.Equal("simple", uniqueIdInfo.Type);
        Assert.Equal(["_id"], uniqueIdInfo.Fields);
        Assert.True(uniqueIdInfo.OidFieldContainsHashValue);
    }

    [Fact]
    public async Task GetSchemaAsync_LeavesUniqueIdInfoNull_WhenMetadataDoesNotExposeIt() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse(includeUniqueIdInfo: false);
            }

            throw new InvalidOperationException("Unexpected request.");
        });

        var schema = await client.GetLayerClient(0).GetSchemaAsync(cancellationToken);

        Assert.False(schema.SupportsUniqueIds);
        Assert.Null(schema.UniqueIdInfo);
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

    private static HttpResponseMessage CreateLayerMetadataResponse(bool includeUniqueIdInfo) {
        var uniqueIdInfoJson = includeUniqueIdInfo
            ? """
              ,
              "uniqueIdInfo": {
                "type": "simple",
                "fields": ["_id"],
                "OIDFieldContainsHashValue": true
              }
              """
            : string.Empty;

        return StubHttpMessageHandler.Json($$"""
        {
          "id": 0,
          "name": "Layer 0",
          "geometryType": "esriGeometryPoint",
          "objectIdField": "OBJECTID",
          "maxRecordCount": 1000,
          "advancedQueryCapabilities": {
            "supportsPagination": true,
            "supportsReturningGeometryEnvelope": false,
            "supportsFullTextSearch": false
          },
          "fields": [
            { "name": "OBJECTID", "type": "esriFieldTypeOID", "nullable": false },
            { "name": "_id", "type": "esriFieldTypeString", "nullable": false }
          ]{{uniqueIdInfoJson}},
          "relationships": [],
          "extent": {
            "spatialReference": { "wkid": 4326, "latestWkid": 4326 }
          }
        }
        """);
    }
}