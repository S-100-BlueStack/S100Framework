using S100Framework.REST.Abstractions;
using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Exceptions;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using System.Text;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureLayerClientAttachmentEditHardeningTests
{
    [Fact]
    public async Task AddAttachmentAsync_Throws_WhenContentTypeIsInvalid() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var layerClient = CreateLayerClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("content"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            layerClient.AddAttachmentAsync(
                new AddAttachmentRequest {
                    ObjectId = 1,
                    Content = stream,
                    FileName = "photo.jpg",
                    ContentType = "not a media type"
                },
                cancellationToken));

        Assert.Contains("ContentType", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AddAttachmentAsync_Throws_WhenKeywordsAreBlank() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var layerClient = CreateLayerClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("content"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            layerClient.AddAttachmentAsync(
                new AddAttachmentRequest {
                    ObjectId = 1,
                    Content = stream,
                    FileName = "photo.jpg",
                    Keywords = " "
                },
                cancellationToken));

        Assert.Contains("Keywords", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateAttachmentAsync_Throws_WhenContentTypeIsInvalid() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var layerClient = CreateLayerClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("content"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            layerClient.UpdateAttachmentAsync(
                new UpdateAttachmentRequest {
                    ObjectId = 1,
                    AttachmentId = 58,
                    Content = stream,
                    FileName = "photo.jpg",
                    ContentType = "not a media type"
                },
                cancellationToken));

        Assert.Contains("ContentType", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateAttachmentAsync_IgnoresNullUpdateResultItems() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var layerClient = CreateLayerClient(request => {
            if (IsUpdateAttachmentRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "updateAttachmentResults": [
                    null,
                    {
                      "objectId": 58,
                      "globalId": null,
                      "success": true
                    }
                  ],
                  "editMoment": 1735689600000
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("content"));

        var result = await layerClient.UpdateAttachmentAsync(
            new UpdateAttachmentRequest {
                ObjectId = 1,
                AttachmentId = 58,
                Content = stream,
                FileName = "photo.jpg",
                ContentType = "image/jpeg"
            },
            cancellationToken);

        Assert.True(result.Result.Success);
        Assert.Equal(58, result.Result.AttachmentId);
        Assert.Equal(1735689600000, result.EditMoment);
    }

    [Fact]
    public async Task UpdateAttachmentAsync_Throws_WhenOnlyNullUpdateResultsAreReturned() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var layerClient = CreateLayerClient(request => {
            if (IsUpdateAttachmentRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "updateAttachmentResults": [
                    null
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("content"));

        var exception = await Assert.ThrowsAsync<FeatureServiceException>(() =>
            layerClient.UpdateAttachmentAsync(
                new UpdateAttachmentRequest {
                    ObjectId = 1,
                    AttachmentId = 58,
                    Content = stream,
                    FileName = "photo.jpg"
                },
                cancellationToken));

        Assert.Contains("updateAttachmentResults", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeleteAttachmentsAsync_IgnoresNullDeleteResultItems() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var layerClient = CreateLayerClient(request => {
            if (IsDeleteAttachmentsRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "deleteAttachmentResults": [
                    null,
                    {
                      "objectId": 58,
                      "globalId": null,
                      "success": true
                    }
                  ],
                  "editMoment": 1735689600000
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var result = await layerClient.DeleteAttachmentsAsync(
            new DeleteAttachmentsRequest {
                ObjectId = 1,
                AttachmentIds = [58]
            },
            cancellationToken);

        var editResult = Assert.Single(result.Results);

        Assert.True(editResult.Success);
        Assert.Equal(58, editResult.AttachmentId);
        Assert.Equal(1735689600000, result.EditMoment);
    }

    [Fact]
    public async Task DeleteAttachmentsAsync_Throws_WhenAttachmentIdsContainNegativeValue() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var layerClient = CreateLayerClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            layerClient.DeleteAttachmentsAsync(
                new DeleteAttachmentsRequest {
                    ObjectId = 1,
                    AttachmentIds = [58, -1]
                },
                cancellationToken));

        Assert.Contains("AttachmentIds", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeleteAttachmentsAsync_Throws_WhenAttachmentIdsContainDuplicates() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var layerClient = CreateLayerClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            layerClient.DeleteAttachmentsAsync(
                new DeleteAttachmentsRequest {
                    ObjectId = 1,
                    AttachmentIds = [58, 58]
                },
                cancellationToken));

        Assert.Contains("AttachmentIds", exception.Message, StringComparison.Ordinal);
    }

    private static IFeatureLayerClient CreateLayerClient(Func<HttpRequestMessage, HttpResponseMessage> handler) {
        return new FeatureServiceClient(
            new HttpClient(FeatureServiceTestHandlers.WithAttachmentCapabilities(0, handler)),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            }).GetLayerClient(0);
    }

    private static bool IsUpdateAttachmentRequest(HttpRequestMessage request) {
        return request.RequestUri?.AbsolutePath.EndsWith(
            "/FeatureServer/0/1/attachments/58/update",
            StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool IsDeleteAttachmentsRequest(HttpRequestMessage request) {
        return request.RequestUri?.AbsolutePath.EndsWith(
            "/FeatureServer/0/1/deleteAttachments",
            StringComparison.OrdinalIgnoreCase) == true;
    }
}