using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using System.Net.Http;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureLayerClientDeleteAttachmentsTests
{
    [Fact]
    public async Task DeleteAttachmentsAsync_PostsAttachmentIds_AndMapsResults() {
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
              "deleteAttachmentResults": [
                {
                  "objectId": 58,
                  "globalId": null,
                  "success": true
                },
                {
                  "objectId": 4,
                  "globalId": null,
                  "success": false,
                  "error": {
                    "code": 50,
                    "description": "Attachment not found"
                  }
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

        var result = await layerClient.DeleteAttachmentsAsync(
            new DeleteAttachmentsRequest {
                ObjectId = 818654,
                AttachmentIds = [58, 4],
                RollbackOnFailure = false,
                ReturnEditMoment = true
            });

        Assert.Equal(HttpMethod.Post, method);
        Assert.Equal(
            "https://example.test/arcgis/rest/services/Test/FeatureServer/0/818654/deleteAttachments",
            requestUri);

        Assert.NotNull(requestBody);
        Assert.Contains("f=json", requestBody);
        Assert.Contains("attachmentIds=58%2C4", requestBody);
        Assert.Contains("rollbackOnFailure=false", requestBody);
        Assert.Contains("returnEditMoment=true", requestBody);

        Assert.Equal(2, result.Results.Count);
        Assert.Equal(1735689600000, result.EditMoment);

        Assert.True(result.Results[0].Success);
        Assert.Equal(58, result.Results[0].AttachmentId);

        Assert.False(result.Results[1].Success);
        Assert.Equal(4, result.Results[1].AttachmentId);
        Assert.Equal(50, result.Results[1].ErrorCode);
        Assert.Equal("Attachment not found", result.Results[1].ErrorDescription);
    }

    [Fact]
    public async Task DeleteAttachmentsAsync_IncludesGdbVersion_WhenProvided() {
        string? requestBody = null;

        var handler = new StubHttpMessageHandler(request => {
            requestBody = request.Content is null
                ? null
                : request.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            return StubHttpMessageHandler.Json("""
            {
              "deleteAttachmentResults": [
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

        var result = await layerClient.DeleteAttachmentsAsync(
            new DeleteAttachmentsRequest {
                ObjectId = 1,
                AttachmentIds = [58],
                GdbVersion = "SDE.DEFAULT"
            });

        Assert.Single(result.Results);
        Assert.NotNull(requestBody);
        Assert.Contains("gdbVersion=SDE.DEFAULT", requestBody);
    }

    [Fact]
    public async Task DeleteAttachmentsAsync_Throws_WhenAttachmentIdsAreMissing() {
        var layerClient = new FeatureServiceClient(
            new HttpClient(new StubHttpMessageHandler(_ =>
                throw new InvalidOperationException("The HTTP request should not be executed."))),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            }).GetLayerClient(0);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            layerClient.DeleteAttachmentsAsync(
                new DeleteAttachmentsRequest {
                    ObjectId = 1,
                    AttachmentIds = []
                }));

        Assert.Contains("attachment ID", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}