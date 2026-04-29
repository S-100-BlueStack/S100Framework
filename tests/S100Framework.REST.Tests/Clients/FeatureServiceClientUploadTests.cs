using System.Text;
using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Exceptions;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureServiceClientUploadTests
{
    [Fact]
    public async Task UploadItemAsync_SendsMultipartUploadRequest() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestBodies = new List<string>();

        var client = CreateClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;

            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse(supportsUploads: true);
            }

            if (uri.EndsWith("/FeatureServer/uploads/upload", StringComparison.OrdinalIgnoreCase)) {
                requestBodies.Add(ReadRequestBody(request));

                return StubHttpMessageHandler.Json("""
                {
                  "success": true,
                  "item": {
                    "itemID": "0c6b928f590f49ebac04761bab413e49",
                    "itemName": "source.zip",
                    "description": "Append source",
                    "date": 1246060800000,
                    "committed": true
                  }
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        await using var content = new MemoryStream(Encoding.UTF8.GetBytes("hello-upload"));

        var result = await client.UploadItemAsync(
            new FeatureServiceUploadRequest {
                Content = content,
                FileName = "source.zip",
                ContentType = "application/zip",
                Description = "Append source"
            },
            cancellationToken);

        Assert.Equal("0c6b928f590f49ebac04761bab413e49", result.ItemId);
        Assert.Equal("source.zip", result.ItemName);
        Assert.Equal("Append source", result.Description);
        Assert.Equal(1246060800000, result.Date);
        Assert.True(result.Committed);

        var body = Assert.Single(requestBodies);

        AssertMultipartField(body, "f");
        Assert.Contains("json", body, StringComparison.Ordinal);
        AssertMultipartField(body, "description");
        Assert.Contains("Append source", body, StringComparison.Ordinal);
        AssertMultipartFile(body, "file", "source.zip");
        Assert.Contains("hello-upload", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UploadItemAsync_Throws_WhenServiceDoesNotAdvertiseUploads() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse(supportsUploads: false);
            }

            throw new InvalidOperationException("HTTP should not be called after metadata lookup.");
        });

        await using var content = new MemoryStream(Encoding.UTF8.GetBytes("hello-upload"));

        var exception = await Assert.ThrowsAsync<FeatureServiceCapabilityException>(() =>
            client.UploadItemAsync(
                new FeatureServiceUploadRequest {
                    Content = content,
                    FileName = "source.zip"
                },
                cancellationToken));

        Assert.Contains("upload", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeleteUploadItemAsync_PostsDeleteRequestAndMapsResult() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestBodies = new List<string>();

        var client = CreateClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;

            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse(supportsUploads: true);
            }

            if (uri.EndsWith(
                "/FeatureServer/uploads/0c6b928f590f49ebac04761bab413e49/delete",
                StringComparison.OrdinalIgnoreCase)) {
                Assert.Equal(HttpMethod.Post, request.Method);

                requestBodies.Add(ReadRequestBody(request));

                return StubHttpMessageHandler.Json("""
                {
                  "success": true
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        var result = await client.DeleteUploadItemAsync(
            "0c6b928f590f49ebac04761bab413e49",
            cancellationToken);

        Assert.Equal("0c6b928f590f49ebac04761bab413e49", result.ItemId);
        Assert.True(result.Success);

        var form = ParseFormBody(Assert.Single(requestBodies));

        Assert.Equal("json", form["f"]);
    }

    [Fact]
    public async Task DeleteUploadItemAsync_ReturnsFalse_WhenServerReportsFailure() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;

            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse(supportsUploads: true);
            }

            if (uri.EndsWith(
                "/FeatureServer/uploads/missing-upload/delete",
                StringComparison.OrdinalIgnoreCase)) {
                return StubHttpMessageHandler.Json("""
                {
                  "success": false
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        var result = await client.DeleteUploadItemAsync(
            "missing-upload",
            cancellationToken);

        Assert.Equal("missing-upload", result.ItemId);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task DeleteUploadItemAsync_Throws_WhenServiceDoesNotAdvertiseUploads() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse(supportsUploads: false);
            }

            throw new InvalidOperationException("HTTP should not be called after metadata lookup.");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceCapabilityException>(() =>
            client.DeleteUploadItemAsync(
                "0c6b928f590f49ebac04761bab413e49",
                cancellationToken));

        Assert.Contains("upload", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeleteUploadItemAsync_Throws_WhenItemIdIsMissing() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            client.DeleteUploadItemAsync(
                " ",
                cancellationToken));

        Assert.Equal("itemId", exception.ParamName);
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

    private static string ReadRequestBody(HttpRequestMessage request) {
        return request.Content?.ReadAsStringAsync().GetAwaiter().GetResult()
            ?? string.Empty;
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

    private static void AssertMultipartField(string body, string name) {
        Assert.True(
            body.Contains($"name=\"{name}\"", StringComparison.Ordinal) ||
            body.Contains($"name={name}", StringComparison.Ordinal),
            $"Expected multipart body to contain a field named '{name}'.");
    }

    private static void AssertMultipartFile(string body, string name, string fileName) {
        var hasName =
            body.Contains($"name=\"{name}\"", StringComparison.Ordinal) ||
            body.Contains($"name={name}", StringComparison.Ordinal);

        var hasFileName =
            body.Contains($"filename=\"{fileName}\"", StringComparison.Ordinal) ||
            body.Contains($"filename={fileName}", StringComparison.Ordinal);

        Assert.True(
            hasName && hasFileName,
            $"Expected multipart body to contain a file field named '{name}' with file name '{fileName}'.");
    }
}