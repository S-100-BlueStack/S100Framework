using NetTopologySuite.Geometries;
using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Exceptions;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using System.Net.Http;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureLayerClientApplyEditsTests
{
    [Fact]
    public async Task ApplyEditsAsync_PostsAddsUpdatesAndDeletes() {
        HttpMethod? method = null;
        string? requestUri = null;
        string? requestBody = null;

        var handler = new StubHttpMessageHandler(request => {
            method = request.Method;
            requestUri = request.RequestUri!.AbsoluteUri;
            requestBody = request.Content is null
                ? null
                : request.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            return StubHttpMessageHandler.Json("""
            {
              "addResults": [
                { "success": true, "objectId": 101 }
              ],
              "updateResults": [
                { "success": true, "objectId": 202 }
              ],
              "deleteResults": [
                { "success": true, "objectId": 303 }
              ]
            }
            """);
        });

        var geometryFactory = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

        var layerClient = new FeatureServiceClient(
            new HttpClient(handler),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            }).GetLayerClient(0);

        var result = await layerClient.ApplyEditsAsync(
            new FeatureEdits {
                Adds =
                [
                    new EditableFeature(
                        geometryFactory.CreatePoint(new Coordinate(10, 20)),
                        new Dictionary<string, object?>
                        {
                            ["NAME"] = "Added feature"
                        })
                ],
                Updates =
                [
                    new EditableFeature(
                        geometryFactory.CreatePoint(new Coordinate(11, 21)),
                        new Dictionary<string, object?>
                        {
                            ["OBJECTID"] = 202,
                            ["NAME"] = "Updated feature"
                        })
                ],
                Deletes = [303]
            });

        Assert.Equal(HttpMethod.Post, method);
        Assert.Equal("https://example.test/arcgis/rest/services/Test/FeatureServer/0/applyEdits", requestUri);
        Assert.NotNull(requestBody);

        Assert.Contains("f=json", requestBody);
        Assert.Contains("rollbackOnFailure=true", requestBody);
        Assert.Contains("adds=", requestBody);
        Assert.Contains("updates=", requestBody);
        Assert.Contains("deletes=303", requestBody);

        Assert.Single(result.AddResults);
        Assert.Single(result.UpdateResults);
        Assert.Single(result.DeleteResults);

        Assert.True(result.AddResults[0].Success);
        Assert.Equal(101, result.AddResults[0].ObjectId);

        Assert.True(result.UpdateResults[0].Success);
        Assert.Equal(202, result.UpdateResults[0].ObjectId);

        Assert.True(result.DeleteResults[0].Success);
        Assert.Equal(303, result.DeleteResults[0].ObjectId);
    }

    [Fact]
    public async Task ApplyEditsAsync_MapsFailedEditResults() {
        var handler = new StubHttpMessageHandler(_ =>
            StubHttpMessageHandler.Json("""
            {
              "addResults": [
                {
                  "success": false,
                  "error": {
                    "code": 1000,
                    "description": "Validation failed."
                  }
                }
              ],
              "updateResults": [],
              "deleteResults": []
            }
            """));

        var layerClient = new FeatureServiceClient(
            new HttpClient(handler),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            }).GetLayerClient(0);

        var result = await layerClient.ApplyEditsAsync(
            new FeatureEdits {
                Adds =
                [
                    new EditableFeature(
                        null,
                        new Dictionary<string, object?>
                        {
                            ["NAME"] = "Invalid feature"
                        })
                ]
            });

        Assert.Single(result.AddResults);
        Assert.False(result.AddResults[0].Success);
        Assert.Equal(1000, result.AddResults[0].ErrorCode);
        Assert.Equal("Validation failed.", result.AddResults[0].ErrorDescription);
    }

    [Fact]
    public async Task ApplyEditsAsync_Throws_WhenNoEditsAreProvided() {
        var layerClient = new FeatureServiceClient(
            new HttpClient(new StubHttpMessageHandler(_ =>
                throw new InvalidOperationException("The HTTP request should not be executed."))),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            }).GetLayerClient(0);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            layerClient.ApplyEditsAsync(new FeatureEdits()));

        Assert.Contains("Adds, Updates, or Deletes", exception.Message);
    }

    [Fact]
    public async Task WaitForApplyEditsCompletionAsync_PollsStatusUntilCompleted_AndReturnsResult() {
        const string statusUrl = "https://example.test/arcgis/rest/services/Test/FeatureServer/jobs/applyEdits-123/status";
        const string resultUrl = "https://example.test/arcgis/rest/directories/arcgisoutput/Test_MapServer/applyEdits-123.json";

        var statusCalls = 0;

        var handler = new StubHttpMessageHandler(request => {
            var uri = request.RequestUri!.AbsoluteUri;

            if (uri == statusUrl) {
                statusCalls++;

                return statusCalls == 1
                    ? StubHttpMessageHandler.Json("""
                {
                  "status": "InProgress"
                }
                """)
                    : StubHttpMessageHandler.Json($$"""
                {
                  "status": "Completed",
                  "resultUrl": "{{resultUrl}}"
                }
                """);
            }

            if (uri == resultUrl) {
                return StubHttpMessageHandler.Json("""
            {
              "addResults": [
                { "success": true, "objectId": 101 }
              ],
              "updateResults": [],
              "deleteResults": []
            }
            """);
            }

            throw new InvalidOperationException($"Unexpected request URI: {uri}");
        });

        var layerClient = new FeatureServiceClient(
            new HttpClient(handler),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            }).GetLayerClient(0);

        var result = await layerClient.WaitForApplyEditsCompletionAsync(
            new Uri(statusUrl),
            new ApplyEditsWaitOptions {
                PollInterval = TimeSpan.Zero,
                Timeout = TimeSpan.FromSeconds(1)
            });

        Assert.Equal(2, statusCalls);
        Assert.Single(result.AddResults);
        Assert.Equal(101, result.AddResults[0].ObjectId);
    }

    [Fact]
    public async Task WaitForApplyEditsCompletionAsync_ThrowsTimeout_WhenJobDoesNotComplete() {
        const string statusUrl = "https://example.test/arcgis/rest/services/Test/FeatureServer/jobs/applyEdits-123/status";

        var handler = new StubHttpMessageHandler(request => {
            var uri = request.RequestUri!.AbsoluteUri;

            if (uri == statusUrl) {
                return StubHttpMessageHandler.Json("""
            {
              "status": "InProgress"
            }
            """);
            }

            throw new InvalidOperationException($"Unexpected request URI: {uri}");
        });

        var layerClient = new FeatureServiceClient(
            new HttpClient(handler),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            }).GetLayerClient(0);

        await Assert.ThrowsAsync<TimeoutException>(() =>
            layerClient.WaitForApplyEditsCompletionAsync(
                new Uri(statusUrl),
                new ApplyEditsWaitOptions {
                    PollInterval = TimeSpan.Zero,
                    Timeout = TimeSpan.Zero
                }));
    }

    [Fact]
    public async Task WaitForApplyEditsCompletionAsync_TreatsCompletedWithErrorsStatusAsCompleted() {
        const string statusUrl = "https://example.test/arcgis/rest/services/Test/FeatureServer/jobs/applyEdits-123/status";
        const string resultUrl = "https://example.test/arcgis/rest/directories/arcgisoutput/Test_MapServer/applyEdits-123.json";

        var handler = new StubHttpMessageHandler(request => {
            var uri = request.RequestUri!.AbsoluteUri;

            if (uri == statusUrl) {
                return StubHttpMessageHandler.Json($$"""
            {
              "status": "Completed_With_Errors",
              "resultUrl": "{{resultUrl}}"
            }
            """);
            }

            if (uri == resultUrl) {
                return StubHttpMessageHandler.Json("""
            {
              "addResults": [
                {
                  "success": false,
                  "error": {
                    "code": 1000,
                    "description": "Validation failed."
                  }
                }
              ],
              "updateResults": [],
              "deleteResults": []
            }
            """);
            }

            throw new InvalidOperationException($"Unexpected request URI: {uri}");
        });

        var layerClient = new FeatureServiceClient(
            new HttpClient(handler),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            }).GetLayerClient(0);

        var result = await layerClient.WaitForApplyEditsCompletionAsync(
            new Uri(statusUrl),
            new ApplyEditsWaitOptions {
                PollInterval = TimeSpan.Zero,
                Timeout = TimeSpan.FromSeconds(1)
            });

        Assert.Single(result.AddResults);
        Assert.False(result.AddResults[0].Success);
        Assert.Equal(1000, result.AddResults[0].ErrorCode);
        Assert.Equal("Validation failed.", result.AddResults[0].ErrorDescription);
    }

    [Fact]
    public async Task WaitForApplyEditsCompletionAsync_Throws_WhenCompletedStatusHasNoResultUrl() {
        const string statusUrl = "https://example.test/arcgis/rest/services/Test/FeatureServer/jobs/applyEdits-123/status";

        var handler = new StubHttpMessageHandler(request => {
            var uri = request.RequestUri!.AbsoluteUri;

            if (uri == statusUrl) {
                return StubHttpMessageHandler.Json("""
            {
              "status": "Completed"
            }
            """);
            }

            throw new InvalidOperationException($"Unexpected request URI: {uri}");
        });

        var layerClient = new FeatureServiceClient(
            new HttpClient(handler),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            }).GetLayerClient(0);

        var exception = await Assert.ThrowsAsync<FeatureServiceException>(() =>
            layerClient.WaitForApplyEditsCompletionAsync(
                new Uri(statusUrl),
                new ApplyEditsWaitOptions {
                    PollInterval = TimeSpan.Zero,
                    Timeout = TimeSpan.FromSeconds(1)
                }));

        Assert.Contains("completed without a resultUrl", exception.Message);
    }
}