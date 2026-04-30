using System.Net.Http;
using NetTopologySuite.Geometries;
using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureLayerClientReturnEnvelopeQueryTests
{
    [Fact]
    public async Task QueryAsync_IncludesReturnEnvelopeAndMapsEnvelopeGeometry_WhenProvided() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<string>();

        var client = CreateClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;
            requestUris.Add(uri);

            if (IsLayerMetadataRequest(request)) {
                return CreatePolygonLayerMetadataResponse();
            }

            if (IsLayerQueryRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "objectIdFieldName": "OBJECTID",
                  "geometryType": "esriGeometryPolygon",
                  "spatialReference": {
                    "wkid": 25832,
                    "latestWkid": 25832
                  },
                  "features": [
                    {
                      "attributes": {
                        "OBJECTID": 10,
                        "NAME": "Area A"
                      },
                      "geometry": {
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
                OutFields = ["OBJECTID", "NAME"],
                ReturnEnvelope = true
            },
            cancellationToken)) {
            results.Add(feature);
        }

        var result = Assert.Single(results);

        Assert.Equal(10, result.ObjectId);
        Assert.Equal("Area A", result.Attributes["NAME"]);

        var geometry = Assert.IsType<Polygon>(result.Geometry);
        Assert.Equal(25832, geometry.SRID);
        Assert.Equal(10.0, geometry.EnvelopeInternal.MinX);
        Assert.Equal(11.0, geometry.EnvelopeInternal.MaxX);
        Assert.Equal(55.0, geometry.EnvelopeInternal.MinY);
        Assert.Equal(56.0, geometry.EnvelopeInternal.MaxY);

        var queryRequest = Assert.Single(
            requestUris,
            uri => uri.Contains("/FeatureServer/0/query?", StringComparison.OrdinalIgnoreCase));

        var decodedQueryRequest = Uri.UnescapeDataString(queryRequest);

        Assert.Contains("returnGeometry=true", decodedQueryRequest, StringComparison.Ordinal);
        Assert.Contains("returnEnvelope=true", decodedQueryRequest, StringComparison.Ordinal);
        Assert.Contains("outFields=OBJECTID,NAME", decodedQueryRequest, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryAsync_DoesNotSendReturnEnvelope_WhenNotProvided() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<string>();

        var client = CreateClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;
            requestUris.Add(uri);

            if (IsLayerMetadataRequest(request)) {
                return CreatePolygonLayerMetadataResponse();
            }

            if (IsLayerQueryRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "objectIdFieldName": "OBJECTID",
                  "geometryType": "esriGeometryPolygon",
                  "features": []
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        var layerClient = client.GetLayerClient(0);

        await foreach (var _ in layerClient.QueryAsync(
            new FeatureQuery {
                ReturnGeometry = true
            },
            cancellationToken)) {
        }

        var queryRequest = Assert.Single(
            requestUris,
            uri => uri.Contains("/FeatureServer/0/query?", StringComparison.OrdinalIgnoreCase));

        var decodedQueryRequest = Uri.UnescapeDataString(queryRequest);

        Assert.Contains("returnGeometry=true", decodedQueryRequest, StringComparison.Ordinal);
        Assert.DoesNotContain("returnEnvelope=", decodedQueryRequest, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryAsync_Throws_WhenReturnEnvelopeIsUsedWithoutReturnGeometry() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var layerClient = client.GetLayerClient(0);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => {
            await foreach (var _ in layerClient.QueryAsync(
                new FeatureQuery {
                    ReturnGeometry = false,
                    ReturnEnvelope = true
                },
                cancellationToken)) {
            }
        });

        Assert.Contains("ReturnEnvelope", exception.Message, StringComparison.Ordinal);
        Assert.Contains("ReturnGeometry", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryCountAsync_DoesNotSendReturnEnvelope_WhenProvided() {
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

        var layerClient = client.GetLayerClient(0);

        var count = await layerClient.QueryCountAsync(
            new FeatureQuery {
                ReturnEnvelope = true
            },
            cancellationToken);

        Assert.Equal(7, count);

        var queryRequest = Assert.Single(requestUris);
        var decodedQueryRequest = Uri.UnescapeDataString(queryRequest);

        Assert.Contains("returnCountOnly=true", decodedQueryRequest, StringComparison.Ordinal);
        Assert.DoesNotContain("returnEnvelope=", decodedQueryRequest, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryObjectIdsAsync_DoesNotSendReturnEnvelope_WhenProvided() {
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

        var layerClient = client.GetLayerClient(0);

        var objectIds = await layerClient.QueryObjectIdsAsync(
            new FeatureQuery {
                ReturnEnvelope = true
            },
            cancellationToken);

        Assert.Equal([10, 20], objectIds);

        var queryRequest = Assert.Single(requestUris);
        var decodedQueryRequest = Uri.UnescapeDataString(queryRequest);

        Assert.Contains("returnIdsOnly=true", decodedQueryRequest, StringComparison.Ordinal);
        Assert.DoesNotContain("returnEnvelope=", decodedQueryRequest, StringComparison.Ordinal);
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

    private static HttpResponseMessage CreatePolygonLayerMetadataResponse() {
        return StubHttpMessageHandler.Json("""
        {
          "id": 0,
          "name": "Layer 0",
          "geometryType": "esriGeometryPolygon",
          "objectIdField": "OBJECTID",
          "maxRecordCount": 1000,
          "advancedQueryCapabilities": {
            "supportsPagination": true,
            "supportsReturningGeometryEnvelope": true
          },
          "fields": [
            { "name": "OBJECTID", "type": "esriFieldTypeOID", "nullable": false },
            { "name": "NAME", "type": "esriFieldTypeString", "nullable": true }
          ],
          "relationships": [],
          "extent": {
            "spatialReference": {
              "wkid": 25832,
              "latestWkid": 25832
            }
          }
        }
        """);
    }
}