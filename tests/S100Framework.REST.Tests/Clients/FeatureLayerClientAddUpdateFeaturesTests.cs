using NetTopologySuite.Geometries;
using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Exceptions;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureLayerClientAddUpdateFeaturesTests
{
    [Fact]
    public async Task AddFeaturesAsync_PostsFeaturesAndMapsAddResults() {
        var cancellationToken = TestContext.Current.CancellationToken;
        string? requestBody = null;

        var client = CreateClient(request => {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.EndsWith(
                "/FeatureServer/0/addFeatures",
                request.RequestUri!.AbsolutePath,
                StringComparison.OrdinalIgnoreCase);

            requestBody = ReadRequestBody(request);

            return StubHttpMessageHandler.Json("""
            {
              "addResults": [
                { "success": true, "objectId": 101 },
                {
                  "success": false,
                  "error": {
                    "code": 1000,
                    "description": "Failed to add feature."
                  }
                }
              ],
              "editMoment": 1457994488000
            }
            """);
        });

        var geometryFactory = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

        var result = await client.GetLayerClient(0).AddFeaturesAsync(
            new AddFeaturesRequest {
                Features = [
                    new EditableFeature(
                        geometryFactory.CreatePoint(new Coordinate(12.34, 56.78)),
                        new Dictionary<string, object?> {
                            ["NAME"] = "Added feature"
                        }),
                    new EditableFeature(
                        geometryFactory.CreatePoint(new Coordinate(12.35, 56.79)),
                        new Dictionary<string, object?> {
                            ["NAME"] = "Rejected feature"
                        })
                ],
                GdbVersion = "SDE.DEFAULT",
                RollbackOnFailure = false,
                UseGlobalIds = true,
                ReturnEditMoment = true
            },
            cancellationToken);

        Assert.False(result.Success);
        Assert.Equal(1457994488000, result.EditMoment);
        Assert.Equal(2, result.AddResults.Count);

        Assert.True(result.AddResults[0].Success);
        Assert.Equal(101, result.AddResults[0].ObjectId);

        Assert.False(result.AddResults[1].Success);
        Assert.Equal(1000, result.AddResults[1].ErrorCode);
        Assert.Equal("Failed to add feature.", result.AddResults[1].ErrorDescription);

        var form = ParseFormBody(requestBody!);

        Assert.Equal("json", form["f"]);
        Assert.Equal("SDE.DEFAULT", form["gdbVersion"]);
        Assert.Equal("false", form["rollbackOnFailure"]);
        Assert.Equal("true", form["useGlobalIds"]);
        Assert.Equal("true", form["returnEditMoment"]);
        Assert.Contains("\"attributes\"", form["features"], StringComparison.Ordinal);
        Assert.Contains("\"geometry\"", form["features"], StringComparison.Ordinal);
        Assert.Contains("Added feature", form["features"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task AddFeaturesAsync_DoesNotSendReturnEditMoment_WhenFalse() {
        var cancellationToken = TestContext.Current.CancellationToken;
        string? requestBody = null;

        var client = CreateClient(request => {
            Assert.EndsWith(
                "/FeatureServer/0/addFeatures",
                request.RequestUri!.AbsolutePath,
                StringComparison.OrdinalIgnoreCase);

            requestBody = ReadRequestBody(request);

            return StubHttpMessageHandler.Json("""
            {
              "addResults": [
                { "success": true, "objectId": 101 }
              ]
            }
            """);
        });

        await client.GetLayerClient(0).AddFeaturesAsync(
            new AddFeaturesRequest {
                Features = [
                    new EditableFeature(
                        Geometry: null,
                        new Dictionary<string, object?> {
                            ["NAME"] = "No edit moment"
                        })
                ],
                ReturnEditMoment = false
            },
            cancellationToken);

        var form = ParseFormBody(requestBody!);

        Assert.False(form.ContainsKey("returnEditMoment"));
    }

    [Fact]
    public async Task UpdateFeaturesAsync_PostsFeaturesAndMapsUpdateResults() {
        var cancellationToken = TestContext.Current.CancellationToken;
        string? requestBody = null;

        var client = CreateClient(request => {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.EndsWith(
                "/FeatureServer/0/updateFeatures",
                request.RequestUri!.AbsolutePath,
                StringComparison.OrdinalIgnoreCase);

            requestBody = ReadRequestBody(request);

            return StubHttpMessageHandler.Json("""
            {
              "updateResults": [
                { "success": true, "objectId": 202 },
                {
                  "success": false,
                  "objectId": 203,
                  "error": {
                    "code": 1001,
                    "description": "Failed to update feature."
                  }
                }
              ],
              "editMoment": 1457994489000
            }
            """);
        });

        var geometryFactory = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

        var result = await client.GetLayerClient(0).UpdateFeaturesAsync(
            new UpdateFeaturesRequest {
                Features = [
                    new EditableFeature(
                        geometryFactory.CreatePoint(new Coordinate(12.36, 56.80)),
                        new Dictionary<string, object?> {
                            ["OBJECTID"] = 202,
                            ["NAME"] = "Updated feature"
                        }),
                    new EditableFeature(
                        geometryFactory.CreatePoint(new Coordinate(12.37, 56.81)),
                        new Dictionary<string, object?> {
                            ["OBJECTID"] = 203,
                            ["NAME"] = "Rejected update"
                        })
                ],
                GdbVersion = "SDE.DEFAULT",
                RollbackOnFailure = true,
                UseGlobalIds = false,
                ReturnEditMoment = true
            },
            cancellationToken);

        Assert.False(result.Success);
        Assert.Equal(1457994489000, result.EditMoment);
        Assert.Equal(2, result.UpdateResults.Count);

        Assert.True(result.UpdateResults[0].Success);
        Assert.Equal(202, result.UpdateResults[0].ObjectId);

        Assert.False(result.UpdateResults[1].Success);
        Assert.Equal(203, result.UpdateResults[1].ObjectId);
        Assert.Equal(1001, result.UpdateResults[1].ErrorCode);
        Assert.Equal("Failed to update feature.", result.UpdateResults[1].ErrorDescription);

        var form = ParseFormBody(requestBody!);

        Assert.Equal("json", form["f"]);
        Assert.Equal("SDE.DEFAULT", form["gdbVersion"]);
        Assert.Equal("true", form["rollbackOnFailure"]);
        Assert.Equal("false", form["useGlobalIds"]);
        Assert.Equal("true", form["returnEditMoment"]);
        Assert.Contains("\"attributes\"", form["features"], StringComparison.Ordinal);
        Assert.Contains("\"geometry\"", form["features"], StringComparison.Ordinal);
        Assert.Contains("Updated feature", form["features"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateFeaturesAsync_DoesNotSendReturnEditMoment_WhenFalse() {
        var cancellationToken = TestContext.Current.CancellationToken;
        string? requestBody = null;

        var client = CreateClient(request => {
            Assert.EndsWith(
                "/FeatureServer/0/updateFeatures",
                request.RequestUri!.AbsolutePath,
                StringComparison.OrdinalIgnoreCase);

            requestBody = ReadRequestBody(request);

            return StubHttpMessageHandler.Json("""
            {
              "updateResults": [
                { "success": true, "objectId": 202 }
              ]
            }
            """);
        });

        await client.GetLayerClient(0).UpdateFeaturesAsync(
            new UpdateFeaturesRequest {
                Features = [
                    new EditableFeature(
                        Geometry: null,
                        new Dictionary<string, object?> {
                            ["OBJECTID"] = 202,
                            ["NAME"] = "No edit moment"
                        })
                ],
                ReturnEditMoment = false
            },
            cancellationToken);

        var form = ParseFormBody(requestBody!);

        Assert.False(form.ContainsKey("returnEditMoment"));
    }

    [Fact]
    public async Task AddFeaturesAsync_Throws_WhenFeaturesAreNull() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetLayerClient(0).AddFeaturesAsync(
                new AddFeaturesRequest {
                    Features = null!
                },
                cancellationToken));

        Assert.Contains("Features", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AddFeaturesAsync_Throws_WhenFeaturesAreEmpty() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetLayerClient(0).AddFeaturesAsync(
                new AddFeaturesRequest(),
                cancellationToken));

        Assert.Contains("Features", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AddFeaturesAsync_Throws_WhenFeaturesContainNullItem() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetLayerClient(0).AddFeaturesAsync(
                new AddFeaturesRequest {
                    Features = [null!]
                },
                cancellationToken));

        Assert.Contains("Features", exception.Message, StringComparison.Ordinal);
        Assert.Contains("null", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddFeaturesAsync_Throws_WhenGdbVersionIsWhitespace() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetLayerClient(0).AddFeaturesAsync(
                new AddFeaturesRequest {
                    Features = [
                        new EditableFeature(
                            Geometry: null,
                            new Dictionary<string, object?> {
                                ["NAME"] = "Invalid version"
                            })
                    ],
                    GdbVersion = " "
                },
                cancellationToken));

        Assert.Contains("GdbVersion", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateFeaturesAsync_Throws_WhenFeaturesAreNull() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetLayerClient(0).UpdateFeaturesAsync(
                new UpdateFeaturesRequest {
                    Features = null!
                },
                cancellationToken));

        Assert.Contains("Features", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateFeaturesAsync_Throws_WhenFeaturesAreEmpty() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetLayerClient(0).UpdateFeaturesAsync(
                new UpdateFeaturesRequest(),
                cancellationToken));

        Assert.Contains("Features", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateFeaturesAsync_Throws_WhenFeaturesContainNullItem() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetLayerClient(0).UpdateFeaturesAsync(
                new UpdateFeaturesRequest {
                    Features = [null!]
                },
                cancellationToken));

        Assert.Contains("Features", exception.Message, StringComparison.Ordinal);
        Assert.Contains("null", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateFeaturesAsync_Throws_WhenGdbVersionIsWhitespace() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetLayerClient(0).UpdateFeaturesAsync(
                new UpdateFeaturesRequest {
                    Features = [
                        new EditableFeature(
                            Geometry: null,
                            new Dictionary<string, object?> {
                                ["OBJECTID"] = 202,
                                ["NAME"] = "Invalid version"
                            })
                    ],
                    GdbVersion = " "
                },
                cancellationToken));

        Assert.Contains("GdbVersion", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AddFeaturesAsync_Throws_WhenResponseDoesNotContainAddResults() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            Assert.EndsWith(
                "/FeatureServer/0/addFeatures",
                request.RequestUri!.AbsolutePath,
                StringComparison.OrdinalIgnoreCase);

            return StubHttpMessageHandler.Json("""
            {
            }
            """);
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceException>(() =>
            client.GetLayerClient(0).AddFeaturesAsync(
                new AddFeaturesRequest {
                    Features = [
                        new EditableFeature(
                            Geometry: null,
                            new Dictionary<string, object?> {
                                ["NAME"] = "Missing add result"
                            })
                    ]
                },
                cancellationToken));

        Assert.Contains("addFeatures", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AddFeaturesAsync_Throws_WhenResponseContainsEmptyAddResults() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            Assert.EndsWith(
                "/FeatureServer/0/addFeatures",
                request.RequestUri!.AbsolutePath,
                StringComparison.OrdinalIgnoreCase);

            return StubHttpMessageHandler.Json("""
            {
              "addResults": []
            }
            """);
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceException>(() =>
            client.GetLayerClient(0).AddFeaturesAsync(
                new AddFeaturesRequest {
                    Features = [
                        new EditableFeature(
                            Geometry: null,
                            new Dictionary<string, object?> {
                                ["NAME"] = "Empty add result"
                            })
                    ]
                },
                cancellationToken));

        Assert.Contains("addFeatures", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateFeaturesAsync_Throws_WhenResponseDoesNotContainUpdateResults() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            Assert.EndsWith(
                "/FeatureServer/0/updateFeatures",
                request.RequestUri!.AbsolutePath,
                StringComparison.OrdinalIgnoreCase);

            return StubHttpMessageHandler.Json("""
            {
            }
            """);
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceException>(() =>
            client.GetLayerClient(0).UpdateFeaturesAsync(
                new UpdateFeaturesRequest {
                    Features = [
                        new EditableFeature(
                            Geometry: null,
                            new Dictionary<string, object?> {
                                ["OBJECTID"] = 202,
                                ["NAME"] = "Missing update result"
                            })
                    ]
                },
                cancellationToken));

        Assert.Contains("updateFeatures", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateFeaturesAsync_Throws_WhenResponseContainsEmptyUpdateResults() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            Assert.EndsWith(
                "/FeatureServer/0/updateFeatures",
                request.RequestUri!.AbsolutePath,
                StringComparison.OrdinalIgnoreCase);

            return StubHttpMessageHandler.Json("""
            {
              "updateResults": []
            }
            """);
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceException>(() =>
            client.GetLayerClient(0).UpdateFeaturesAsync(
                new UpdateFeaturesRequest {
                    Features = [
                        new EditableFeature(
                            Geometry: null,
                            new Dictionary<string, object?> {
                                ["OBJECTID"] = 202,
                                ["NAME"] = "Empty update result"
                            })
                    ]
                },
                cancellationToken));

        Assert.Contains("updateFeatures", exception.Message, StringComparison.Ordinal);
    }

    private static FeatureServiceClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> handler) {
        return new FeatureServiceClient(
            new HttpClient(new StubHttpMessageHandler(handler)),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });
    }

    private static string ReadRequestBody(HttpRequestMessage request) {
        return request.Content?.ReadAsStringAsync().GetAwaiter().GetResult()
               ?? string.Empty;
    }

    private static Dictionary<string, string> ParseFormBody(string body) {
        return body
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .ToDictionary(
                parts => Uri.UnescapeDataString(parts[0].Replace("+", " ", StringComparison.Ordinal)),
                parts => parts.Length > 1
                    ? Uri.UnescapeDataString(parts[1].Replace("+", " ", StringComparison.Ordinal))
                    : string.Empty,
                StringComparer.Ordinal);
    }
}