using NetTopologySuite.Geometries;
using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Exceptions;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureServiceClientServiceQueryTests
{
    [Fact]
    public async Task QueryAsync_SendsServiceQueryAndMapsLayerResults() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<Uri>();

        var client = CreateClient(request => {
            requestUris.Add(request.RequestUri!);

            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse(capabilities: "Query");
            }

            if (request.RequestUri!.AbsolutePath.EndsWith(
                "/FeatureServer/query",
                StringComparison.OrdinalIgnoreCase)) {
                Assert.Equal(HttpMethod.Get, request.Method);

                return StubHttpMessageHandler.Json("""
                {
                  "layers": [
                    {
                      "id": 0,
                      "objectIdFieldName": "OBJECTID",
                      "geometryType": "esriGeometryPoint",
                      "spatialReference": {
                        "wkid": 4326,
                        "latestWkid": 4326
                      },
                      "exceededTransferLimit": false,
                      "features": [
                        {
                          "attributes": {
                            "OBJECTID": 10,
                            "NAME": "Harbor A",
                            "DEPTH": 12.5
                          },
                          "geometry": {
                            "x": 12.34,
                            "y": 56.78,
                            "spatialReference": {
                              "wkid": 4326
                            }
                          }
                        }
                      ]
                    },
                    {
                      "id": 1,
                      "objectIdFieldName": "OBJECTID",
                      "features": [
                        {
                          "attributes": {
                            "OBJECTID": 20,
                            "NAME": "Table Row"
                          }
                        }
                      ]
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var result = await client.QueryAsync(
            new FeatureServiceQueryRequest {
                LayerDefinitions = [
                    new FeatureServiceLayerQueryDefinition {
                        LayerId = 0,
                        Where = "STATUS = 'Active'",
                        OutFields = ["OBJECTID", "NAME", "DEPTH"]
                    },
                    new FeatureServiceLayerQueryDefinition {
                        LayerId = 1,
                        Where = "OBJECTID < 100",
                        OutFields = ["OBJECTID", "NAME"]
                    }
                ],
                ReturnGeometry = true,
                OutSrid = 4326,
                TimeExtent = new FeatureTimeExtent(
                    new DateTimeOffset(2026, 01, 01, 0, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 02, 01, 0, 0, 0, TimeSpan.Zero)),
                ReturnZ = false,
                ReturnM = false,
                GeometryPrecision = 3,
                MaxAllowableOffset = 2,
                SqlFormat = FeatureQuerySqlFormat.Standard,
                TimeReferenceUnknownClient = true
            },
            cancellationToken);

        Assert.Equal(2, result.Layers.Count);

        var layer0 = result.Layers[0];
        Assert.Equal(0, layer0.LayerId);
        Assert.False(layer0.ExceededTransferLimit);

        var feature = Assert.Single(layer0.Records);
        Assert.Equal(10, feature.ObjectId);
        Assert.Equal("Harbor A", feature.Attributes["NAME"]);
        Assert.Equal(12.5m, feature.Attributes["DEPTH"]);

        var point = Assert.IsType<Point>(feature.Geometry);
        Assert.Equal(12.34, point.X);
        Assert.Equal(56.78, point.Y);

        var layer1 = result.Layers[1];
        Assert.Equal(1, layer1.LayerId);

        var tableRecord = Assert.Single(layer1.Records);
        Assert.Equal(20, tableRecord.ObjectId);
        Assert.Null(tableRecord.Geometry);
        Assert.Equal("Table Row", tableRecord.Attributes["NAME"]);

        var queryRequest = Assert.Single(
            requestUris,
            uri => uri.AbsolutePath.EndsWith(
                "/FeatureServer/query",
                StringComparison.OrdinalIgnoreCase));

        var query = ParseQuery(queryRequest);

        Assert.Equal("json", query["f"]);
        Assert.Equal("true", query["returnGeometry"]);
        Assert.Equal("4326", query["outSR"]);
        Assert.Equal("false", query["returnZ"]);
        Assert.Equal("false", query["returnM"]);
        Assert.Equal("3", query["geometryPrecision"]);
        Assert.Equal("2", query["maxAllowableOffset"]);
        Assert.Equal("standard", query["sqlFormat"]);
        Assert.Equal("true", query["timeReferenceUnknownClient"]);
        Assert.Contains("\"layerId\":0", query["layerDefs"], StringComparison.Ordinal);
        Assert.Contains("\"where\":\"STATUS = \\u0027Active\\u0027\"", query["layerDefs"], StringComparison.Ordinal);
        Assert.Contains("\"outFields\":\"OBJECTID,NAME,DEPTH\"", query["layerDefs"], StringComparison.Ordinal);
        Assert.Contains("\"layerId\":1", query["layerDefs"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryAsync_AppliesSpatialFilter() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<Uri>();

        var client = CreateClient(request => {
            requestUris.Add(request.RequestUri!);

            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse(capabilities: "Query");
            }

            if (request.RequestUri!.AbsolutePath.EndsWith(
                "/FeatureServer/query",
                StringComparison.OrdinalIgnoreCase)) {
                return StubHttpMessageHandler.Json("""
                {
                  "layers": []
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        await client.QueryAsync(
            new FeatureServiceQueryRequest {
                LayerDefinitions = [
                    new FeatureServiceLayerQueryDefinition {
                        LayerId = 0
                    }
                ],
                SpatialFilter = FeatureSpatialFilter.FromEnvelope(
                    new Envelope(10, 11, 55, 56),
                    4326)
            },
            cancellationToken);

        var queryRequest = Assert.Single(
            requestUris,
            uri => uri.AbsolutePath.EndsWith(
                "/FeatureServer/query",
                StringComparison.OrdinalIgnoreCase));

        var query = ParseQuery(queryRequest);

        Assert.Equal("esriGeometryEnvelope", query["geometryType"]);
        Assert.Equal("4326", query["inSR"]);
        Assert.Equal("esriSpatialRelIntersects", query["spatialRel"]);
        Assert.Contains("\"xmin\":10", query["geometry"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryAsync_Throws_WhenServiceDoesNotAdvertiseQuerySupport() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse(capabilities: "Uploads");
            }

            throw new InvalidOperationException("HTTP should not be called after metadata lookup.");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceCapabilityException>(() =>
            client.QueryAsync(
                new FeatureServiceQueryRequest {
                    LayerDefinitions = [
                        new FeatureServiceLayerQueryDefinition {
                            LayerId = 0
                        }
                    ]
                },
                cancellationToken));

        Assert.Contains("query", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QueryAsync_Throws_WhenLayerDefinitionsAreMissing() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.QueryAsync(
                new FeatureServiceQueryRequest(),
                cancellationToken));

        Assert.Contains("layer definition", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QueryAsync_Throws_WhenLayerDefinitionsContainDuplicates() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.QueryAsync(
                new FeatureServiceQueryRequest {
                    LayerDefinitions = [
                        new FeatureServiceLayerQueryDefinition {
                            LayerId = 0
                        },
                        new FeatureServiceLayerQueryDefinition {
                            LayerId = 0
                        }
                    ]
                },
                cancellationToken));

        Assert.Contains("Duplicate", exception.Message, StringComparison.Ordinal);
    }

    private static FeatureServiceClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> handler) {
        return new FeatureServiceClient(
            new HttpClient(new StubHttpMessageHandler(handler)),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });
    }

    private static bool IsServiceMetadataRequest(HttpRequestMessage request) {
        return request.RequestUri?.AbsolutePath.EndsWith(
            "/FeatureServer",
            StringComparison.OrdinalIgnoreCase) == true;
    }

    private static HttpResponseMessage CreateServiceMetadataResponse(string capabilities) {
        return StubHttpMessageHandler.Json($$"""
        {
          "layers": [
            { "id": 0, "name": "Harbors" }
          ],
          "tables": [
            { "id": 1, "name": "Inspections" }
          ],
          "capabilities": "{{capabilities}}"
        }
        """);
    }

    private static Dictionary<string, string> ParseQuery(Uri uri) {
        return uri.Query
            .TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .ToDictionary(
                parts => Uri.UnescapeDataString(parts[0]),
                parts => parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty,
                StringComparer.Ordinal);
    }
}