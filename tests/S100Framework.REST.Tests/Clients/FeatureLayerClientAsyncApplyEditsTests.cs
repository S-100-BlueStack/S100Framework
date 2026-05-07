using NetTopologySuite.Geometries;
using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Exceptions;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using System.Net.Http;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureLayerClientAsyncApplyEditsTests
{
    [Fact]
    public async Task SubmitApplyEditsAsync_PostsAsyncParameterAndReturnsStatusUrl() {
        var cancellationToken = TestContext.Current.CancellationToken;
        HttpMethod? method = null;
        string? requestUri = null;
        string? requestBody = null;

        var handler = new StubHttpMessageHandler(request => {
            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse(supportsAsyncApplyEdits: true);
            }

            method = request.Method;
            requestUri = request.RequestUri!.AbsoluteUri;
            requestBody = request.Content is null
                ? null
                : request.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            return StubHttpMessageHandler.Json("""
            {
              "statusUrl": "https://example.test/jobs/apply-edits-1/status"
            }
            """);
        });

        var geometryFactory = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
        var layerClient = CreateClient(handler).GetLayerClient(0);

        var result = await layerClient.SubmitApplyEditsAsync(
            new FeatureEdits {
                Adds =
                [
                    new EditableFeature(
                        geometryFactory.CreatePoint(new Coordinate(10, 20)),
                        new Dictionary<string, object?>
                        {
                            ["NAME"] = "Added feature"
                        })
                ]
            },
            cancellationToken);

        Assert.Equal(HttpMethod.Post, method);
        Assert.Equal(
            "https://example.test/arcgis/rest/services/Test/FeatureServer/0/applyEdits",
            requestUri);

        Assert.NotNull(requestBody);
        Assert.Contains("async=true", requestBody!, StringComparison.Ordinal);
        Assert.Contains("adds=", requestBody!, StringComparison.Ordinal);

        Assert.True(result.IsPending);
        Assert.Null(result.Result);
        Assert.Equal(
            "https://example.test/jobs/apply-edits-1/status",
            result.StatusUrl!.AbsoluteUri);
    }

    [Fact]
    public async Task SubmitApplyEditsAsync_Throws_WhenLayerDoesNotSupportAsyncApplyEdits() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var handler = new StubHttpMessageHandler(request => {
            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse(supportsAsyncApplyEdits: false);
            }

            throw new InvalidOperationException("applyEdits endpoint should not be called.");
        });

        var layerClient = CreateClient(handler).GetLayerClient(0);

        var exception = await Assert.ThrowsAsync<FeatureServiceCapabilityException>(() =>
            layerClient.SubmitApplyEditsAsync(
                new FeatureEdits {
                    Deletes = [1]
                },
                cancellationToken));

        Assert.Contains("asynchronous applyEdits", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetApplyEditsStatusAsync_MapsCompletedStatus() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var handler = new StubHttpMessageHandler(_ =>
            StubHttpMessageHandler.Json("""
            {
              "status": "COMPLETED",
              "resultUrl": "https://example.test/results/apply-edits-1.json",
              "submissionTime": 1710000000000,
              "lastUpdatedTime": 1710000005000
            }
            """));

        var layerClient = CreateClient(handler).GetLayerClient(0);

        var status = await layerClient.GetApplyEditsStatusAsync(
            new Uri("https://example.test/jobs/apply-edits-1/status"),
            cancellationToken);

        Assert.True(status.IsTerminal);
        Assert.True(status.IsCompleted);
        Assert.Equal("COMPLETED", status.Status);
        Assert.Equal(
            "https://example.test/results/apply-edits-1.json",
            status.ResultUrl!.AbsoluteUri);
        Assert.Equal(1710000000000, status.SubmissionTime);
        Assert.Equal(1710000005000, status.LastUpdatedTime);
    }

    [Fact]
    public async Task GetApplyEditsStatusAsync_ThrowsFeatureServiceException_WhenResultUrlIsInvalid() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var handler = new StubHttpMessageHandler(_ =>
            StubHttpMessageHandler.Json("""
        {
          "status": "COMPLETED",
          "resultUrl": "not a valid absolute uri"
        }
        """));

        var layerClient = CreateClient(handler).GetLayerClient(0);

        var exception = await Assert.ThrowsAsync<FeatureServiceException>(() =>
            layerClient.GetApplyEditsStatusAsync(
                new Uri("https://example.test/jobs/apply-edits-1/status"),
                cancellationToken));

        Assert.Contains("resultUrl", exception.Message, StringComparison.Ordinal);
        Assert.Contains("applyEdits", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetApplyEditsResultAsync_MapsEditResults() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var handler = new StubHttpMessageHandler(_ =>
            StubHttpMessageHandler.Json("""
            {
              "addResults": [
                { "success": true, "objectId": 101 }
              ],
              "updateResults": [
                { "success": false, "error": { "code": 1000, "description": "Validation failed." } }
              ],
              "deleteResults": []
            }
            """));

        var layerClient = CreateClient(handler).GetLayerClient(0);

        var result = await layerClient.GetApplyEditsResultAsync(
            new Uri("https://example.test/results/apply-edits-1.json"),
            cancellationToken);

        Assert.Single(result.AddResults);
        Assert.Single(result.UpdateResults);
        Assert.Empty(result.DeleteResults);

        Assert.True(result.AddResults[0].Success);
        Assert.Equal(101, result.AddResults[0].ObjectId);

        Assert.False(result.UpdateResults[0].Success);
        Assert.Equal(1000, result.UpdateResults[0].ErrorCode);
        Assert.Equal("Validation failed.", result.UpdateResults[0].ErrorDescription);
    }

    [Fact]
    public async Task SubmitApplyEditsAsync_ThrowsFeatureServiceException_WhenStatusUrlIsInvalid() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var handler = new StubHttpMessageHandler(request => {
            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse(supportsAsyncApplyEdits: true);
            }

            if (request.RequestUri!.AbsolutePath.EndsWith(
                "/FeatureServer/0/applyEdits",
                StringComparison.OrdinalIgnoreCase)) {
                return StubHttpMessageHandler.Json("""
            {
              "statusUrl": "not a valid absolute uri"
            }
            """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var layerClient = CreateClient(handler).GetLayerClient(0);

        var exception = await Assert.ThrowsAsync<FeatureServiceException>(() =>
            layerClient.SubmitApplyEditsAsync(
                new FeatureEdits {
                    Deletes = [1]
                },
                cancellationToken));

        Assert.Contains("statusUrl", exception.Message, StringComparison.Ordinal);
        Assert.Contains("applyEdits", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SubmitApplyEditsAsync_ThrowsFeatureServiceException_WhenStatusUrlIsNotAString() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var handler = new StubHttpMessageHandler(request => {
            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse(supportsAsyncApplyEdits: true);
            }

            if (request.RequestUri!.AbsolutePath.EndsWith(
                "/FeatureServer/0/applyEdits",
                StringComparison.OrdinalIgnoreCase)) {
                return StubHttpMessageHandler.Json("""
            {
              "statusUrl": 123
            }
            """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var layerClient = CreateClient(handler).GetLayerClient(0);

        var exception = await Assert.ThrowsAsync<FeatureServiceException>(() =>
            layerClient.SubmitApplyEditsAsync(
                new FeatureEdits {
                    Deletes = [1]
                },
                cancellationToken));

        Assert.Contains("statusUrl", exception.Message, StringComparison.Ordinal);
        Assert.Contains("applyEdits", exception.Message, StringComparison.Ordinal);
    }

    private static FeatureServiceClient CreateClient(HttpMessageHandler handler) {
        return new FeatureServiceClient(
            new HttpClient(handler),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });
    }

    private static bool IsLayerMetadataRequest(HttpRequestMessage request) {
        return request.RequestUri?.AbsolutePath.EndsWith(
            "/FeatureServer/0",
            StringComparison.OrdinalIgnoreCase) == true;
    }

    private static HttpResponseMessage CreateLayerMetadataResponse(bool supportsAsyncApplyEdits) {
        var supportsAsyncApplyEditsJson = supportsAsyncApplyEdits ? "true" : "false";

        return StubHttpMessageHandler.Json($$"""
        {
          "id": 0,
          "name": "Layer 0",
          "geometryType": "esriGeometryPoint",
          "advancedQueryCapabilities": {
            "supportsPagination": true
          },
          "advancedEditingCapabilities": {
            "supportsAsyncApplyEdits": {{supportsAsyncApplyEditsJson}}
          },
          "fields": [],
          "relationships": []
        }
        """);
    }
}