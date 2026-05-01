using System.Net.Http;
using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureLayerClientCacheHintQueryTests
{
    [Fact]
    public async Task QueryAsync_IncludesCacheHintTrue_WhenProvided() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<string>();

        var client = CreateClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;
            requestUris.Add(uri);

            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse();
            }

            if (IsLayerQueryRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "objectIdFieldName": "OBJECTID",
                  "features": []
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        await foreach (var _ in client.GetLayerClient(0).QueryAsync(
            new FeatureQuery {
                CacheHint = true
            },
            cancellationToken)) {
        }

        var requestUri = Assert.Single(
            requestUris,
            uri => uri.Contains("/FeatureServer/0/query?", StringComparison.OrdinalIgnoreCase));

        var query = ParseQuery(new Uri(requestUri));

        Assert.Equal("true", query["cacheHint"]);
    }

    [Fact]
    public async Task QueryAsync_IncludesCacheHintFalse_WhenExplicitlyProvided() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<string>();

        var client = CreateClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;
            requestUris.Add(uri);

            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse();
            }

            if (IsLayerQueryRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "objectIdFieldName": "OBJECTID",
                  "features": []
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        await foreach (var _ in client.GetLayerClient(0).QueryAsync(
            new FeatureQuery {
                CacheHint = false
            },
            cancellationToken)) {
        }

        var requestUri = Assert.Single(
            requestUris,
            uri => uri.Contains("/FeatureServer/0/query?", StringComparison.OrdinalIgnoreCase));

        var query = ParseQuery(new Uri(requestUri));

        Assert.Equal("false", query["cacheHint"]);
    }

    [Fact]
    public async Task QueryAsync_DoesNotSendCacheHint_WhenNotProvided() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<string>();

        var client = CreateClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;
            requestUris.Add(uri);

            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse();
            }

            if (IsLayerQueryRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "objectIdFieldName": "OBJECTID",
                  "features": []
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        await foreach (var _ in client.GetLayerClient(0).QueryAsync(
            new FeatureQuery(),
            cancellationToken)) {
        }

        var requestUri = Assert.Single(
            requestUris,
            uri => uri.Contains("/FeatureServer/0/query?", StringComparison.OrdinalIgnoreCase));

        var query = ParseQuery(new Uri(requestUri));

        Assert.False(query.ContainsKey("cacheHint"));
    }

    [Fact]
    public async Task QueryCountAsync_IncludesCacheHint_WhenProvided() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<string>();

        var client = CreateClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;
            requestUris.Add(uri);

            if (IsLayerQueryRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "count": 7
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        var count = await client.GetLayerClient(0).QueryCountAsync(
            new FeatureQuery {
                CacheHint = true
            },
            cancellationToken);

        Assert.Equal(7, count);

        var requestUri = Assert.Single(requestUris);
        var query = ParseQuery(new Uri(requestUri));

        Assert.Equal("true", query["returnCountOnly"]);
        Assert.Equal("true", query["cacheHint"]);
    }

    [Fact]
    public async Task QueryObjectIdsAsync_IncludesCacheHint_WhenProvided() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<string>();

        var client = CreateClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;
            requestUris.Add(uri);

            if (IsLayerQueryRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "objectIdFieldName": "OBJECTID",
                  "objectIds": [10, 20]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        var objectIds = await client.GetLayerClient(0).QueryObjectIdsAsync(
            new FeatureQuery {
                CacheHint = false
            },
            cancellationToken);

        Assert.Equal([10, 20], objectIds);

        var requestUri = Assert.Single(requestUris);
        var query = ParseQuery(new Uri(requestUri));

        Assert.Equal("true", query["returnIdsOnly"]);
        Assert.Equal("false", query["cacheHint"]);
    }

    [Fact]
    public async Task QueryExtentAsync_IncludesCacheHint_WhenProvided() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<string>();

        var client = CreateClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;
            requestUris.Add(uri);

            if (IsLayerQueryRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "extent": {
                    "xmin": 10.0,
                    "ymin": 55.0,
                    "xmax": 11.0,
                    "ymax": 56.0,
                    "spatialReference": {
                      "wkid": 25832,
                      "latestWkid": 25832
                    }
                  }
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        var extent = await client.GetLayerClient(0).QueryExtentAsync(
            new FeatureQuery {
                CacheHint = true
            },
            cancellationToken);

        Assert.NotNull(extent);
        Assert.Equal(25832, extent.Srid);

        var requestUri = Assert.Single(requestUris);
        var query = ParseQuery(new Uri(requestUri));

        Assert.Equal("true", query["returnExtentOnly"]);
        Assert.Equal("true", query["cacheHint"]);
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

    private static bool IsLayerQueryRequest(HttpRequestMessage request) {
        return request.RequestUri?.AbsolutePath.EndsWith(
            "/FeatureServer/0/query",
            StringComparison.OrdinalIgnoreCase) == true;
    }

    private static Dictionary<string, string> ParseQuery(Uri uri) {
        return uri.Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .ToDictionary(
                parts => DecodeFormValue(parts[0]),
                parts => parts.Length == 2 ? DecodeFormValue(parts[1]) : string.Empty,
                StringComparer.Ordinal);
    }

    private static string DecodeFormValue(string value) {
        return Uri.UnescapeDataString(value.Replace("+", "%20", StringComparison.Ordinal));
    }

    private static HttpResponseMessage CreateLayerMetadataResponse() {
        return StubHttpMessageHandler.Json("""
        {
          "id": 0,
          "name": "Layer 0",
          "geometryType": "esriGeometryPoint",
          "objectIdField": "OBJECTID",
          "maxRecordCount": 1000,
          "advancedQueryCapabilities": {
            "supportsPagination": true,
            "supportsQueryWithCacheHint": true
          },
          "fields": [
            { "name": "OBJECTID", "type": "esriFieldTypeOID", "nullable": false }
          ],
          "relationships": [],
          "extent": {
            "spatialReference": {
              "wkid": 4326,
              "latestWkid": 4326
            }
          }
        }
        """);
    }
}