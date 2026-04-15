using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Exceptions;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using System.IO;
using System.Net.Http;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureLayerClientAttachmentCapabilityTests
{
    [Fact]
    public async Task QueryAttachmentsAsync_Throws_WhenLayerDoesNotSupportAttachments() {
        var client = CreateClient(request => {
            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse(
                    hasAttachments: false,
                    supportsQueryAttachments: false);
            }

            throw new InvalidOperationException("Attachment endpoint should not be called.");
        });

        var layerClient = client.GetLayerClient(0);

        var exception = await Assert.ThrowsAsync<FeatureServiceCapabilityException>(() =>
            layerClient.QueryAttachmentsAsync(new AttachmentQuery {
                ObjectIds = [1]
            }));

        Assert.Contains("does not support attachments", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QueryAttachmentsAsync_Throws_WhenLayerDoesNotSupportAttachmentQueries() {
        var client = CreateClient(request => {
            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse(
                    hasAttachments: true,
                    supportsQueryAttachments: false);
            }

            throw new InvalidOperationException("Attachment query endpoint should not be called.");
        });

        var layerClient = client.GetLayerClient(0);

        var exception = await Assert.ThrowsAsync<FeatureServiceCapabilityException>(() =>
            layerClient.QueryAttachmentsAsync(new AttachmentQuery {
                ObjectIds = [1]
            }));

        Assert.Contains("attachment queries", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddAttachmentAsync_Throws_WhenServiceDoesNotSupportUploads() {
        var client = CreateClient(request => {
            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse(
                    hasAttachments: true,
                    supportsQueryAttachments: true);
            }

            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse("Query,Editing");
            }

            throw new InvalidOperationException("addAttachment endpoint should not be called.");
        });

        var layerClient = client.GetLayerClient(0);

        using var content = new MemoryStream([1, 2, 3]);

        var exception = await Assert.ThrowsAsync<FeatureServiceCapabilityException>(() =>
            layerClient.AddAttachmentAsync(new AddAttachmentRequest {
                ObjectId = 1,
                Content = content,
                FileName = "test.bin",
                ContentType = "application/octet-stream"
            }));

        Assert.Contains("uploads", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeleteAttachmentsAsync_Throws_WhenServiceDoesNotSupportEditing() {
        var client = CreateClient(request => {
            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse(
                    hasAttachments: true,
                    supportsQueryAttachments: true);
            }

            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse("Query");
            }

            throw new InvalidOperationException("deleteAttachments endpoint should not be called.");
        });

        var layerClient = client.GetLayerClient(0);

        var exception = await Assert.ThrowsAsync<FeatureServiceCapabilityException>(() =>
            layerClient.DeleteAttachmentsAsync(new DeleteAttachmentsRequest {
                ObjectId = 1,
                AttachmentIds = [10]
            }));

        Assert.Contains("editing", exception.Message, StringComparison.OrdinalIgnoreCase);
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

    private static bool IsLayerMetadataRequest(HttpRequestMessage request) {
        return request.RequestUri?.AbsolutePath.EndsWith(
            "/FeatureServer/0",
            StringComparison.OrdinalIgnoreCase) == true;
    }

    private static HttpResponseMessage CreateServiceMetadataResponse(string capabilities) {
        return StubHttpMessageHandler.Json($$"""
        {
          "capabilities": "{{capabilities}}",
          "layers": [
            { "id": 0, "name": "Layer 0" }
          ],
          "tables": []
        }
        """);
    }

    private static HttpResponseMessage CreateLayerMetadataResponse(
        bool hasAttachments,
        bool supportsQueryAttachments) {
        var hasAttachmentsJson = hasAttachments ? "true" : "false";
        var supportsQueryAttachmentsJson = supportsQueryAttachments ? "true" : "false";

        return StubHttpMessageHandler.Json($$"""
        {
          "id": 0,
          "name": "Layer 0",
          "geometryType": "esriGeometryPoint",
          "hasAttachments": {{hasAttachmentsJson}},
          "supportsQueryAttachments": {{supportsQueryAttachmentsJson}},
          "advancedQueryCapabilities": {
            "supportsPagination": true
          },
          "fields": [],
          "relationships": []
        }
        """);
    }
}