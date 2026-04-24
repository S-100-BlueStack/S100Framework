using System.Text.Json;
using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Exceptions;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureServiceClientAppendUploadTests
{
    [Fact]
    public async Task SubmitAppendAsync_UploadRequest_IncludesUploadIdFormatAndLayerMappings() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestBodies = new List<string>();

        var client = CreateClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;

            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse(
                    supportsAppend: true,
                    syncEnabled: false,
                    supportsChangeTracking: false);
            }

            if (uri.EndsWith("/FeatureServer/append", StringComparison.OrdinalIgnoreCase)) {
                requestBodies.Add(ReadRequestBody(request));

                return StubHttpMessageHandler.Json("""
                {
                  "status": "processing",
                  "statusMessage": "Job Status for jobId: 5db2f302-6b0d-4bd9-8e52-0fd7b0a3f2d1",
                  "itemId": "temp-upload-result-item"
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        var submission = await client.SubmitAppendAsync(
            new FeatureServiceAppendUploadRequest {
                Layers = [0],
                AppendUploadId = "0c6b928f590f49ebac04761bab413e49",
                AppendUploadFormat = FeatureServiceAppendSourceFormat.FileGeodatabase,
                LayerMappings =
                [
                    new FeatureServiceAppendLayerMapping
                    {
                        Id = 0,
                        SourceTableName = "USA"
                    }
                ],
                RollbackOnFailure = true
            },
            cancellationToken);

        Assert.Equal("processing", submission.Status);
        Assert.Equal("5db2f302-6b0d-4bd9-8e52-0fd7b0a3f2d1", submission.JobId);
        Assert.NotNull(submission.StatusUrl);

        var body = Assert.Single(requestBodies);
        var form = ParseFormBody(body);

        Assert.Equal("json", form["f"]);
        Assert.Equal("[0]", form["layers"]);
        Assert.Equal("0c6b928f590f49ebac04761bab413e49", form["appendUploadId"]);
        Assert.Equal("filegdb", form["appendUploadFormat"]);
        Assert.Equal("false", form["upsert"]);
        Assert.Equal("false", form["useGlobalIds"]);
        Assert.Equal("true", form["rollbackOnFailure"]);

        using var document = JsonDocument.Parse(form["layerMappings"]);
        var mappings = document.RootElement;

        Assert.Equal(JsonValueKind.Array, mappings.ValueKind);
        Assert.Equal(1, mappings.GetArrayLength());
        Assert.Equal("USA", mappings[0].GetProperty("sourceTableName").GetString());
    }

    [Fact]
    public async Task WaitForAppendCompletionAsync_UploadRequest_PollsUntilJobCompletes() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var statusRequestCount = 0;

        var client = CreateClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;

            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse(
                    supportsAppend: true,
                    syncEnabled: false,
                    supportsChangeTracking: false);
            }

            if (uri.EndsWith("/FeatureServer/append", StringComparison.OrdinalIgnoreCase)) {
                return StubHttpMessageHandler.Json("""
                {
                  "status": "processing",
                  "statusMessage": "Job Status for jobId: 77df1305-3ad3-41e5-b57f-0f09034db6fb",
                  "itemId": "temp-upload-result-item"
                }
                """);
            }

            if (uri.Contains("/FeatureServer/append/jobs/77df1305-3ad3-41e5-b57f-0f09034db6fb", StringComparison.OrdinalIgnoreCase)) {
                statusRequestCount++;

                return statusRequestCount == 1
                    ? StubHttpMessageHandler.Json("""
                    {
                      "layerName": "CITIES",
                      "submissionTime": 1520876908117,
                      "lastUpdatedTime": 1520876909000,
                      "recordCount": 2,
                      "status": "Processing"
                    }
                    """)
                    : StubHttpMessageHandler.Json("""
                    {
                      "layerName": "CITIES",
                      "submissionTime": 1520876908117,
                      "lastUpdatedTime": 1520876913647,
                      "recordCount": 2,
                      "status": "Completed"
                    }
                    """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        var status = await client.WaitForAppendCompletionAsync(
            new FeatureServiceAppendUploadRequest {
                Layers = [0],
                AppendUploadId = "0c6b928f590f49ebac04761bab413e49",
                AppendUploadFormat = FeatureServiceAppendSourceFormat.FileGeodatabase,
                LayerMappings =
                [
                    new FeatureServiceAppendLayerMapping
                    {
                        Id = 0,
                        SourceTableName = "USA"
                    }
                ]
            },
            new AppendWaitOptions {
                PollInterval = TimeSpan.FromMilliseconds(1),
                Timeout = TimeSpan.FromSeconds(5)
            },
            cancellationToken);

        Assert.True(status.IsCompleted);
        Assert.Equal("CITIES", status.LayerName);
        Assert.Equal(2, status.RecordCount);
        Assert.True(statusRequestCount >= 2);
    }

    [Fact]
    public async Task SubmitAppendAsync_UploadRequest_Throws_WhenServiceDoesNotAdvertiseAppendSupport() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<string>();

        var client = CreateClient(request => {
            requestUris.Add(request.RequestUri!.AbsoluteUri);

            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse(
                    supportsAppend: false,
                    syncEnabled: false,
                    supportsChangeTracking: false);
            }

            throw new InvalidOperationException("HTTP should not be called after metadata lookup.");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceCapabilityException>(() =>
            client.SubmitAppendAsync(
                new FeatureServiceAppendUploadRequest {
                    Layers = [0],
                    AppendUploadId = "0c6b928f590f49ebac04761bab413e49",
                    AppendUploadFormat = FeatureServiceAppendSourceFormat.FileGeodatabase
                },
                cancellationToken));

        Assert.Contains("append", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single(requestUris);
    }

    [Fact]
    public async Task SubmitAppendAsync_UploadRequest_Throws_WhenFormatIsMissing() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(_ => throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.SubmitAppendAsync(
                new FeatureServiceAppendUploadRequest {
                    Layers = [0],
                    AppendUploadId = "0c6b928f590f49ebac04761bab413e49"
                },
                cancellationToken));

        Assert.Contains("AppendUploadFormat", exception.Message, StringComparison.OrdinalIgnoreCase);
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

    private static Dictionary<string, string> ParseFormBody(string body) {
        return body
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .ToDictionary(
                parts => Uri.UnescapeDataString(parts[0]),
                parts => parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty,
                StringComparer.Ordinal);
    }

    private static HttpResponseMessage CreateServiceMetadataResponse(
        bool supportsAppend,
        bool syncEnabled,
        bool supportsChangeTracking) {
        var capabilities = supportsChangeTracking
            ? "Query,ChangeTracking"
            : "Query";

        return StubHttpMessageHandler.Json($$"""
        {
          "layers": [
            { "id": 0, "name": "Layer 0" }
          ],
          "tables": [],
          "capabilities": "{{capabilities}}",
          "maxRecordCount": 1000,
          "syncEnabled": {{syncEnabled.ToString().ToLowerInvariant()}},
          "supportsAppend": {{supportsAppend.ToString().ToLowerInvariant()}},
          "supportedAppendFormats": [
            "filegdb",
            "sqlite",
            "pbf"
          ]
        }
        """);
    }
}