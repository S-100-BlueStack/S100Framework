using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Exceptions;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureLayerClientDirectEditHardeningTests
{
    [Fact]
    public async Task AddFeaturesAsync_IgnoresNullAddResultItems() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsAddFeaturesRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "addResults": [
                    null,
                    {
                      "success": true,
                      "objectId": 101
                    }
                  ],
                  "editMoment": 1457994488000
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var result = await client.GetLayerClient(0).AddFeaturesAsync(
            new AddFeaturesRequest {
                Features = [
                    new EditableFeature(
                        Geometry: null,
                        new Dictionary<string, object?> {
                            ["NAME"] = "Added feature"
                        })
                ],
                ReturnEditMoment = true
            },
            cancellationToken);

        Assert.True(result.Success);
        Assert.Equal(1457994488000, result.EditMoment);

        var addResult = Assert.Single(result.AddResults);
        Assert.True(addResult.Success);
        Assert.Equal(101, addResult.ObjectId);
    }

    [Fact]
    public async Task AddFeaturesAsync_Throws_WhenOnlyNullAddResultsAreReturned() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsAddFeaturesRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "addResults": [
                    null
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceException>(() =>
            client.GetLayerClient(0).AddFeaturesAsync(
                new AddFeaturesRequest {
                    Features = [
                        new EditableFeature(
                            Geometry: null,
                            new Dictionary<string, object?> {
                                ["NAME"] = "Added feature"
                            })
                    ]
                },
                cancellationToken));

        Assert.Contains("add results", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateFeaturesAsync_IgnoresNullUpdateResultItems() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsUpdateFeaturesRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "updateResults": [
                    null,
                    {
                      "success": false,
                      "objectId": 202,
                      "error": {
                        "code": 1001,
                        "description": "Failed to update feature."
                      }
                    }
                  ],
                  "editMoment": 1457994489000
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var result = await client.GetLayerClient(0).UpdateFeaturesAsync(
            new UpdateFeaturesRequest {
                Features = [
                    new EditableFeature(
                        Geometry: null,
                        new Dictionary<string, object?> {
                            ["OBJECTID"] = 202,
                            ["NAME"] = "Updated feature"
                        })
                ],
                ReturnEditMoment = true
            },
            cancellationToken);

        Assert.False(result.Success);
        Assert.Equal(1457994489000, result.EditMoment);

        var updateResult = Assert.Single(result.UpdateResults);
        Assert.False(updateResult.Success);
        Assert.Equal(202, updateResult.ObjectId);
        Assert.Equal(1001, updateResult.ErrorCode);
        Assert.Equal("Failed to update feature.", updateResult.ErrorDescription);
    }

    [Fact]
    public async Task UpdateFeaturesAsync_Throws_WhenOnlyNullUpdateResultsAreReturned() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsUpdateFeaturesRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "updateResults": [
                    null
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceException>(() =>
            client.GetLayerClient(0).UpdateFeaturesAsync(
                new UpdateFeaturesRequest {
                    Features = [
                        new EditableFeature(
                            Geometry: null,
                            new Dictionary<string, object?> {
                                ["OBJECTID"] = 202,
                                ["NAME"] = "Updated feature"
                            })
                    ]
                },
                cancellationToken));

        Assert.Contains("update results", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeleteFeaturesAsync_IgnoresNullDeleteResultItems() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsDeleteFeaturesRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "deleteResults": [
                    null,
                    {
                      "success": true,
                      "objectId": 10
                    }
                  ],
                  "editMoment": 1457994490000
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var result = await client.GetLayerClient(0).DeleteFeaturesAsync(
            new DeleteFeaturesRequest {
                ObjectIds = [10],
                ReturnDeleteResults = true,
                ReturnEditMoment = true
            },
            cancellationToken);

        Assert.True(result.Success);
        Assert.Equal(1457994490000, result.EditMoment);

        var deleteResult = Assert.Single(result.DeleteResults);
        Assert.True(deleteResult.Success);
        Assert.Equal(10, deleteResult.ObjectId);
    }

    [Fact]
    public async Task DeleteFeaturesAsync_Throws_WhenOnlyNullDeleteResultsAreReturnedWithoutSuccessValue() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsDeleteFeaturesRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "deleteResults": [
                    null
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceException>(() =>
            client.GetLayerClient(0).DeleteFeaturesAsync(
                new DeleteFeaturesRequest {
                    ObjectIds = [10],
                    ReturnDeleteResults = true
                },
                cancellationToken));

        Assert.Contains("delete results", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeleteFeaturesAsync_AllowsSuccessValueWithOnlyNullDeleteResults() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsDeleteFeaturesRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "success": true,
                  "deleteResults": [
                    null
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var result = await client.GetLayerClient(0).DeleteFeaturesAsync(
            new DeleteFeaturesRequest {
                ObjectIds = [10],
                ReturnDeleteResults = true
            },
            cancellationToken);

        Assert.True(result.Success);
        Assert.Empty(result.DeleteResults);
    }

    private static FeatureServiceClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> handler) {
        return new FeatureServiceClient(
            new HttpClient(new StubHttpMessageHandler(handler)),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });
    }

    private static bool IsAddFeaturesRequest(HttpRequestMessage request) {
        return request.RequestUri?.AbsolutePath.EndsWith(
            "/FeatureServer/0/addFeatures",
            StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool IsUpdateFeaturesRequest(HttpRequestMessage request) {
        return request.RequestUri?.AbsolutePath.EndsWith(
            "/FeatureServer/0/updateFeatures",
            StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool IsDeleteFeaturesRequest(HttpRequestMessage request) {
        return request.RequestUri?.AbsolutePath.EndsWith(
            "/FeatureServer/0/deleteFeatures",
            StringComparison.OrdinalIgnoreCase) == true;
    }
}