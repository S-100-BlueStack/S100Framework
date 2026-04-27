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