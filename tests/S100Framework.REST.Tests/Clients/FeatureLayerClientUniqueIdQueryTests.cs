using System.Net.Http;
using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Exceptions;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureLayerClientUniqueIdQueryTests
{
    [Fact]
    public async Task QueryUniqueIdsAsync_ReturnsSimpleUniqueIds_WhenLayerSupportsUniqueIds() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<string>();

        var client = CreateClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;
            requestUris.Add(uri);

            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse(includeUniqueIdInfo: true, uniqueIdType: "simple");
            }

            if (uri.Contains("/FeatureServer/0/query?", StringComparison.OrdinalIgnoreCase)) {
                return StubHttpMessageHandler.Json("""
                {
                  "exceededTransferLimit": false,
                  "uniqueIdFieldNames": "_id",
                  "uniqueIds": ["alpha", "beta"]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        var result = await client.GetLayerClient(0).QueryUniqueIdsAsync(
            new FeatureQuery {
                Where = "NAME LIKE 'A%'"
            },
            cancellationToken);

        Assert.False(result.IsComposite);
        Assert.False(result.ExceededTransferLimit);
        Assert.Equal(["_id"], result.UniqueIdFieldNames);
        Assert.Equal(2, result.UniqueIds.Count);
        Assert.Equal("alpha", result.UniqueIds[0].SingleValue);
        Assert.Equal(["beta"], result.UniqueIds[1].Components);

        var queryRequest = Assert.Single(
            requestUris,
            uri => uri.Contains("/FeatureServer/0/query?", StringComparison.OrdinalIgnoreCase));

        var decodedQueryRequest = Uri.UnescapeDataString(queryRequest);

        Assert.Contains("returnUniqueIdsOnly=true", decodedQueryRequest, StringComparison.Ordinal);
        Assert.DoesNotContain("returnIdsOnly=true", decodedQueryRequest, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryUniqueIdsAsync_ReturnsCompositeUniqueIds_WhenLayerUsesCompositeUniqueIds() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse(includeUniqueIdInfo: true, uniqueIdType: "composite");
            }

            if (request.RequestUri!.AbsoluteUri.Contains("/FeatureServer/0/query?", StringComparison.OrdinalIgnoreCase)) {
                return StubHttpMessageHandler.Json("""
                {
                  "exceededTransferLimit": true,
                  "uniqueIdFieldNames": ["COUNTRY_CODE", "HARBOR_CODE"],
                  "uniqueIds": [
                    ["DK", "CPH"],
                    ["SE", "GOT"]
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri!.AbsoluteUri}");
        });

        var result = await client.GetLayerClient(0).QueryUniqueIdsAsync(
            new FeatureQuery(),
            cancellationToken);

        Assert.True(result.IsComposite);
        Assert.True(result.ExceededTransferLimit);
        Assert.Equal(["COUNTRY_CODE", "HARBOR_CODE"], result.UniqueIdFieldNames);
        Assert.Equal(["DK", "CPH"], result.UniqueIds[0].Components);
        Assert.Equal(["SE", "GOT"], result.UniqueIds[1].Components);
        Assert.Null(result.UniqueIds[0].SingleValue);
    }

    [Fact]
    public async Task QueryUniqueIdsAsync_Throws_WhenLayerDoesNotExposeUniqueIds() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<string>();

        var client = CreateClient(request => {
            requestUris.Add(request.RequestUri!.AbsoluteUri);

            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse(includeUniqueIdInfo: false, uniqueIdType: null);
            }

            throw new InvalidOperationException("HTTP should not be called after schema lookup.");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceCapabilityException>(() =>
            client.GetLayerClient(0).QueryUniqueIdsAsync(
                new FeatureQuery(),
                cancellationToken));

        Assert.Contains("unique ID", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single(requestUris);
    }

    private static FeatureServiceClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> handler) {
        return new FeatureServiceClient(
            new HttpClient(new StubHttpMessageHandler(handler)),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer"),
                QueryRequestMethodPreference = QueryRequestMethodPreference.Get
            });
    }

    private static bool IsLayerMetadataRequest(HttpRequestMessage request) {
        return request.RequestUri?.AbsolutePath.EndsWith(
            "/FeatureServer/0",
            StringComparison.OrdinalIgnoreCase) == true;
    }

    private static HttpResponseMessage CreateLayerMetadataResponse(bool includeUniqueIdInfo, string? uniqueIdType) {
        var uniqueIdInfoJson = includeUniqueIdInfo
            ? uniqueIdType == "composite"
                ? """
                  ,
                  "uniqueIdInfo": {
                    "type": "composite",
                    "fields": ["COUNTRY_CODE", "HARBOR_CODE"],
                    "OIDFieldContainsHashValue": true
                  }
                  """
                : """
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
            { "name": "_id", "type": "esriFieldTypeString", "nullable": false },
            { "name": "COUNTRY_CODE", "type": "esriFieldTypeString", "nullable": false },
            { "name": "HARBOR_CODE", "type": "esriFieldTypeString", "nullable": false }
          ]{{uniqueIdInfoJson}},
          "relationships": [],
          "extent": {
            "spatialReference": { "wkid": 4326, "latestWkid": 4326 }
          }
        }
        """);
    }
}