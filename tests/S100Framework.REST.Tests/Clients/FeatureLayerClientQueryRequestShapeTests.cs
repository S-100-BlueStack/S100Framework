using System.Net.Http;
using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureLayerClientQueryRequestShapeTests
{
    [Fact]
    public async Task QueryAsync_IncludesResultWindowDefaultSridAndSqlFormat_WhenPaginationIsSupported() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<string>();

        var client = CreateClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;
            requestUris.Add(uri);

            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse(supportsPagination: true);
            }

            if (uri.Contains("/FeatureServer/0/query?", StringComparison.OrdinalIgnoreCase)) {
                return StubHttpMessageHandler.Json("""
                {
                  "objectIdFieldName": "OBJECTID",
                  "features": [
                    {
                      "attributes": { "OBJECTID": 6, "NAME": "Feature 6" },
                      "geometry": { "x": 10, "y": 20 }
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        var layerClient = client.GetLayerClient(0);
        var results = new List<FeatureRecord>();

        await foreach (var feature in layerClient.QueryAsync(
            new FeatureQuery {
                ResultOffset = 5,
                ResultRecordCount = 3,
                DefaultSrid = 25832,
                SqlFormat = FeatureQuerySqlFormat.Standard
            },
            cancellationToken)) {
            results.Add(feature);
        }

        Assert.Single(results);

        var queryRequest = Assert.Single(
            requestUris,
            uri => uri.Contains("/FeatureServer/0/query?", StringComparison.OrdinalIgnoreCase));

        var decodedQueryRequest = Uri.UnescapeDataString(queryRequest);

        Assert.Contains("resultOffset=5", decodedQueryRequest, StringComparison.Ordinal);
        Assert.Contains("resultRecordCount=3", decodedQueryRequest, StringComparison.Ordinal);
        Assert.Contains("defaultSR=25832", decodedQueryRequest, StringComparison.Ordinal);
        Assert.Contains("sqlFormat=standard", decodedQueryRequest, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(FeatureQuerySqlFormat.None, "none")]
    [InlineData(FeatureQuerySqlFormat.Standard, "standard")]
    [InlineData(FeatureQuerySqlFormat.Native, "native")]
    public async Task QueryCountAsync_IncludesDefaultSridAndSqlFormat_WhenProvided(
        FeatureQuerySqlFormat sqlFormat,
        string expectedSqlFormat) {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<string>();

        var client = CreateClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;
            requestUris.Add(uri);

            if (uri.Contains("/FeatureServer/0/query?", StringComparison.OrdinalIgnoreCase)) {
                return StubHttpMessageHandler.Json("""
                {
                  "count": 7
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        var layerClient = client.GetLayerClient(0);

        var count = await layerClient.QueryCountAsync(
            new FeatureQuery {
                DefaultSrid = 25832,
                SqlFormat = sqlFormat
            },
            cancellationToken);

        Assert.Equal(7, count);

        var queryRequest = Assert.Single(requestUris);
        var decodedQueryRequest = Uri.UnescapeDataString(queryRequest);

        Assert.Contains("returnCountOnly=true", decodedQueryRequest, StringComparison.Ordinal);
        Assert.Contains("defaultSR=25832", decodedQueryRequest, StringComparison.Ordinal);
        Assert.Contains($"sqlFormat={expectedSqlFormat}", decodedQueryRequest, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryAsync_Throws_WhenResultOffsetIsProvidedAndPaginationIsNotSupported() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse(supportsPagination: false);
            }

            throw new InvalidOperationException("HTTP should not be called.");
        });

        var layerClient = client.GetLayerClient(0);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => {
            await foreach (var _ in layerClient.QueryAsync(
                new FeatureQuery {
                    ResultOffset = 1
                },
                cancellationToken)) {
            }
        });

        Assert.Contains("supports pagination", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QueryAsync_Throws_WhenResultOffsetIsNegative() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = CreateClient(_ => throw new InvalidOperationException("HTTP should not be called."));
        var layerClient = client.GetLayerClient(0);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => {
            await foreach (var _ in layerClient.QueryAsync(
                new FeatureQuery {
                    ResultOffset = -1
                },
                cancellationToken)) {
            }
        });

        Assert.Contains("ResultOffset", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QueryAsync_Throws_WhenResultRecordCountIsZero() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = CreateClient(_ => throw new InvalidOperationException("HTTP should not be called."));
        var layerClient = client.GetLayerClient(0);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => {
            await foreach (var _ in layerClient.QueryAsync(
                new FeatureQuery {
                    ResultRecordCount = 0
                },
                cancellationToken)) {
            }
        });

        Assert.Contains("ResultRecordCount", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QueryCountAsync_Throws_WhenDefaultSridIsZero() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = CreateClient(_ => throw new InvalidOperationException("HTTP should not be called."));
        var layerClient = client.GetLayerClient(0);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            layerClient.QueryCountAsync(
                new FeatureQuery {
                    DefaultSrid = 0
                },
                cancellationToken));

        Assert.Contains("DefaultSrid", exception.Message, StringComparison.OrdinalIgnoreCase);
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

    private static HttpResponseMessage CreateLayerMetadataResponse(bool supportsPagination) {
        return StubHttpMessageHandler.Json($$"""
        {
          "id": 0,
          "name": "Layer 0",
          "geometryType": "esriGeometryPoint",
          "objectIdField": "OBJECTID",
          "maxRecordCount": 1000,
          "advancedQueryCapabilities": {
            "supportsPagination": {{supportsPagination.ToString().ToLowerInvariant()}}
          },
          "fields": [
            { "name": "OBJECTID", "type": "esriFieldTypeOID", "nullable": false },
            { "name": "NAME", "type": "esriFieldTypeString", "nullable": true }
          ],
          "relationships": [],
          "extent": {
            "spatialReference": { "wkid": 4326, "latestWkid": 4326 }
          }
        }
        """);
    }
}