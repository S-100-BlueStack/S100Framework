using System.Text;
using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Exceptions;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureServiceClientUploadHardeningTests
{
    [Fact]
    public async Task UploadItemAsync_Throws_WhenContentTypeIsInvalid() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        await using var content = new MemoryStream(Encoding.UTF8.GetBytes("hello-upload"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.UploadItemAsync(
                new FeatureServiceUploadRequest {
                    Content = content,
                    FileName = "source.zip",
                    ContentType = "not a media type"
                },
                cancellationToken));

        Assert.Contains("ContentType", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UploadItemAsync_Throws_WhenServerReportsFailure() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse(supportsUploads: true);
            }

            if (IsUploadRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "success": false
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        await using var content = new MemoryStream(Encoding.UTF8.GetBytes("hello-upload"));

        var exception = await Assert.ThrowsAsync<FeatureServiceException>(() =>
            client.UploadItemAsync(
                new FeatureServiceUploadRequest {
                    Content = content,
                    FileName = "source.zip",
                    ContentType = "application/zip"
                },
                cancellationToken));

        Assert.Contains("upload", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UploadItemAsync_Throws_WhenServerOmitsItem() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse(supportsUploads: true);
            }

            if (IsUploadRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "success": true
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        await using var content = new MemoryStream(Encoding.UTF8.GetBytes("hello-upload"));

        var exception = await Assert.ThrowsAsync<FeatureServiceException>(() =>
            client.UploadItemAsync(
                new FeatureServiceUploadRequest {
                    Content = content,
                    FileName = "source.zip",
                    ContentType = "application/zip"
                },
                cancellationToken));

        Assert.Contains("item ID", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UploadItemAsync_Throws_WhenServerReturnsBlankItemId() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse(supportsUploads: true);
            }

            if (IsUploadRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "success": true,
                  "item": {
                    "itemID": " "
                  }
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        await using var content = new MemoryStream(Encoding.UTF8.GetBytes("hello-upload"));

        var exception = await Assert.ThrowsAsync<FeatureServiceException>(() =>
            client.UploadItemAsync(
                new FeatureServiceUploadRequest {
                    Content = content,
                    FileName = "source.zip",
                    ContentType = "application/zip"
                },
                cancellationToken));

        Assert.Contains("item ID", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeleteUploadItemAsync_EscapesItemIdInDeleteUrl() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<Uri>();

        var client = CreateClient(request => {
            requestUris.Add(request.RequestUri!);

            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse(supportsUploads: true);
            }

            if (request.RequestUri?.AbsolutePath.EndsWith(
                    "/FeatureServer/uploads/folder%2Fitem%20one/delete",
                    StringComparison.OrdinalIgnoreCase) == true) {
                return StubHttpMessageHandler.Json("""
                {
                  "success": true
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var result = await client.DeleteUploadItemAsync(
            "folder/item one",
            cancellationToken);

        Assert.True(result.Success);

        Assert.Contains(
            requestUris,
            uri => uri.AbsolutePath.EndsWith(
                "/FeatureServer/uploads/folder%2Fitem%20one/delete",
                StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task DeleteUploadItemAsync_Throws_WhenServerOmitsSuccessValue() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse(supportsUploads: true);
            }

            if (IsUploadDeleteRequest(request)) {
                return StubHttpMessageHandler.Json("{}");
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceException>(() =>
            client.DeleteUploadItemAsync(
                "0c6b928f590f49ebac04761bab413e49",
                cancellationToken));

        Assert.Contains("success", exception.Message, StringComparison.OrdinalIgnoreCase);
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

    private static bool IsUploadRequest(HttpRequestMessage request) {
        return request.RequestUri?.AbsolutePath.EndsWith(
            "/FeatureServer/uploads/upload",
            StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool IsUploadDeleteRequest(HttpRequestMessage request) {
        return request.RequestUri?.AbsolutePath.EndsWith(
            "/FeatureServer/uploads/0c6b928f590f49ebac04761bab413e49/delete",
            StringComparison.OrdinalIgnoreCase) == true;
    }

    private static HttpResponseMessage CreateServiceMetadataResponse(bool supportsUploads) {
        var capabilities = supportsUploads
            ? "Query,Uploads"
            : "Query";

        return StubHttpMessageHandler.Json($$"""
        {
          "layers": [],
          "tables": [],
          "capabilities": "{{capabilities}}"
        }
        """);
    }
}