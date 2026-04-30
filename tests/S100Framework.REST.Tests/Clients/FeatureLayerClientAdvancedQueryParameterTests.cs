using NetTopologySuite.Geometries;
using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureLayerClientAdvancedQueryParameterTests
{
    [Fact]
    public async Task QueryAsync_IncludesAdvancedQueryParameters_WhenProvided() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<string>();

        var client = CreateClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;
            requestUris.Add(uri);

            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse();
            }

            if (uri.Contains("/FeatureServer/0/query?", StringComparison.OrdinalIgnoreCase)) {
                return StubHttpMessageHandler.Json("""
                {
                  "objectIdFieldName": "OBJECTID",
                  "features": [
                    {
                      "attributes": { "OBJECTID": 1 },
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
                ResultType = FeatureQueryResultType.Tile,
                ReturnExceededLimitFeatures = false,
                DatumTransformationWkid = 1623,
                QuantizationParametersJson = """
                {
                  "mode": "view",
                  "originPosition": "upperLeft",
                  "tolerance": 1,
                  "extent": {
                    "xmin": 0,
                    "ymin": 0,
                    "xmax": 10,
                    "ymax": 10,
                    "spatialReference": { "wkid": 4326 }
                  }
                }
                """,
                MultipatchOption = FeatureQueryMultipatchOption.Extent
            },
            cancellationToken)) {
            results.Add(feature);
        }

        Assert.Single(results);

        var queryRequest = Assert.Single(
            requestUris,
            uri => uri.Contains("/FeatureServer/0/query?", StringComparison.OrdinalIgnoreCase));

        var decodedQueryRequest = Uri.UnescapeDataString(queryRequest);

        Assert.Contains("resultType=tile", decodedQueryRequest, StringComparison.Ordinal);
        Assert.Contains("returnExceededLimitFeatures=false", decodedQueryRequest, StringComparison.Ordinal);
        Assert.Contains("datumTransformation=1623", decodedQueryRequest, StringComparison.Ordinal);
        Assert.Contains("quantizationParameters=", decodedQueryRequest, StringComparison.Ordinal);
        Assert.Contains("\"mode\": \"view\"", decodedQueryRequest, StringComparison.Ordinal);
        Assert.Contains("multipatchOption=extent", decodedQueryRequest, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryAsync_IncludesReturnDistinctValues_WhenProvided() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<string>();

        var client = CreateClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;
            requestUris.Add(uri);

            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse();
            }

            if (uri.Contains("/FeatureServer/0/query?", StringComparison.OrdinalIgnoreCase)) {
                return StubHttpMessageHandler.Json("""
                {
                  "objectIdFieldName": "OBJECTID",
                  "features": [
                    {
                      "attributes": {
                        "CATEGORY": "A"
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
                OutFields = ["CATEGORY"],
                ReturnGeometry = false,
                ReturnDistinctValues = true
            },
            cancellationToken)) {
            results.Add(feature);
        }

        var result = Assert.Single(results);

        Assert.Null(result.Geometry);
        Assert.Equal("A", result.Attributes["CATEGORY"]);

        var queryRequest = Assert.Single(
            requestUris,
            uri => uri.Contains("/FeatureServer/0/query?", StringComparison.OrdinalIgnoreCase));

        var decodedQueryRequest = Uri.UnescapeDataString(queryRequest);

        Assert.Contains("outFields=CATEGORY", decodedQueryRequest, StringComparison.Ordinal);
        Assert.Contains("returnGeometry=false", decodedQueryRequest, StringComparison.Ordinal);
        Assert.Contains("returnDistinctValues=true", decodedQueryRequest, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryCountAsync_IncludesReturnDistinctValues_WhenProvided() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<string>();

        var client = CreateClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;
            requestUris.Add(uri);

            if (uri.Contains("/FeatureServer/0/query?", StringComparison.OrdinalIgnoreCase)) {
                return StubHttpMessageHandler.Json("""
                {
                  "count": 3
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        var layerClient = client.GetLayerClient(0);

        var count = await layerClient.QueryCountAsync(
            new FeatureQuery {
                OutFields = ["CATEGORY"],
                ReturnDistinctValues = true
            },
            cancellationToken);

        Assert.Equal(3, count);

        var queryRequest = Assert.Single(requestUris);
        var decodedQueryRequest = Uri.UnescapeDataString(queryRequest);

        Assert.Contains("returnCountOnly=true", decodedQueryRequest, StringComparison.Ordinal);
        Assert.DoesNotContain("outFields=", decodedQueryRequest, StringComparison.Ordinal);
        Assert.Contains("returnDistinctValues=true", decodedQueryRequest, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryAsync_IncludesReturnCentroidAndMapsCentroid_WhenProvided() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<string>();

        var client = CreateClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;
            requestUris.Add(uri);

            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse();
            }

            if (uri.Contains("/FeatureServer/0/query?", StringComparison.OrdinalIgnoreCase)) {
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
                      "centroid": {
                        "x": 12.34,
                        "y": 56.78,
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
                ReturnGeometry = false,
                ReturnCentroid = true
            },
            cancellationToken)) {
            results.Add(feature);
        }

        var result = Assert.Single(results);

        Assert.Equal(10, result.ObjectId);
        Assert.Equal("Area A", result.Attributes["NAME"]);
        Assert.Null(result.Geometry);

        var centroid = Assert.IsType<Point>(result.Centroid);
        Assert.Equal(12.34, centroid.X);
        Assert.Equal(56.78, centroid.Y);
        Assert.Equal(25832, centroid.SRID);

        var queryRequest = Assert.Single(
            requestUris,
            uri => uri.Contains("/FeatureServer/0/query?", StringComparison.OrdinalIgnoreCase));

        var decodedQueryRequest = Uri.UnescapeDataString(queryRequest);

        Assert.Contains("returnGeometry=false", decodedQueryRequest, StringComparison.Ordinal);
        Assert.Contains("returnCentroid=true", decodedQueryRequest, StringComparison.Ordinal);
        Assert.Contains("outFields=OBJECTID,NAME", decodedQueryRequest, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryAsync_IncludesReturnCentroidFalse_WhenExplicitlyProvided() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<string>();

        var client = CreateClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;
            requestUris.Add(uri);

            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse();
            }

            if (uri.Contains("/FeatureServer/0/query?", StringComparison.OrdinalIgnoreCase)) {
                return StubHttpMessageHandler.Json("""
                {
                  "objectIdFieldName": "OBJECTID",
                  "features": []
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        var layerClient = client.GetLayerClient(0);

        await foreach (var _ in layerClient.QueryAsync(
            new FeatureQuery {
                ReturnCentroid = false
            },
            cancellationToken)) {
        }

        var queryRequest = Assert.Single(
            requestUris,
            uri => uri.Contains("/FeatureServer/0/query?", StringComparison.OrdinalIgnoreCase));

        var decodedQueryRequest = Uri.UnescapeDataString(queryRequest);

        Assert.Contains("returnCentroid=false", decodedQueryRequest, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryCountAsync_DoesNotSendReturnCentroid_WhenProvided() {
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
                ReturnCentroid = true
            },
            cancellationToken);

        Assert.Equal(7, count);

        var queryRequest = Assert.Single(requestUris);
        var decodedQueryRequest = Uri.UnescapeDataString(queryRequest);

        Assert.Contains("returnCountOnly=true", decodedQueryRequest, StringComparison.Ordinal);
        Assert.DoesNotContain("returnCentroid=", decodedQueryRequest, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryAsync_MapsMissingAndNullCentroidToNull() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;

            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse();
            }

            if (uri.Contains("/FeatureServer/0/query?", StringComparison.OrdinalIgnoreCase)) {
                return StubHttpMessageHandler.Json("""
                {
                  "objectIdFieldName": "OBJECTID",
                  "geometryType": "esriGeometryPolygon",
                  "features": [
                    {
                      "attributes": {
                        "OBJECTID": 10,
                        "NAME": "Missing centroid"
                      }
                    },
                    {
                      "attributes": {
                        "OBJECTID": 11,
                        "NAME": "Null centroid"
                      },
                      "centroid": null
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
                ReturnGeometry = false,
                ReturnCentroid = true
            },
            cancellationToken)) {
            results.Add(feature);
        }

        Assert.Equal(2, results.Count);

        Assert.Equal(10, results[0].ObjectId);
        Assert.Equal("Missing centroid", results[0].Attributes["NAME"]);
        Assert.Null(results[0].Centroid);

        Assert.Equal(11, results[1].ObjectId);
        Assert.Equal("Null centroid", results[1].Attributes["NAME"]);
        Assert.Null(results[1].Centroid);
    }

    [Fact]
    public async Task QueryAsync_Throws_WhenCentroidPayloadIsUnsupported() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;

            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse();
            }

            if (uri.Contains("/FeatureServer/0/query?", StringComparison.OrdinalIgnoreCase)) {
                return StubHttpMessageHandler.Json("""
                {
                  "objectIdFieldName": "OBJECTID",
                  "geometryType": "esriGeometryPolygon",
                  "features": [
                    {
                      "attributes": {
                        "OBJECTID": 10
                      },
                      "centroid": [12.34, 56.78]
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        var layerClient = client.GetLayerClient(0);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => {
            await foreach (var _ in layerClient.QueryAsync(
                new FeatureQuery {
                    ReturnGeometry = false,
                    ReturnCentroid = true
                },
                cancellationToken)) {
            }
        });

        Assert.Contains("centroid", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QueryAsync_DoesNotSendReturnCentroid_WhenNotProvided() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<string>();

        var client = CreateClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;
            requestUris.Add(uri);

            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse();
            }

            if (uri.Contains("/FeatureServer/0/query?", StringComparison.OrdinalIgnoreCase)) {
                return StubHttpMessageHandler.Json("""
                {
                  "objectIdFieldName": "OBJECTID",
                  "features": []
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        var layerClient = client.GetLayerClient(0);

        await foreach (var _ in layerClient.QueryAsync(
            new FeatureQuery(),
            cancellationToken)) {
        }

        var queryRequest = Assert.Single(
            requestUris,
            uri => uri.Contains("/FeatureServer/0/query?", StringComparison.OrdinalIgnoreCase));

        var decodedQueryRequest = Uri.UnescapeDataString(queryRequest);

        Assert.DoesNotContain("returnCentroid=", decodedQueryRequest, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryAsync_IncludesStandardResultTypeAndReturnExceededLimitFeaturesTrue_WhenProvided() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<string>();

        var client = CreateClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;
            requestUris.Add(uri);

            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse();
            }

            if (uri.Contains("/FeatureServer/0/query?", StringComparison.OrdinalIgnoreCase)) {
                return StubHttpMessageHandler.Json("""
                {
                  "objectIdFieldName": "OBJECTID",
                  "features": []
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        var layerClient = client.GetLayerClient(0);

        await foreach (var _ in layerClient.QueryAsync(
            new FeatureQuery {
                ResultType = FeatureQueryResultType.Standard,
                ReturnExceededLimitFeatures = true
            },
            cancellationToken)) {
        }

        var queryRequest = Assert.Single(
            requestUris,
            uri => uri.Contains("/FeatureServer/0/query?", StringComparison.OrdinalIgnoreCase));

        var decodedQueryRequest = Uri.UnescapeDataString(queryRequest);

        Assert.Contains("resultType=standard", decodedQueryRequest, StringComparison.Ordinal);
        Assert.Contains("returnExceededLimitFeatures=true", decodedQueryRequest, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryAsync_DoesNotSendResultTypeParameters_WhenNotProvided() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<string>();

        var client = CreateClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;
            requestUris.Add(uri);

            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse();
            }

            if (uri.Contains("/FeatureServer/0/query?", StringComparison.OrdinalIgnoreCase)) {
                return StubHttpMessageHandler.Json("""
                {
                  "objectIdFieldName": "OBJECTID",
                  "features": []
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        var layerClient = client.GetLayerClient(0);

        await foreach (var _ in layerClient.QueryAsync(
            new FeatureQuery(),
            cancellationToken)) {
        }

        var queryRequest = Assert.Single(
            requestUris,
            uri => uri.Contains("/FeatureServer/0/query?", StringComparison.OrdinalIgnoreCase));

        var decodedQueryRequest = Uri.UnescapeDataString(queryRequest);

        Assert.DoesNotContain("resultType=", decodedQueryRequest, StringComparison.Ordinal);
        Assert.DoesNotContain("returnExceededLimitFeatures=", decodedQueryRequest, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryCountAsync_IncludesResultTypeAndReturnExceededLimitFeatures_WhenProvided() {
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
                ResultType = FeatureQueryResultType.Tile,
                ReturnExceededLimitFeatures = false
            },
            cancellationToken);

        Assert.Equal(7, count);

        var queryRequest = Assert.Single(requestUris);
        var decodedQueryRequest = Uri.UnescapeDataString(queryRequest);

        Assert.Contains("returnCountOnly=true", decodedQueryRequest, StringComparison.Ordinal);
        Assert.Contains("resultType=tile", decodedQueryRequest, StringComparison.Ordinal);
        Assert.Contains("returnExceededLimitFeatures=false", decodedQueryRequest, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryObjectIdsAsync_IncludesResultTypeAndReturnExceededLimitFeatures_WhenProvided() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<string>();

        var client = CreateClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;
            requestUris.Add(uri);

            if (uri.Contains("/FeatureServer/0/query?", StringComparison.OrdinalIgnoreCase)) {
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
                ResultType = FeatureQueryResultType.Tile,
                ReturnExceededLimitFeatures = true
            },
            cancellationToken);

        Assert.Equal([10, 20], objectIds);

        var queryRequest = Assert.Single(requestUris);
        var decodedQueryRequest = Uri.UnescapeDataString(queryRequest);

        Assert.Contains("returnIdsOnly=true", decodedQueryRequest, StringComparison.Ordinal);
        Assert.Contains("resultType=tile", decodedQueryRequest, StringComparison.Ordinal);
        Assert.Contains("returnExceededLimitFeatures=true", decodedQueryRequest, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryExtentAsync_IncludesResultTypeAndReturnExceededLimitFeatures_WhenProvided() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<string>();

        var client = CreateClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;
            requestUris.Add(uri);

            if (uri.Contains("/FeatureServer/0/query?", StringComparison.OrdinalIgnoreCase)) {
                return StubHttpMessageHandler.Json("""
                {
                  "count": 3,
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

        var layerClient = client.GetLayerClient(0);

        var extent = await layerClient.QueryExtentAsync(
            new FeatureQuery {
                ResultType = FeatureQueryResultType.Standard,
                ReturnExceededLimitFeatures = false
            },
            cancellationToken);

        Assert.NotNull(extent);
        Assert.Equal(25832, extent.Srid);

        var queryRequest = Assert.Single(requestUris);
        var decodedQueryRequest = Uri.UnescapeDataString(queryRequest);

        Assert.Contains("returnExtentOnly=true", decodedQueryRequest, StringComparison.Ordinal);
        Assert.Contains("resultType=standard", decodedQueryRequest, StringComparison.Ordinal);
        Assert.Contains("returnExceededLimitFeatures=false", decodedQueryRequest, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryCountAsync_IncludesDatumTransformationJson_WhenProvided() {
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
                DatumTransformationJson = """
                {
                  "geoTransforms": [
                    {
                      "wkid": 108190,
                      "forward": true
                    }
                  ]
                }
                """
            },
            cancellationToken);

        Assert.Equal(7, count);

        var queryRequest = Assert.Single(requestUris);
        var decodedQueryRequest = Uri.UnescapeDataString(queryRequest);

        Assert.Contains("returnCountOnly=true", decodedQueryRequest, StringComparison.Ordinal);
        Assert.Contains("datumTransformation=", decodedQueryRequest, StringComparison.Ordinal);
        Assert.Contains("\"geoTransforms\"", decodedQueryRequest, StringComparison.Ordinal);
        Assert.Contains("\"wkid\": 108190", decodedQueryRequest, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryCountAsync_Throws_WhenDatumTransformationFormsAreCombined() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = CreateClient(_ => throw new InvalidOperationException("HTTP should not be called."));
        var layerClient = client.GetLayerClient(0);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            layerClient.QueryCountAsync(
                new FeatureQuery {
                    DatumTransformationWkid = 1623,
                    DatumTransformationJson = """{ "wkid": 1623 }"""
                },
                cancellationToken));

        Assert.Contains("DatumTransformation", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QueryCountAsync_Throws_WhenQuantizationParametersJsonIsInvalid() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = CreateClient(_ => throw new InvalidOperationException("HTTP should not be called."));
        var layerClient = client.GetLayerClient(0);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            layerClient.QueryCountAsync(
                new FeatureQuery {
                    QuantizationParametersJson = "not-json"
                },
                cancellationToken));

        Assert.Contains("QuantizationParametersJson", exception.Message, StringComparison.OrdinalIgnoreCase);
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

    private static HttpResponseMessage CreateLayerMetadataResponse() {
        return StubHttpMessageHandler.Json("""
        {
          "id": 0,
          "name": "Layer 0",
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
          "extent": {
            "spatialReference": { "wkid": 4326, "latestWkid": 4326 }
          }
        }
        """);
    }
}