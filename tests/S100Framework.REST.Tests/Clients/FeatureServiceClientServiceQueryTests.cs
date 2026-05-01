using NetTopologySuite.Geometries;
using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Exceptions;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using System.Globalization;
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

    [Fact]
    public async Task QueryShapeAsyncMethods_SendReturnShapeFlagsAndMapLayerResults() {
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
                var query = ParseQuery(request.RequestUri);

                if (query.ContainsKey("returnCountOnly")) {
                    return StubHttpMessageHandler.Json("""
                    {
                      "layers": [
                        {
                          "id": 0,
                          "count": 42
                        },
                        {
                          "id": 1,
                          "count": 7
                        }
                      ]
                    }
                    """);
                }

                if (query.ContainsKey("returnIdsOnly")) {
                    return StubHttpMessageHandler.Json("""
                    {
                      "layers": [
                        {
                          "id": 0,
                          "objectIdFieldName": "OBJECTID",
                          "objectIds": [10, 20, 30]
                        },
                        {
                          "id": 1,
                          "objectIdFieldName": "OBJECTID",
                          "objectIds": [100]
                        }
                      ]
                    }
                    """);
                }

                if (query.ContainsKey("returnUniqueIdsOnly")) {
                    return StubHttpMessageHandler.Json("""
                    {
                      "layers": [
                        {
                          "id": 0,
                          "uniqueIdFieldNames": "GLOBALID",
                          "uniqueIds": ["a", "b", "c"],
                          "exceededTransferLimit": false
                        },
                        {
                          "id": 1,
                          "uniqueIdFieldNames": ["COUNTRY", "LOCAL_ID"],
                          "uniqueIds": [
                            ["DK", 100],
                            ["NO", 200]
                          ],
                          "exceededTransferLimit": true
                        }
                      ]
                    }
                    """);
                }
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var request = new FeatureServiceQueryRequest {
            LayerDefinitions = [
                new FeatureServiceLayerQueryDefinition {
                    LayerId = 0,
                    Where = "STATUS = 'Active'"
                },
                new FeatureServiceLayerQueryDefinition {
                    LayerId = 1,
                    Where = "INSPECTION_REQUIRED = 1"
                }
            ]
        };

        var countResult = await client.QueryCountAsync(request, cancellationToken);
        var objectIdsResult = await client.QueryObjectIdsAsync(request, cancellationToken);
        var uniqueIdsResult = await client.QueryUniqueIdsAsync(request, cancellationToken);

        Assert.Equal(2, countResult.Layers.Count);
        Assert.Equal(0, countResult.Layers[0].LayerId);
        Assert.Equal(42, countResult.Layers[0].Count);
        Assert.Equal(1, countResult.Layers[1].LayerId);
        Assert.Equal(7, countResult.Layers[1].Count);

        Assert.Equal(2, objectIdsResult.Layers.Count);
        Assert.Equal(0, objectIdsResult.Layers[0].LayerId);
        Assert.Equal("OBJECTID", objectIdsResult.Layers[0].ObjectIdFieldName);
        Assert.Equal([10, 20, 30], objectIdsResult.Layers[0].ObjectIds);
        Assert.Equal(1, objectIdsResult.Layers[1].LayerId);
        Assert.Equal([100], objectIdsResult.Layers[1].ObjectIds);

        Assert.Equal(2, uniqueIdsResult.Layers.Count);

        var simpleUniqueIdLayer = uniqueIdsResult.Layers[0];
        Assert.Equal(0, simpleUniqueIdLayer.LayerId);
        Assert.Equal(["GLOBALID"], simpleUniqueIdLayer.UniqueIdFieldNames);
        Assert.False(simpleUniqueIdLayer.IsComposite);
        Assert.False(simpleUniqueIdLayer.ExceededTransferLimit);
        Assert.Collection(
    simpleUniqueIdLayer.UniqueIds,
    uniqueId => Assert.Equal("a", uniqueId.SingleValue),
    uniqueId => Assert.Equal("b", uniqueId.SingleValue),
    uniqueId => Assert.Equal("c", uniqueId.SingleValue));

        var compositeUniqueIdLayer = uniqueIdsResult.Layers[1];
        Assert.Equal(1, compositeUniqueIdLayer.LayerId);
        Assert.Equal(["COUNTRY", "LOCAL_ID"], compositeUniqueIdLayer.UniqueIdFieldNames);
        Assert.True(compositeUniqueIdLayer.IsComposite);
        Assert.True(compositeUniqueIdLayer.ExceededTransferLimit);
        Assert.Null(compositeUniqueIdLayer.UniqueIds[0].SingleValue);
        Assert.Equal(["DK", "100"], compositeUniqueIdLayer.UniqueIds[0].Components);
        Assert.Equal(["NO", "200"], compositeUniqueIdLayer.UniqueIds[1].Components);

        var queryRequests = requestUris
            .Where(uri => uri.AbsolutePath.EndsWith(
                "/FeatureServer/query",
                StringComparison.OrdinalIgnoreCase))
            .Select(ParseQuery)
            .ToArray();

        Assert.Equal(3, queryRequests.Length);
        Assert.Equal("true", queryRequests[0]["returnCountOnly"]);
        Assert.False(queryRequests[0].ContainsKey("returnIdsOnly"));
        Assert.False(queryRequests[0].ContainsKey("returnUniqueIdsOnly"));
        Assert.Equal("true", queryRequests[1]["returnIdsOnly"]);
        Assert.False(queryRequests[1].ContainsKey("returnCountOnly"));
        Assert.False(queryRequests[1].ContainsKey("returnUniqueIdsOnly"));
        Assert.Equal("true", queryRequests[2]["returnUniqueIdsOnly"]);
        Assert.False(queryRequests[2].ContainsKey("returnCountOnly"));
        Assert.False(queryRequests[2].ContainsKey("returnIdsOnly"));
    }

    [Fact]
    public async Task QueryCountAsync_Throws_WhenServiceDoesNotAdvertiseQuerySupport() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse(capabilities: "Uploads");
            }

            throw new InvalidOperationException("HTTP should not be called after metadata lookup.");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceCapabilityException>(() =>
            client.QueryCountAsync(
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
    public async Task QueryUniqueIdsAsync_MapsEmptyAndMissingUniqueIdPayloads() {
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
                  "layers": [
                    {
                      "id": 0,
                      "uniqueIdFieldNames": null,
                      "uniqueIds": []
                    },
                    {
                      "id": 1
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var result = await client.QueryUniqueIdsAsync(
            new FeatureServiceQueryRequest {
                LayerDefinitions = [
                    new FeatureServiceLayerQueryDefinition {
                        LayerId = 0
                    },
                    new FeatureServiceLayerQueryDefinition {
                        LayerId = 1
                    }
                ]
            },
            cancellationToken);

        Assert.Equal(2, result.Layers.Count);

        Assert.Equal(0, result.Layers[0].LayerId);
        Assert.Empty(result.Layers[0].UniqueIdFieldNames);
        Assert.Empty(result.Layers[0].UniqueIds);
        Assert.False(result.Layers[0].IsComposite);
        Assert.Null(result.Layers[0].ExceededTransferLimit);

        Assert.Equal(1, result.Layers[1].LayerId);
        Assert.Empty(result.Layers[1].UniqueIdFieldNames);
        Assert.Empty(result.Layers[1].UniqueIds);
        Assert.False(result.Layers[1].IsComposite);
        Assert.Null(result.Layers[1].ExceededTransferLimit);

        var queryRequest = requestUris
            .Where(uri => uri.AbsolutePath.EndsWith(
                "/FeatureServer/query",
                StringComparison.OrdinalIgnoreCase))
            .Select(ParseQuery)
            .Single();

        Assert.Equal("true", queryRequest["returnUniqueIdsOnly"]);
        Assert.False(queryRequest.ContainsKey("returnCountOnly"));
        Assert.False(queryRequest.ContainsKey("returnIdsOnly"));
    }

    [Fact]
    public async Task QueryUniqueIdsAsync_Throws_WhenUniqueIdFieldNamesPayloadIsUnsupported() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse(capabilities: "Query");
            }

            if (request.RequestUri!.AbsolutePath.EndsWith(
                "/FeatureServer/query",
                StringComparison.OrdinalIgnoreCase)) {
                return StubHttpMessageHandler.Json("""
                {
                  "layers": [
                    {
                      "id": 0,
                      "uniqueIdFieldNames": {
                        "field": "GLOBALID"
                      },
                      "uniqueIds": []
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.QueryUniqueIdsAsync(
                new FeatureServiceQueryRequest {
                    LayerDefinitions = [
                        new FeatureServiceLayerQueryDefinition {
                            LayerId = 0
                        }
                    ]
                },
                cancellationToken));

        Assert.Contains("uniqueIdFieldNames", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryUniqueIdsAsync_Throws_WhenUniqueIdsPayloadIsUnsupported() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse(capabilities: "Query");
            }

            if (request.RequestUri!.AbsolutePath.EndsWith(
                "/FeatureServer/query",
                StringComparison.OrdinalIgnoreCase)) {
                return StubHttpMessageHandler.Json("""
                {
                  "layers": [
                    {
                      "id": 0,
                      "uniqueIdFieldNames": "GLOBALID",
                      "uniqueIds": {
                        "value": "abc"
                      }
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.QueryUniqueIdsAsync(
                new FeatureServiceQueryRequest {
                    LayerDefinitions = [
                        new FeatureServiceLayerQueryDefinition {
                            LayerId = 0
                        }
                    ]
                },
                cancellationToken));

        Assert.Contains("uniqueIds", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryUniqueIdsAsync_Throws_WhenUniqueIdComponentPayloadIsUnsupported() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse(capabilities: "Query");
            }

            if (request.RequestUri!.AbsolutePath.EndsWith(
                "/FeatureServer/query",
                StringComparison.OrdinalIgnoreCase)) {
                return StubHttpMessageHandler.Json("""
                {
                  "layers": [
                    {
                      "id": 0,
                      "uniqueIdFieldNames": ["COUNTRY", "LOCAL_ID"],
                      "uniqueIds": [
                        ["DK", { "value": 100 }]
                      ]
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.QueryUniqueIdsAsync(
                new FeatureServiceQueryRequest {
                    LayerDefinitions = [
                        new FeatureServiceLayerQueryDefinition {
                            LayerId = 0
                        }
                    ]
                },
                cancellationToken));

        Assert.Contains("unique ID component", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QueryExtentsAsync_UsesLayerQueryEndpointsAndMapsLayerExtents() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<Uri>();

        var client = CreateClient(request => {
            requestUris.Add(request.RequestUri!);

            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse(capabilities: "Query");
            }

            if (request.RequestUri!.AbsolutePath.EndsWith(
                "/FeatureServer/0/query",
                StringComparison.OrdinalIgnoreCase)) {
                return StubHttpMessageHandler.Json("""
                {
                  "count": 12,
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

            if (request.RequestUri!.AbsolutePath.EndsWith(
                "/FeatureServer/1/query",
                StringComparison.OrdinalIgnoreCase)) {
                return StubHttpMessageHandler.Json("""
                {
                  "count": 0,
                  "extent": null
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var result = await client.QueryExtentsAsync(
            new FeatureServiceQueryRequest {
                LayerDefinitions = [
                    new FeatureServiceLayerQueryDefinition {
                        LayerId = 0,
                        Where = "STATUS = 'Active'"
                    },
                    new FeatureServiceLayerQueryDefinition {
                        LayerId = 1,
                        Where = "INSPECTION_REQUIRED = 1"
                    }
                ],
                OutSrid = 25832,
                SpatialFilter = FeatureSpatialFilter.FromEnvelope(
                    new Envelope(9, 12, 54, 57),
                    4326),
                TimeInstant = DateTimeOffset.UnixEpoch,
                SqlFormat = FeatureQuerySqlFormat.Standard
            },
            cancellationToken);

        Assert.Equal(2, result.Layers.Count);

        var layer0 = result.Layers[0];
        Assert.Equal(0, layer0.LayerId);
        Assert.NotNull(layer0.Extent);
        Assert.Equal(10.0, layer0.Extent.Envelope.MinX);
        Assert.Equal(11.0, layer0.Extent.Envelope.MaxX);
        Assert.Equal(55.0, layer0.Extent.Envelope.MinY);
        Assert.Equal(56.0, layer0.Extent.Envelope.MaxY);
        Assert.Equal(25832, layer0.Extent.Srid);

        var layer1 = result.Layers[1];
        Assert.Equal(1, layer1.LayerId);
        Assert.Null(layer1.Extent);

        Assert.DoesNotContain(
            requestUris,
            uri => uri.AbsolutePath.EndsWith(
                "/FeatureServer/query",
                StringComparison.OrdinalIgnoreCase));

        var layerQueryRequests = requestUris
            .Where(uri => uri.AbsolutePath.EndsWith(
                "/query",
                StringComparison.OrdinalIgnoreCase))
            .Where(uri => !IsServiceMetadataUri(uri))
            .ToArray();

        Assert.Equal(2, layerQueryRequests.Length);

        var layer0Query = ParseQuery(layerQueryRequests[0]);
        Assert.Equal("json", layer0Query["f"]);
        Assert.Equal("true", layer0Query["returnExtentOnly"]);
        Assert.Equal("25832", layer0Query["outSR"]);
        Assert.Equal("0", layer0Query["time"]);
        Assert.Equal("standard", layer0Query["sqlFormat"]);
        Assert.Equal("STATUS = 'Active'", layer0Query["where"]);
        Assert.Equal("esriGeometryEnvelope", layer0Query["geometryType"]);
        Assert.Equal("4326", layer0Query["inSR"]);
        Assert.False(layer0Query.ContainsKey("returnCountOnly"));
        Assert.False(layer0Query.ContainsKey("returnIdsOnly"));
        Assert.False(layer0Query.ContainsKey("returnUniqueIdsOnly"));

        var layer1Query = ParseQuery(layerQueryRequests[1]);
        Assert.Equal("true", layer1Query["returnExtentOnly"]);
        Assert.Equal("INSPECTION_REQUIRED = 1", layer1Query["where"]);
    }

    [Fact]
    public async Task QueryExtentsAsync_Throws_WhenServiceDoesNotAdvertiseQuerySupport() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse(capabilities: "Uploads");
            }

            throw new InvalidOperationException("HTTP should not be called after metadata lookup.");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceCapabilityException>(() =>
            client.QueryExtentsAsync(
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
    public async Task QueryExtentsAsync_Throws_WhenGdbVersionIsProvided() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<NotSupportedException>(() =>
            client.QueryExtentsAsync(
                new FeatureServiceQueryRequest {
                    LayerDefinitions = [
                        new FeatureServiceLayerQueryDefinition {
                            LayerId = 0
                        }
                    ],
                    GdbVersion = "SDE.DEFAULT"
                },
                cancellationToken));

        Assert.Contains("GdbVersion", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryExtentsAsync_ForwardsTimeReferenceUnknownClientToLayerExtentQueries() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<Uri>();

        var client = CreateClient(request => {
            requestUris.Add(request.RequestUri!);

            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse(capabilities: "Query");
            }

            if (request.RequestUri!.AbsolutePath.EndsWith(
                "/FeatureServer/0/query",
                StringComparison.OrdinalIgnoreCase)) {
                return StubHttpMessageHandler.Json("""
                {
                  "extent": null
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var result = await client.QueryExtentsAsync(
            new FeatureServiceQueryRequest {
                LayerDefinitions = [
                    new FeatureServiceLayerQueryDefinition {
                        LayerId = 0,
                        Where = "STATUS = 'Active'"
                    }
                ],
                TimeReferenceUnknownClient = true
            },
            cancellationToken);

        var layer = Assert.Single(result.Layers);

        Assert.Equal(0, layer.LayerId);
        Assert.Null(layer.Extent);

        var queryRequest = Assert.Single(
            requestUris,
            uri => uri.AbsolutePath.EndsWith(
                "/FeatureServer/0/query",
                StringComparison.OrdinalIgnoreCase));

        var query = ParseQuery(queryRequest);

        Assert.Equal("true", query["returnExtentOnly"]);
        Assert.Equal("STATUS = 'Active'", query["where"]);
        Assert.Equal("true", query["timeReferenceUnknownClient"]);
    }

    private static bool IsServiceMetadataUri(Uri uri) {
        return uri.AbsolutePath.EndsWith(
            "/FeatureServer",
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QueryExtentsAsync_SendsDefaultWhereTimeExtentAndHistoricMoment() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<Uri>();
        var timeStart = new DateTimeOffset(2026, 01, 01, 0, 0, 0, TimeSpan.Zero);
        var timeEnd = new DateTimeOffset(2026, 01, 31, 0, 0, 0, TimeSpan.Zero);
        var historicMoment = new DateTimeOffset(2025, 12, 01, 0, 0, 0, TimeSpan.Zero);

        var client = CreateClient(request => {
            requestUris.Add(request.RequestUri!);

            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse(capabilities: "Query");
            }

            if (request.RequestUri!.AbsolutePath.EndsWith(
                "/FeatureServer/0/query",
                StringComparison.OrdinalIgnoreCase)) {
                return StubHttpMessageHandler.Json("""
                {
                  "extent": {
                    "xmin": 10.0,
                    "ymin": 55.0,
                    "xmax": 11.0,
                    "ymax": 56.0
                  }
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var result = await client.QueryExtentsAsync(
            new FeatureServiceQueryRequest {
                LayerDefinitions = [
                    new FeatureServiceLayerQueryDefinition {
                        LayerId = 0
                    }
                ],
                TimeExtent = new FeatureTimeExtent(timeStart, timeEnd),
                HistoricMoment = historicMoment
            },
            cancellationToken);

        var layer = Assert.Single(result.Layers);
        Assert.Equal(0, layer.LayerId);
        Assert.NotNull(layer.Extent);
        Assert.Null(layer.Extent.Srid);

        var queryRequest = Assert.Single(
            requestUris,
            uri => uri.AbsolutePath.EndsWith(
                "/FeatureServer/0/query",
                StringComparison.OrdinalIgnoreCase));

        var query = ParseQuery(queryRequest);

        Assert.Equal("json", query["f"]);
        Assert.Equal("true", query["returnExtentOnly"]);
        Assert.Equal("1=1", query["where"]);
        Assert.Equal(
            $"{timeStart.ToUnixTimeMilliseconds()},{timeEnd.ToUnixTimeMilliseconds()}",
            query["time"]);
        Assert.Equal(
            historicMoment.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture),
            query["historicMoment"]);
    }

    [Fact]
    public async Task QueryExtentsAsync_MapsIncompleteExtentToNull() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse(capabilities: "Query");
            }

            if (request.RequestUri!.AbsolutePath.EndsWith(
                "/FeatureServer/0/query",
                StringComparison.OrdinalIgnoreCase)) {
                return StubHttpMessageHandler.Json("""
                {
                  "extent": {
                    "xmin": 10.0,
                    "ymin": 55.0,
                    "xmax": 11.0
                  }
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var result = await client.QueryExtentsAsync(
            new FeatureServiceQueryRequest {
                LayerDefinitions = [
                    new FeatureServiceLayerQueryDefinition {
                        LayerId = 0
                    }
                ]
            },
            cancellationToken);

        var layer = Assert.Single(result.Layers);

        Assert.Equal(0, layer.LayerId);
        Assert.Null(layer.Extent);
    }

    [Fact]
    public async Task QueryExtentsAsync_DoesNotSendFeatureSetOrIdResultParameters() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<Uri>();

        var client = CreateClient(request => {
            requestUris.Add(request.RequestUri!);

            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse(capabilities: "Query");
            }

            if (request.RequestUri!.AbsolutePath.EndsWith(
                "/FeatureServer/0/query",
                StringComparison.OrdinalIgnoreCase)) {
                return StubHttpMessageHandler.Json("""
                {
                  "extent": null
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        await client.QueryExtentsAsync(
            new FeatureServiceQueryRequest {
                LayerDefinitions = [
                    new FeatureServiceLayerQueryDefinition {
                        LayerId = 0,
                        Where = "OBJECTID > 0",
                        OutFields = ["OBJECTID", "NAME"]
                    }
                ],
                ReturnGeometry = true,
                ReturnZ = true,
                ReturnM = true,
                GeometryPrecision = 2,
                MaxAllowableOffset = 5
            },
            cancellationToken);

        var queryRequest = Assert.Single(
            requestUris,
            uri => uri.AbsolutePath.EndsWith(
                "/FeatureServer/0/query",
                StringComparison.OrdinalIgnoreCase));

        var query = ParseQuery(queryRequest);

        Assert.Equal("true", query["returnExtentOnly"]);
        Assert.Equal("OBJECTID > 0", query["where"]);
        Assert.False(query.ContainsKey("returnGeometry"));
        Assert.False(query.ContainsKey("returnZ"));
        Assert.False(query.ContainsKey("returnM"));
        Assert.False(query.ContainsKey("geometryPrecision"));
        Assert.False(query.ContainsKey("maxAllowableOffset"));
        Assert.False(query.ContainsKey("outFields"));
        Assert.False(query.ContainsKey("returnCountOnly"));
        Assert.False(query.ContainsKey("returnIdsOnly"));
        Assert.False(query.ContainsKey("returnUniqueIdsOnly"));
    }

    [Fact]
    public async Task QueryAllAsync_UsesLayerQueryEndpointsAndMapsCompleteLayerResults() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<Uri>();

        var client = CreateClient(request => {
            requestUris.Add(request.RequestUri!);

            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse(capabilities: "Query");
            }

            if (request.RequestUri!.AbsolutePath.EndsWith(
                "/FeatureServer/0",
                StringComparison.OrdinalIgnoreCase)) {
                return CreateLayerMetadataResponse(
                    layerId: 0,
                    name: "Harbors",
                    geometryType: "esriGeometryPoint");
            }

            if (request.RequestUri!.AbsolutePath.EndsWith(
                "/FeatureServer/1",
                StringComparison.OrdinalIgnoreCase)) {
                return CreateLayerMetadataResponse(
                    layerId: 1,
                    name: "Inspections",
                    geometryType: null);
            }

            if (request.RequestUri!.AbsolutePath.EndsWith(
                "/FeatureServer/0/query",
                StringComparison.OrdinalIgnoreCase)) {
                return StubHttpMessageHandler.Json("""
                {
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
                        "NAME": "Harbor A"
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
                }
                """);
            }

            if (request.RequestUri!.AbsolutePath.EndsWith(
                "/FeatureServer/1/query",
                StringComparison.OrdinalIgnoreCase)) {
                return StubHttpMessageHandler.Json("""
                {
                  "objectIdFieldName": "OBJECTID",
                  "features": [
                    {
                      "attributes": {
                        "OBJECTID": 20,
                        "NAME": "Inspection A"
                      }
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var result = await client.QueryAllAsync(
            new FeatureServiceQueryRequest {
                LayerDefinitions = [
                    new FeatureServiceLayerQueryDefinition {
                        LayerId = 0,
                        Where = "STATUS = 'Active'",
                        OutFields = ["OBJECTID", "NAME"]
                    },
                    new FeatureServiceLayerQueryDefinition {
                        LayerId = 1,
                        Where = "OBJECTID < 100",
                        OutFields = ["OBJECTID", "NAME"]
                    }
                ],
                ReturnGeometry = true,
                OutSrid = 4326,
                ReturnZ = false,
                ReturnM = false,
                GeometryPrecision = 2,
                MaxAllowableOffset = 5,
                SqlFormat = FeatureQuerySqlFormat.Standard
            },
            cancellationToken);

        Assert.Equal(2, result.Layers.Count);

        var layer0 = result.Layers[0];
        Assert.Equal(0, layer0.LayerId);
        Assert.Null(layer0.ExceededTransferLimit);

        var feature = Assert.Single(layer0.Records);
        Assert.Equal(10, feature.ObjectId);
        Assert.Equal("Harbor A", feature.Attributes["NAME"]);

        var point = Assert.IsType<Point>(feature.Geometry);
        Assert.Equal(12.34, point.X);
        Assert.Equal(56.78, point.Y);

        var layer1 = result.Layers[1];
        Assert.Equal(1, layer1.LayerId);
        Assert.Null(layer1.ExceededTransferLimit);

        var tableRecord = Assert.Single(layer1.Records);
        Assert.Equal(20, tableRecord.ObjectId);
        Assert.Null(tableRecord.Geometry);
        Assert.Equal("Inspection A", tableRecord.Attributes["NAME"]);

        Assert.DoesNotContain(
            requestUris,
            uri => uri.AbsolutePath.EndsWith(
                "/FeatureServer/query",
                StringComparison.OrdinalIgnoreCase));

        var layerQueryRequests = requestUris
            .Where(uri => uri.AbsolutePath.EndsWith(
                "/query",
                StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.Equal(2, layerQueryRequests.Length);

        var layer0Query = ParseQuery(layerQueryRequests[0]);
        Assert.Equal("json", layer0Query["f"]);
        Assert.Equal("STATUS = 'Active'", layer0Query["where"]);
        Assert.Equal("OBJECTID,NAME", layer0Query["outFields"]);
        Assert.Equal("true", layer0Query["returnGeometry"]);
        Assert.Equal("4326", layer0Query["outSR"]);
        Assert.Equal("false", layer0Query["returnZ"]);
        Assert.Equal("false", layer0Query["returnM"]);
        Assert.Equal("2", layer0Query["geometryPrecision"]);
        Assert.Equal("5", layer0Query["maxAllowableOffset"]);
        Assert.Equal("standard", layer0Query["sqlFormat"]);
        Assert.False(layer0Query.ContainsKey("layerDefs"));

        var layer1Query = ParseQuery(layerQueryRequests[1]);
        Assert.Equal("OBJECTID < 100", layer1Query["where"]);
        Assert.Equal("OBJECTID,NAME", layer1Query["outFields"]);
    }

    [Fact]
    public async Task QueryAllAsync_Throws_WhenServiceDoesNotAdvertiseQuerySupport() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse(capabilities: "Uploads");
            }

            throw new InvalidOperationException("HTTP should not be called after metadata lookup.");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceCapabilityException>(() =>
            client.QueryAllAsync(
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
    public async Task QueryAllAsync_Throws_WhenGdbVersionIsProvided() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<NotSupportedException>(() =>
            client.QueryAllAsync(
                new FeatureServiceQueryRequest {
                    LayerDefinitions = [
                        new FeatureServiceLayerQueryDefinition {
                            LayerId = 0
                        }
                    ],
                    GdbVersion = "SDE.DEFAULT"
                },
                cancellationToken));

        Assert.Contains("GdbVersion", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryAllAsync_ForwardsTimeReferenceUnknownClientToLayerQueries() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<Uri>();

        var client = CreateClient(request => {
            requestUris.Add(request.RequestUri!);

            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse(capabilities: "Query");
            }

            if (request.RequestUri!.AbsolutePath.EndsWith(
                "/FeatureServer/0",
                StringComparison.OrdinalIgnoreCase)) {
                return CreateLayerMetadataResponse(
                    layerId: 0,
                    name: "Harbors",
                    geometryType: "esriGeometryPoint");
            }

            if (request.RequestUri!.AbsolutePath.EndsWith(
                "/FeatureServer/0/query",
                StringComparison.OrdinalIgnoreCase)) {
                return StubHttpMessageHandler.Json("""
                {
                  "objectIdFieldName": "OBJECTID",
                  "geometryType": "esriGeometryPoint",
                  "features": []
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var result = await client.QueryAllAsync(
            new FeatureServiceQueryRequest {
                LayerDefinitions = [
                    new FeatureServiceLayerQueryDefinition {
                        LayerId = 0,
                        Where = "STATUS = 'Active'"
                    }
                ],
                TimeReferenceUnknownClient = true
            },
            cancellationToken);

        var layer = Assert.Single(result.Layers);

        Assert.Equal(0, layer.LayerId);
        Assert.Empty(layer.Records);

        var queryRequest = Assert.Single(
            requestUris,
            uri => uri.AbsolutePath.EndsWith(
                "/FeatureServer/0/query",
                StringComparison.OrdinalIgnoreCase));

        var query = ParseQuery(queryRequest);

        Assert.Equal("STATUS = 'Active'", query["where"]);
        Assert.Equal("true", query["timeReferenceUnknownClient"]);
    }

    [Fact]
    public async Task QueryAllAsync_SendsDefaultWhereTimeExtentHistoricMomentAndSpatialFilter() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<Uri>();
        var timeStart = new DateTimeOffset(2026, 01, 01, 0, 0, 0, TimeSpan.Zero);
        var timeEnd = new DateTimeOffset(2026, 01, 31, 0, 0, 0, TimeSpan.Zero);
        var historicMoment = new DateTimeOffset(2025, 12, 01, 0, 0, 0, TimeSpan.Zero);

        var client = CreateClient(request => {
            requestUris.Add(request.RequestUri!);

            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse(capabilities: "Query");
            }

            if (request.RequestUri!.AbsolutePath.EndsWith(
                "/FeatureServer/0",
                StringComparison.OrdinalIgnoreCase)) {
                return CreateLayerMetadataResponse(
                    layerId: 0,
                    name: "Harbors",
                    geometryType: "esriGeometryPoint");
            }

            if (request.RequestUri!.AbsolutePath.EndsWith(
                "/FeatureServer/0/query",
                StringComparison.OrdinalIgnoreCase)) {
                return StubHttpMessageHandler.Json("""
                {
                  "objectIdFieldName": "OBJECTID",
                  "geometryType": "esriGeometryPoint",
                  "spatialReference": {
                    "wkid": 25832,
                    "latestWkid": 25832
                  },
                  "features": []
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var result = await client.QueryAllAsync(
            new FeatureServiceQueryRequest {
                LayerDefinitions = [
                    new FeatureServiceLayerQueryDefinition {
                        LayerId = 0
                    }
                ],
                OutSrid = 25832,
                SpatialFilter = FeatureSpatialFilter.FromEnvelope(
                    new Envelope(9, 12, 54, 57),
                    4326),
                TimeExtent = new FeatureTimeExtent(timeStart, timeEnd),
                HistoricMoment = historicMoment
            },
            cancellationToken);

        var layer = Assert.Single(result.Layers);
        Assert.Equal(0, layer.LayerId);
        Assert.Empty(layer.Records);
        Assert.Null(layer.ExceededTransferLimit);

        var queryRequest = Assert.Single(
            requestUris,
            uri => uri.AbsolutePath.EndsWith(
                "/FeatureServer/0/query",
                StringComparison.OrdinalIgnoreCase));

        var query = ParseQuery(queryRequest);

        Assert.Equal("json", query["f"]);
        Assert.Equal("1=1", query["where"]);
        Assert.Equal("25832", query["outSR"]);
        Assert.Equal(
            $"{timeStart.ToUnixTimeMilliseconds()},{timeEnd.ToUnixTimeMilliseconds()}",
            query["time"]);
        Assert.Equal(
            historicMoment.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture),
            query["historicMoment"]);
        Assert.Equal("esriGeometryEnvelope", query["geometryType"]);
        Assert.Equal("4326", query["inSR"]);
        Assert.True(query.ContainsKey("geometry"));
    }

    [Fact]
    public async Task QueryAllAsync_SendsTimeInstantWhenProvided() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<Uri>();
        var timeInstant = new DateTimeOffset(2026, 02, 15, 12, 30, 0, TimeSpan.Zero);

        var client = CreateClient(request => {
            requestUris.Add(request.RequestUri!);

            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse(capabilities: "Query");
            }

            if (request.RequestUri!.AbsolutePath.EndsWith(
                "/FeatureServer/0",
                StringComparison.OrdinalIgnoreCase)) {
                return CreateLayerMetadataResponse(
                    layerId: 0,
                    name: "Harbors",
                    geometryType: "esriGeometryPoint");
            }

            if (request.RequestUri!.AbsolutePath.EndsWith(
                "/FeatureServer/0/query",
                StringComparison.OrdinalIgnoreCase)) {
                return StubHttpMessageHandler.Json("""
                {
                  "objectIdFieldName": "OBJECTID",
                  "geometryType": "esriGeometryPoint",
                  "features": []
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        await client.QueryAllAsync(
            new FeatureServiceQueryRequest {
                LayerDefinitions = [
                    new FeatureServiceLayerQueryDefinition {
                        LayerId = 0,
                        Where = "OBJECTID > 0"
                    }
                ],
                TimeInstant = timeInstant
            },
            cancellationToken);

        var queryRequest = Assert.Single(
            requestUris,
            uri => uri.AbsolutePath.EndsWith(
                "/FeatureServer/0/query",
                StringComparison.OrdinalIgnoreCase));

        var query = ParseQuery(queryRequest);

        Assert.Equal("OBJECTID > 0", query["where"]);
        Assert.Equal(
            timeInstant.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture),
            query["time"]);
    }

    [Fact]
    public async Task QueryAllAsync_DoesNotSendServiceLevelQueryParametersToLayerQueries() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<Uri>();

        var client = CreateClient(request => {
            requestUris.Add(request.RequestUri!);

            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse(capabilities: "Query");
            }

            if (request.RequestUri!.AbsolutePath.EndsWith(
                "/FeatureServer/0",
                StringComparison.OrdinalIgnoreCase)) {
                return CreateLayerMetadataResponse(
                    layerId: 0,
                    name: "Harbors",
                    geometryType: "esriGeometryPoint");
            }

            if (request.RequestUri!.AbsolutePath.EndsWith(
                "/FeatureServer/0/query",
                StringComparison.OrdinalIgnoreCase)) {
                return StubHttpMessageHandler.Json("""
                {
                  "objectIdFieldName": "OBJECTID",
                  "geometryType": "esriGeometryPoint",
                  "features": []
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        await client.QueryAllAsync(
            new FeatureServiceQueryRequest {
                LayerDefinitions = [
                    new FeatureServiceLayerQueryDefinition {
                        LayerId = 0,
                        Where = "STATUS = 'Active'",
                        OutFields = ["OBJECTID", "NAME"]
                    }
                ],
                ReturnGeometry = false,
                ReturnZ = true,
                ReturnM = true,
                GeometryPrecision = 3,
                MaxAllowableOffset = 12.5
            },
            cancellationToken);

        var queryRequest = Assert.Single(
            requestUris,
            uri => uri.AbsolutePath.EndsWith(
                "/FeatureServer/0/query",
                StringComparison.OrdinalIgnoreCase));

        var query = ParseQuery(queryRequest);

        Assert.Equal("STATUS = 'Active'", query["where"]);
        Assert.Equal("OBJECTID,NAME", query["outFields"]);
        Assert.Equal("false", query["returnGeometry"]);
        Assert.Equal("true", query["returnZ"]);
        Assert.Equal("true", query["returnM"]);
        Assert.Equal("3", query["geometryPrecision"]);
        Assert.Equal("12.5", query["maxAllowableOffset"]);

        Assert.False(query.ContainsKey("layerDefs"));
        Assert.Equal("false", query["returnTrueCurves"]);
        Assert.False(query.ContainsKey("returnExtentOnly"));
        Assert.False(query.ContainsKey("returnCountOnly"));
        Assert.False(query.ContainsKey("returnIdsOnly"));
        Assert.False(query.ContainsKey("returnUniqueIdsOnly"));
    }

    [Fact]
    public async Task QueryAllAsync_PreservesLayerOrderWhenLayerResultsAreEmpty() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse(capabilities: "Query");
            }

            if (request.RequestUri!.AbsolutePath.EndsWith(
                "/FeatureServer/2",
                StringComparison.OrdinalIgnoreCase)) {
                return CreateLayerMetadataResponse(
                    layerId: 2,
                    name: "First",
                    geometryType: "esriGeometryPoint");
            }

            if (request.RequestUri!.AbsolutePath.EndsWith(
                "/FeatureServer/5",
                StringComparison.OrdinalIgnoreCase)) {
                return CreateLayerMetadataResponse(
                    layerId: 5,
                    name: "Second",
                    geometryType: "esriGeometryPoint");
            }

            if (request.RequestUri!.AbsolutePath.EndsWith(
                "/FeatureServer/2/query",
                StringComparison.OrdinalIgnoreCase) ||
                request.RequestUri!.AbsolutePath.EndsWith(
                    "/FeatureServer/5/query",
                    StringComparison.OrdinalIgnoreCase)) {
                return StubHttpMessageHandler.Json("""
                {
                  "objectIdFieldName": "OBJECTID",
                  "geometryType": "esriGeometryPoint",
                  "features": []
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var result = await client.QueryAllAsync(
            new FeatureServiceQueryRequest {
                LayerDefinitions = [
                    new FeatureServiceLayerQueryDefinition {
                        LayerId = 2
                    },
                    new FeatureServiceLayerQueryDefinition {
                        LayerId = 5
                    }
                ]
            },
            cancellationToken);

        Assert.Equal(2, result.Layers.Count);
        Assert.Equal(2, result.Layers[0].LayerId);
        Assert.Equal(5, result.Layers[1].LayerId);
        Assert.Empty(result.Layers[0].Records);
        Assert.Empty(result.Layers[1].Records);
    }

    [Fact]
    public async Task QueryCountAsync_IncludesSpatialDistanceAndUnits_WhenProvided() {
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
                  "layers": [
                    {
                      "id": 0,
                      "count": 3
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var result = await client.QueryCountAsync(
            new FeatureServiceQueryRequest {
                LayerDefinitions = [
                    new FeatureServiceLayerQueryDefinition {
                        LayerId = 0,
                        Where = "STATUS = 'Active'"
                    }
                ],
                SpatialFilter = FeatureSpatialFilter.FromGeometry(
                    new Point(12.34, 56.78) {
                        SRID = 4326
                    },
                    distance: 250,
                    distanceUnit: FeatureSpatialDistanceUnit.Meter)
            },
            cancellationToken);

        var layer = Assert.Single(result.Layers);

        Assert.Equal(0, layer.LayerId);
        Assert.Equal(3, layer.Count);

        var queryRequest = Assert.Single(
            requestUris,
            uri => uri.AbsolutePath.EndsWith(
                "/FeatureServer/query",
                StringComparison.OrdinalIgnoreCase));

        var query = ParseQuery(queryRequest);

        Assert.Equal("true", query["returnCountOnly"]);
        Assert.Equal("esriGeometryPoint", query["geometryType"]);
        Assert.Equal("esriSpatialRelIntersects", query["spatialRel"]);
        Assert.Equal("4326", query["inSR"]);
        Assert.Equal("250", query["distance"]);
        Assert.Equal("esriSRUnit_Meter", query["units"]);
        Assert.True(query.ContainsKey("geometry"));
    }

    [Fact]
    public async Task QueryAllAsync_ForwardsSpatialDistanceToLayerQueries() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<Uri>();

        var client = CreateClient(request => {
            requestUris.Add(request.RequestUri!);

            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse(capabilities: "Query");
            }

            if (request.RequestUri!.AbsolutePath.EndsWith(
                "/FeatureServer/0",
                StringComparison.OrdinalIgnoreCase)) {
                return CreateLayerMetadataResponse(
                    layerId: 0,
                    name: "Harbors",
                    geometryType: "esriGeometryPoint");
            }

            if (request.RequestUri!.AbsolutePath.EndsWith(
                "/FeatureServer/0/query",
                StringComparison.OrdinalIgnoreCase)) {
                return StubHttpMessageHandler.Json("""
                {
                  "objectIdFieldName": "OBJECTID",
                  "geometryType": "esriGeometryPoint",
                  "features": []
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var result = await client.QueryAllAsync(
            new FeatureServiceQueryRequest {
                LayerDefinitions = [
                    new FeatureServiceLayerQueryDefinition {
                        LayerId = 0,
                        Where = "STATUS = 'Active'"
                    }
                ],
                SpatialFilter = FeatureSpatialFilter.FromEnvelope(
                    new Envelope(10, 11, 55, 56),
                    inSrid: 4326,
                    distance: 2.5,
                    distanceUnit: FeatureSpatialDistanceUnit.Kilometer)
            },
            cancellationToken);

        var layer = Assert.Single(result.Layers);

        Assert.Equal(0, layer.LayerId);
        Assert.Empty(layer.Records);

        var queryRequest = Assert.Single(
            requestUris,
            uri => uri.AbsolutePath.EndsWith(
                "/FeatureServer/0/query",
                StringComparison.OrdinalIgnoreCase));

        var query = ParseQuery(queryRequest);

        Assert.Equal("STATUS = 'Active'", query["where"]);
        Assert.Equal("esriGeometryEnvelope", query["geometryType"]);
        Assert.Equal("4326", query["inSR"]);
        Assert.Equal("2.5", query["distance"]);
        Assert.Equal("esriSRUnit_Kilometer", query["units"]);
        Assert.True(query.ContainsKey("geometry"));
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

    private static HttpResponseMessage CreateLayerMetadataResponse(
    int layerId,
    string name,
    string? geometryType) {
        var geometryTypeProperty = geometryType is null
            ? string.Empty
            : $"""
              "geometryType": "{geometryType}",
            """;

        return StubHttpMessageHandler.Json($$"""
        {
          "id": {{layerId}},
          "name": "{{name}}",
          {{geometryTypeProperty}}
          "objectIdField": "OBJECTID",
          "maxRecordCount": 1000,
          "advancedQueryCapabilities": {
            "supportsPagination": true
          },
          "fields": [
            { "name": "OBJECTID", "type": "esriFieldTypeOID", "nullable": false },
            { "name": "NAME", "type": "esriFieldTypeString", "nullable": true }
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
    [Fact]
    public async Task QueryAsync_SerializesTimeInstant() {
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
                TimeInstant = DateTimeOffset.UnixEpoch
            },
            cancellationToken);

        var queryRequest = Assert.Single(
            requestUris,
            uri => uri.AbsolutePath.EndsWith(
                "/FeatureServer/query",
                StringComparison.OrdinalIgnoreCase));

        var query = ParseQuery(queryRequest);

        Assert.Equal("0", query["time"]);
    }

    [Fact]
    public async Task QueryAsync_SerializesOpenEndedTimeExtent_WithStartOnly() {
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
                TimeExtent = new FeatureTimeExtent(
                    DateTimeOffset.UnixEpoch,
                    null)
            },
            cancellationToken);

        var queryRequest = Assert.Single(
            requestUris,
            uri => uri.AbsolutePath.EndsWith(
                "/FeatureServer/query",
                StringComparison.OrdinalIgnoreCase));

        var query = ParseQuery(queryRequest);

        Assert.Equal("0,null", query["time"]);
    }

    [Fact]
    public async Task QueryAsync_SerializesOpenEndedTimeExtent_WithEndOnly() {
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
                TimeExtent = new FeatureTimeExtent(
                    null,
                    DateTimeOffset.UnixEpoch)
            },
            cancellationToken);

        var queryRequest = Assert.Single(
            requestUris,
            uri => uri.AbsolutePath.EndsWith(
                "/FeatureServer/query",
                StringComparison.OrdinalIgnoreCase));

        var query = ParseQuery(queryRequest);

        Assert.Equal("null,0", query["time"]);
    }

    [Fact]
    public async Task QueryAsync_DoesNotSendSqlFormat_WhenSqlFormatIsNone() {
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
                SqlFormat = FeatureQuerySqlFormat.None
            },
            cancellationToken);

        var queryRequest = Assert.Single(
            requestUris,
            uri => uri.AbsolutePath.EndsWith(
                "/FeatureServer/query",
                StringComparison.OrdinalIgnoreCase));

        var query = ParseQuery(queryRequest);

        Assert.False(query.ContainsKey("sqlFormat"));
    }

    [Fact]
    public async Task QueryAsync_SendsNativeSqlFormat_WhenRequested() {
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
                SqlFormat = FeatureQuerySqlFormat.Native
            },
            cancellationToken);

        var queryRequest = Assert.Single(
            requestUris,
            uri => uri.AbsolutePath.EndsWith(
                "/FeatureServer/query",
                StringComparison.OrdinalIgnoreCase));

        var query = ParseQuery(queryRequest);

        Assert.Equal("native", query["sqlFormat"]);
    }

    [Fact]
    public async Task QueryAsync_SendsReturnGeometryFalse() {
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
              "layers": [
                {
                  "id": 0,
                  "objectIdFieldName": "OBJECTID",
                  "features": [
                    {
                      "attributes": {
                        "OBJECTID": 10,
                        "NAME": "Harbor A"
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
                    LayerId = 0
                }
                ],
                ReturnGeometry = false
            },
            cancellationToken);

        var record = Assert.Single(Assert.Single(result.Layers).Records);

        Assert.Null(record.Geometry);
        Assert.Equal(10, record.ObjectId);

        var queryRequest = Assert.Single(
            requestUris,
            uri => uri.AbsolutePath.EndsWith(
                "/FeatureServer/query",
                StringComparison.OrdinalIgnoreCase));

        var query = ParseQuery(queryRequest);

        Assert.Equal("false", query["returnGeometry"]);
    }

    [Fact]
    public async Task QueryAsync_ReturnsEmptyLayers_WhenResponseDoesNotContainLayers() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse(capabilities: "Query");
            }

            if (request.RequestUri!.AbsolutePath.EndsWith(
                "/FeatureServer/query",
                StringComparison.OrdinalIgnoreCase)) {
                return StubHttpMessageHandler.Json("""
            {
            }
            """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var result = await client.QueryAsync(
            new FeatureServiceQueryRequest {
                LayerDefinitions = [
                    new FeatureServiceLayerQueryDefinition {
                    LayerId = 0
                }
                ]
            },
            cancellationToken);

        Assert.Empty(result.Layers);
    }

    [Fact]
    public async Task QueryAsync_ReturnsEmptyRecords_WhenReturnedLayerDoesNotContainFeatures() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse(capabilities: "Query");
            }

            if (request.RequestUri!.AbsolutePath.EndsWith(
                "/FeatureServer/query",
                StringComparison.OrdinalIgnoreCase)) {
                return StubHttpMessageHandler.Json("""
            {
              "layers": [
                {
                  "id": 0,
                  "objectIdFieldName": "OBJECTID"
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
                    LayerId = 0
                }
                ]
            },
            cancellationToken);

        var layer = Assert.Single(result.Layers);

        Assert.Equal(0, layer.LayerId);
        Assert.Empty(layer.Records);
    }
}