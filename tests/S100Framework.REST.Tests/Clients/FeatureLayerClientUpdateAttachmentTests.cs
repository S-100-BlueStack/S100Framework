using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using System.Net.Http;
using System.Text;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureLayerClientUpdateAttachmentTests
{
    [Fact]
    public async Task UpdateAttachmentAsync_PostsMultipartFormData_AndMapsSingleResult() {
        HttpMethod? method = null;
        string? requestUri = null;
        string? contentType = null;
        string? requestBody = null;

        var handler = new StubHttpMessageHandler(request => {
            method = request.Method;
            requestUri = request.RequestUri!.AbsoluteUri;
            contentType = request.Content?.Headers.ContentType?.ToString();
            requestBody = request.Content is null
                ? null
                : request.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            return StubHttpMessageHandler.Json("""
            {
              "updateAttachmentResults": [
                {
                  "objectId": 58,
                  "globalId": null,
                  "success": true
                }
              ],
              "editMoment": 1735689600000
            }
            """);
        });

        var layerClient = new FeatureServiceClient(
            new HttpClient(handler),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            }).GetLayerClient(0);

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("updated-attachment-content"));

        var result = await layerClient.UpdateAttachmentAsync(
            new UpdateAttachmentRequest {
                ObjectId = 818654,
                AttachmentId = 58,
                Content = stream,
                FileName = "photo-updated.jpg",
                ContentType = "image/jpeg",
                Keywords = "harbor,photo,updated",
                ReturnEditMoment = true
            });

        Assert.Equal(HttpMethod.Post, method);
        Assert.Equal(
            "https://example.test/arcgis/rest/services/Test/FeatureServer/0/818654/attachments/58/update",
            requestUri);

        Assert.NotNull(contentType);
        Assert.Contains("multipart/form-data", contentType, StringComparison.OrdinalIgnoreCase);

        Assert.NotNull(requestBody);
        Assert.Contains("name=\"f\"", requestBody);
        Assert.Contains("json", requestBody);
        Assert.Contains("name=\"keywords\"", requestBody);
        Assert.Contains("harbor,photo,updated", requestBody);
        Assert.Contains("name=\"returnEditMoment\"", requestBody);
        Assert.Contains("true", requestBody);
        Assert.Contains("name=\"attachment\"; filename=\"photo-updated.jpg\"", requestBody);
        Assert.Contains("updated-attachment-content", requestBody);

        Assert.True(result.Result.Success);
        Assert.Equal(58, result.Result.AttachmentId);
        Assert.Equal(1735689600000, result.EditMoment);
    }

    [Fact]
    public async Task UpdateAttachmentAsync_IncludesGdbVersion_WhenProvided() {
        string? requestBody = null;

        var handler = new StubHttpMessageHandler(request => {
            requestBody = request.Content is null
                ? null
                : request.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            return StubHttpMessageHandler.Json("""
            {
              "updateAttachmentResults": [
                {
                  "objectId": 58,
                  "globalId": null,
                  "success": true
                }
              ]
            }
            """);
        });

        var layerClient = new FeatureServiceClient(
            new HttpClient(handler),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            }).GetLayerClient(0);

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("content"));

        var result = await layerClient.UpdateAttachmentAsync(
            new UpdateAttachmentRequest {
                ObjectId = 1,
                AttachmentId = 58,
                Content = stream,
                FileName = "doc.txt",
                GdbVersion = "SDE.DEFAULT"
            });

        Assert.True(result.Result.Success);
        Assert.NotNull(requestBody);
        Assert.Contains("name=\"gdbVersion\"", requestBody);
        Assert.Contains("SDE.DEFAULT", requestBody);
    }

    [Fact]
    public async Task UpdateAttachmentAsync_MapsFailedResult() {
        var handler = new StubHttpMessageHandler(_ =>
            StubHttpMessageHandler.Json("""
            {
              "updateAttachmentResults": [
                {
                  "success": false,
                  "error": {
                    "code": 1009,
                    "description": "Attachment update failed."
                  }
                }
              ]
            }
            """));

        var layerClient = new FeatureServiceClient(
            new HttpClient(handler),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            }).GetLayerClient(0);

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("content"));

        var result = await layerClient.UpdateAttachmentAsync(
            new UpdateAttachmentRequest {
                ObjectId = 1,
                AttachmentId = 58,
                Content = stream,
                FileName = "doc.txt"
            });

        Assert.False(result.Result.Success);
        Assert.Equal(1009, result.Result.ErrorCode);
        Assert.Equal("Attachment update failed.", result.Result.ErrorDescription);
    }

    [Fact]
    public async Task UpdateAttachmentAsync_Throws_WhenAttachmentIdIsMissing() {
        var layerClient = new FeatureServiceClient(
            new HttpClient(new StubHttpMessageHandler(_ =>
                throw new InvalidOperationException("The HTTP request should not be executed."))),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            }).GetLayerClient(0);

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("content"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            layerClient.UpdateAttachmentAsync(
                new UpdateAttachmentRequest {
                    ObjectId = 1,
                    AttachmentId = -1,
                    Content = stream,
                    FileName = "doc.txt"
                }));

        Assert.Contains("AttachmentId", exception.Message);
    }
}