using S100Framework.REST.Abstractions;
using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureLayerClientAttachmentsTests
{
    [Fact]
    public async Task QueryAttachmentsAsync_SendsExpectedParameters_AndMapsGroups() {
        var requestUris = new List<string>();

        var handler = new StubHttpMessageHandler(request => {
            var uri = request.RequestUri!.AbsoluteUri;
            requestUris.Add(uri);

            if (uri.Contains("/FeatureServer/0/queryAttachments?")) {
                return StubHttpMessageHandler.Json("""
                {
                  "attachmentGroups": [
                    {
                      "parentObjectId": 100,
                      "parentGlobalId": "{AAA}",
                      "attachmentInfos": [
                        {
                          "id": 1,
                          "globalId": "{ATT-1}",
                          "name": "photo-a.jpg",
                          "contentType": "image/jpeg",
                          "size": 1234,
                          "keywords": "harbor,photo",
                          "url": "https://example.test/a.jpg"
                        },
                        {
                          "id": 2,
                          "name": "report.pdf",
                          "contentType": "application/pdf",
                          "size": 9999
                        }
                      ]
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        IFeatureServiceClient serviceClient = new FeatureServiceClient(
            new HttpClient(handler),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });

        var layerClient = serviceClient.GetLayerClient(0);

        var groups = await layerClient.QueryAttachmentsAsync(
            new AttachmentQuery {
                ObjectIds = [100, 200],
                DefinitionExpression = "1=1",
                AttachmentTypes = ["image/jpeg", "application/pdf"],
                Keywords = ["harbor", "photo"],
                MinimumSizeBytes = 100,
                MaximumSizeBytes = 10000,
                ReturnUrl = true,
                ReturnMetadata = true
            });

        Assert.Single(groups);
        Assert.Equal(100, groups[0].SourceObjectId);
        Assert.Equal("{AAA}", groups[0].SourceGlobalId);
        Assert.Equal(2, groups[0].Attachments.Count);

        Assert.Equal(1, groups[0].Attachments[0].AttachmentId);
        Assert.Equal("photo-a.jpg", groups[0].Attachments[0].Name);
        Assert.Equal("image/jpeg", groups[0].Attachments[0].ContentType);
        Assert.Equal(1234, groups[0].Attachments[0].Size);
        Assert.Equal("https://example.test/a.jpg", groups[0].Attachments[0].Url);
        Assert.Equal("harbor,photo", groups[0].Attachments[0].GetString("keywords"));

        var requestUri = Assert.Single(requestUris);
        Assert.Contains("objectIds=100%2C200", requestUri);
        Assert.Contains("definitionExpression=1%3D1", requestUri);
        Assert.Contains("attachmentTypes=image%2Fjpeg%2Capplication%2Fpdf", requestUri);
        Assert.Contains("keywords=harbor%2Cphoto", requestUri);
        Assert.Contains("size=100%2C10000", requestUri);
        Assert.Contains("returnUrl=true", requestUri);
        Assert.Contains("returnMetadata=true", requestUri);
    }

    [Fact]
    public async Task DownloadAttachmentAsync_ReturnsBytesContentTypeAndFileName() {
        var fileBytes = Encoding.UTF8.GetBytes("attachment-content");

        var handler = new StubHttpMessageHandler(request => {
            var uri = request.RequestUri!.AbsoluteUri;

            if (uri.Contains("/FeatureServer/0/100/attachments/7")) {
                var response = new HttpResponseMessage(HttpStatusCode.OK) {
                    Content = new ByteArrayContent(fileBytes)
                };

                response.Content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
                response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment") {
                    FileName = "\"photo.jpg\""
                };

                return response;
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        var layerClient = new FeatureServiceClient(
            new HttpClient(handler),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            }).GetLayerClient(0);

        var content = await layerClient.DownloadAttachmentAsync(100, 7);

        Assert.Equal(fileBytes, content.Content);
        Assert.Equal("image/jpeg", content.ContentType);
        Assert.Equal("photo.jpg", content.FileName);
    }

    [Fact]
    public async Task QueryAttachmentsAsync_Throws_WhenNoSelectorIsProvided() {
        var layerClient = new FeatureServiceClient(
            new HttpClient(new StubHttpMessageHandler(_ =>
                throw new InvalidOperationException("The HTTP request should not be executed."))),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            }).GetLayerClient(0);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            layerClient.QueryAttachmentsAsync(new AttachmentQuery()));

        Assert.Contains("ObjectIds or DefinitionExpression", exception.Message);
    }
}