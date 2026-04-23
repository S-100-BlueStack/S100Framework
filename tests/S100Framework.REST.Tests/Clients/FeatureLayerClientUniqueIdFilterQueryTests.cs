using System.Net.Http;
using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureLayerClientUniqueIdFilterQueryTests
{
    [Fact]
    public async Task QueryCountAsync_IncludesSimpleUniqueIds_WhenProvided() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<string>();

        var client = CreateClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;
            requestUris.Add(uri);

            if (uri.Contains("/FeatureServer/0/query?", StringComparison.OrdinalIgnoreCase)) {
                return StubHttpMessageHandler.Json("""
                {
                  "count": 2
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        var count = await client.GetLayerClient(0).QueryCountAsync(
            new FeatureQuery {
                UniqueIds =
                [
                    new FeatureUniqueId(["alpha"]),
                    new FeatureUniqueId(["beta"])
                ]
            },
            cancellationToken);

        Assert.Equal(2, count);

        var queryRequest = Assert.Single(requestUris);
        var decodedQueryRequest = Uri.UnescapeDataString(queryRequest);

        Assert.Contains("returnCountOnly=true", decodedQueryRequest, StringComparison.Ordinal);
        Assert.Contains(@"uniqueIds=[""alpha"",""beta""]", decodedQueryRequest, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryObjectIdsAsync_IncludesCompositeUniqueIds_WhenProvided() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<string>();

        var client = CreateClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;
            requestUris.Add(uri);

            if (uri.Contains("/FeatureServer/0/query?", StringComparison.OrdinalIgnoreCase)) {
                return StubHttpMessageHandler.Json("""
                {
                  "objectIdFieldName": "OBJECTID",
                  "objectIds": [10, 11]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        var objectIds = await client.GetLayerClient(0).QueryObjectIdsAsync(
            new FeatureQuery {
                UniqueIds =
                [
                    new FeatureUniqueId(["DK", "CPH"]),
                    new FeatureUniqueId(["SE", "GOT"])
                ]
            },
            cancellationToken);

        Assert.Equal([10L, 11L], objectIds);

        var queryRequest = Assert.Single(requestUris);
        var decodedQueryRequest = Uri.UnescapeDataString(queryRequest);

        Assert.Contains("returnIdsOnly=true", decodedQueryRequest, StringComparison.Ordinal);
        Assert.Contains(@"uniqueIds=[[""DK"",""CPH""],[""SE"",""GOT""]]", decodedQueryRequest, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryAsync_UsesUniqueIdBatching_WhenPaginationIsNotSupported() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<string>();

        var client = CreateClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;
            requestUris.Add(uri);

            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse(supportsPagination: false, includeUniqueIdInfo: true);
            }

            if (uri.Contains(@"uniqueIds=%5B%22alpha%22%5D", StringComparison.OrdinalIgnoreCase)) {
                return StubHttpMessageHandler.Json("""
                {
                  "objectIdFieldName": "OBJECTID",
                  "features": [
                    {
                      "attributes": { "OBJECTID": 1, "_id": "alpha", "NAME": "Alpha" },
                      "geometry": { "x": 10, "y": 20 }
                    }
                  ]
                }
                """);
            }

            if (uri.Contains(@"uniqueIds=%5B%22beta%22%5D", StringComparison.OrdinalIgnoreCase)) {
                return StubHttpMessageHandler.Json("""
                {
                  "objectIdFieldName": "OBJECTID",
                  "features": [
                    {
                      "attributes": { "OBJECTID": 2, "_id": "beta", "NAME": "Beta" },
                      "geometry": { "x": 30, "y": 40 }
                    }
                  ]
                }
                """);
            }

            if (uri.Contains("returnIdsOnly=true", StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidOperationException("The object ID fallback should not be used for unique ID batching.");
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        var layerClient = client.GetLayerClient(0);
        var records = new List<FeatureRecord>();

        await foreach (var feature in layerClient.QueryAsync(
            new FeatureQuery {
                PageSize = 1,
                UniqueIds =
                [
                    new FeatureUniqueId(["alpha"]),
                    new FeatureUniqueId(["beta"])
                ]
            },
            cancellationToken)) {
            records.Add(feature);
        }

        Assert.Equal(2, records.Count);

        var queryRequests = requestUris
            .Where(uri => uri.Contains("/FeatureServer/0/query?", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.Equal(2, queryRequests.Length);

        Assert.All(queryRequests, uri => {
            var decoded = Uri.UnescapeDataString(uri);

            Assert.DoesNotContain("objectIds=", decoded, StringComparison.Ordinal);
            Assert.Contains("uniqueIds=", decoded, StringComparison.Ordinal);
        });

        Assert.DoesNotContain(
            requestUris,
            uri => uri.Contains("returnIdsOnly=true", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task QueryCountAsync_Throws_WhenUniqueIdsContainEmptyComponents() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = CreateClient(_ => throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetLayerClient(0).QueryCountAsync(
                new FeatureQuery {
                    UniqueIds =
                    [
                        new FeatureUniqueId([""])
                    ]
                },
                cancellationToken));

        Assert.Contains("UniqueIds", exception.Message, StringComparison.OrdinalIgnoreCase);
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

    private static HttpResponseMessage CreateLayerMetadataResponse(bool supportsPagination, bool includeUniqueIdInfo) {
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
            "supportsPagination": {{supportsPagination.ToString().ToLowerInvariant()}},
            "supportsReturningGeometryEnvelope": false,
            "supportsFullTextSearch": false
          },
          "fields": [
            { "name": "OBJECTID", "type": "esriFieldTypeOID", "nullable": false },
            { "name": "_id", "type": "esriFieldTypeString", "nullable": false },
            { "name": "NAME", "type": "esriFieldTypeString", "nullable": true }
          ]{{uniqueIdInfoJson}},
          "relationships": [],
          "extent": {
            "spatialReference": { "wkid": 4326, "latestWkid": 4326 }
          }
        }
        """);
    }
}