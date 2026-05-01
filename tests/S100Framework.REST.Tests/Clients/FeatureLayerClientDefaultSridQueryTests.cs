using System.Net.Http;
using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureLayerClientDefaultSridQueryTests
{
    [Fact]
    public async Task QueryAsync_IncludesDefaultSrid_WhenProvided() {
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
                  "geometryType": "esriGeometryPoint",
                  "features": []
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        await foreach (var _ in client.GetLayerClient(0).QueryAsync(
            new FeatureQuery {
                DefaultSrid = 25832,
                OutSrid = 25832,
                ReturnGeometry = false
            },
            cancellationToken)) {
        }

        var queryRequest = Assert.Single(
            requestUris,
            uri => uri.Contains("/FeatureServer/0/query?", StringComparison.OrdinalIgnoreCase));

        var query = ParseQuery(new Uri(queryRequest));

        Assert.Equal("25832", query["defaultSR"]);
        Assert.Equal("25832", query["outSR"]);
        Assert.Equal("false", query["returnGeometry"]);
    }

    [Fact]
    public async Task QueryAsync_DoesNotSendDefaultSrid_WhenNotProvided() {
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
                  "geometryType": "esriGeometryPoint",
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

        var queryRequest = Assert.Single(
            requestUris,
            uri => uri.Contains("/FeatureServer/0/query?", StringComparison.OrdinalIgnoreCase));

        var query = ParseQuery(new Uri(queryRequest));

        Assert.False(query.ContainsKey("defaultSR"));
    }

    [Fact]
    public async Task QueryCountAsync_IncludesDefaultSrid_WhenProvided() {
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
                DefaultSrid = 25832
            },
            cancellationToken);

        Assert.Equal(7, count);

        var queryRequest = Assert.Single(requestUris);
        var query = ParseQuery(new Uri(queryRequest));

        Assert.Equal("true", query["returnCountOnly"]);
        Assert.Equal("25832", query["defaultSR"]);
    }

    [Fact]
    public async Task QueryObjectIdsAsync_IncludesDefaultSrid_WhenProvided() {
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
                DefaultSrid = 25832
            },
            cancellationToken);

        Assert.Equal([10, 20], objectIds);

        var queryRequest = Assert.Single(requestUris);
        var query = ParseQuery(new Uri(queryRequest));

        Assert.Equal("true", query["returnIdsOnly"]);
        Assert.Equal("25832", query["defaultSR"]);
    }

    [Fact]
    public async Task QueryExtentAsync_IncludesDefaultSrid_WhenProvided() {
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
                DefaultSrid = 25832
            },
            cancellationToken);

        Assert.NotNull(extent);
        Assert.Equal(25832, extent.Srid);

        var queryRequest = Assert.Single(requestUris);
        var query = ParseQuery(new Uri(queryRequest));

        Assert.Equal("true", query["returnExtentOnly"]);
        Assert.Equal("25832", query["defaultSR"]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task QueryCountAsync_Throws_WhenDefaultSridIsNotPositive(int defaultSrid) {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetLayerClient(0).QueryCountAsync(
                new FeatureQuery {
                    DefaultSrid = defaultSrid
                },
                cancellationToken));

        Assert.Contains("DefaultSrid", exception.Message, StringComparison.Ordinal);
        Assert.Contains("greater than zero", exception.Message, StringComparison.OrdinalIgnoreCase);
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
            "supportsDefaultSR": true
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