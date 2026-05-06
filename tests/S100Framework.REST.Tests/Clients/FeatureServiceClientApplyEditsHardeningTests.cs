using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureServiceClientApplyEditsHardeningTests
{
    [Fact]
    public async Task LayerApplyEditsAsync_IgnoresNullEditResultItems() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var layerClient = CreateClient(request => {
            if (IsLayerApplyEditsRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "addResults": [
                    null,
                    {
                      "success": true,
                      "objectId": 101
                    }
                  ],
                  "updateResults": [
                    null,
                    {
                      "success": false,
                      "objectId": 202,
                      "error": {
                        "code": 1001,
                        "description": "Update failed."
                      }
                    }
                  ],
                  "deleteResults": [
                    null,
                    {
                      "success": true,
                      "objectId": 303
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        }).GetLayerClient(0);

        var result = await layerClient.ApplyEditsAsync(
            new FeatureEdits {
                Adds = [
                    new EditableFeature(
                        Geometry: null,
                        new Dictionary<string, object?> {
                            ["NAME"] = "Added"
                        })
                ],
                Updates = [
                    new EditableFeature(
                        Geometry: null,
                        new Dictionary<string, object?> {
                            ["OBJECTID"] = 202,
                            ["NAME"] = "Updated"
                        })
                ],
                Deletes = [303]
            },
            cancellationToken);

        var addResult = Assert.Single(result.AddResults);
        Assert.True(addResult.Success);
        Assert.Equal(101, addResult.ObjectId);

        var updateResult = Assert.Single(result.UpdateResults);
        Assert.False(updateResult.Success);
        Assert.Equal(202, updateResult.ObjectId);
        Assert.Equal(1001, updateResult.ErrorCode);
        Assert.Equal("Update failed.", updateResult.ErrorDescription);

        var deleteResult = Assert.Single(result.DeleteResults);
        Assert.True(deleteResult.Success);
        Assert.Equal(303, deleteResult.ObjectId);
    }

    [Fact]
    public async Task LayerApplyEditsAsync_ReturnsEmptyResultCollections_WhenResultPropertiesAreMissing() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var layerClient = CreateClient(request => {
            if (IsLayerApplyEditsRequest(request)) {
                return StubHttpMessageHandler.Json("{}");
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        }).GetLayerClient(0);

        var result = await layerClient.ApplyEditsAsync(
            new FeatureEdits {
                Deletes = [303]
            },
            cancellationToken);

        Assert.Empty(result.AddResults);
        Assert.Empty(result.UpdateResults);
        Assert.Empty(result.DeleteResults);
    }

    [Fact]
    public async Task ServiceApplyEditsAsync_IgnoresNullEditResultItems() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceApplyEditsRequest(request)) {
                return StubHttpMessageHandler.Json("""
                [
                  {
                    "id": 0,
                    "addResults": [
                      null,
                      {
                        "success": true,
                        "objectId": 101
                      }
                    ],
                    "updateResults": [
                      null,
                      {
                        "success": false,
                        "objectId": 202,
                        "error": {
                          "code": 1001,
                          "description": "Update failed."
                        }
                      }
                    ],
                    "deleteResults": [
                      null,
                      {
                        "success": true,
                        "objectId": 303
                      }
                    ]
                  }
                ]
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var result = await client.ApplyEditsAsync(
            new FeatureServiceEdits {
                Layers = [
                    new ServiceLayerEdits {
                        LayerId = 0,
                        Adds = [
                            new EditableFeature(
                                Geometry: null,
                                new Dictionary<string, object?> {
                                    ["NAME"] = "Added"
                                })
                        ],
                        Updates = [
                            new EditableFeature(
                                Geometry: null,
                                new Dictionary<string, object?> {
                                    ["OBJECTID"] = 202,
                                    ["NAME"] = "Updated"
                                })
                        ],
                        DeleteObjectIds = [303]
                    }
                ]
            },
            cancellationToken);

        var layerResult = Assert.Single(result.LayerResults);
        Assert.Equal(0, layerResult.LayerId);

        var addResult = Assert.Single(layerResult.AddResults);
        Assert.True(addResult.Success);
        Assert.Equal(101, addResult.ObjectId);

        var updateResult = Assert.Single(layerResult.UpdateResults);
        Assert.False(updateResult.Success);
        Assert.Equal(202, updateResult.ObjectId);
        Assert.Equal(1001, updateResult.ErrorCode);
        Assert.Equal("Update failed.", updateResult.ErrorDescription);

        var deleteResult = Assert.Single(layerResult.DeleteResults);
        Assert.True(deleteResult.Success);
        Assert.Equal(303, deleteResult.ObjectId);
    }

    [Fact]
    public async Task ServiceApplyEditsAsync_ReturnsEmptyResultCollections_WhenResultPropertiesAreMissing() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceApplyEditsRequest(request)) {
                return StubHttpMessageHandler.Json("""
                [
                  {
                    "id": 0
                  }
                ]
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var result = await client.ApplyEditsAsync(
            new FeatureServiceEdits {
                Layers = [
                    new ServiceLayerEdits {
                        LayerId = 0,
                        DeleteObjectIds = [303]
                    }
                ]
            },
            cancellationToken);

        var layerResult = Assert.Single(result.LayerResults);

        Assert.Equal(0, layerResult.LayerId);
        Assert.Empty(layerResult.AddResults);
        Assert.Empty(layerResult.UpdateResults);
        Assert.Empty(layerResult.DeleteResults);
    }

    [Fact]
    public async Task ServiceApplyEditsAsync_IgnoresNullLayerResultItems() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceApplyEditsRequest(request)) {
                return StubHttpMessageHandler.Json("""
            [
              null,
              {
                "id": 0,
                "addResults": [
                  {
                    "success": true,
                    "objectId": 101
                  }
                ],
                "updateResults": [],
                "deleteResults": []
              },
              null,
              {
                "id": 1,
                "addResults": [],
                "updateResults": [],
                "deleteResults": [
                  {
                    "success": true,
                    "objectId": 202
                  }
                ]
              }
            ]
            """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var result = await client.ApplyEditsAsync(
            new FeatureServiceEdits {
                Layers = [
                    new ServiceLayerEdits {
                    LayerId = 0,
                    Adds = [
                        new EditableFeature(
                            Geometry: null,
                            new Dictionary<string, object?> {
                                ["NAME"] = "Added"
                            })
                    ]
                },
                new ServiceLayerEdits {
                    LayerId = 1,
                    DeleteObjectIds = [202]
                }
                ]
            },
            cancellationToken);

        Assert.Collection(
            result.LayerResults,
            firstLayer => {
                Assert.Equal(0, firstLayer.LayerId);

                var addResult = Assert.Single(firstLayer.AddResults);
                Assert.True(addResult.Success);
                Assert.Equal(101, addResult.ObjectId);
            },
            secondLayer => {
                Assert.Equal(1, secondLayer.LayerId);

                var deleteResult = Assert.Single(secondLayer.DeleteResults);
                Assert.True(deleteResult.Success);
                Assert.Equal(202, deleteResult.ObjectId);
            });
    }

    private static FeatureServiceClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> handler) {
        return new FeatureServiceClient(
            new HttpClient(new StubHttpMessageHandler(handler)),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });
    }

    private static bool IsLayerApplyEditsRequest(HttpRequestMessage request) {
        return request.RequestUri?.AbsolutePath.EndsWith(
            "/FeatureServer/0/applyEdits",
            StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool IsServiceApplyEditsRequest(HttpRequestMessage request) {
        return request.RequestUri?.AbsolutePath.EndsWith(
            "/FeatureServer/applyEdits",
            StringComparison.OrdinalIgnoreCase) == true;
    }
}