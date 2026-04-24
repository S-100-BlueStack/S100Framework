using System.Text.Json;
using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Exceptions;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureServiceClientAppendItemTests
{
    [Fact]
    public async Task SubmitAppendAsync_ItemRequest_IncludesAppendItemFormatAndLayerMappings() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestBodies = new List<string>();

        var client = CreateClient(request =>
        {
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
              "statusMessage": "Job Status for jobId: b62e9db7-507c-443d-3473-8a1f7a7e9fac",
              "itemId": "cc7ddbc1e33440688d3110c885fa0b30"
            }
            """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        var submission = await client.SubmitAppendAsync(
            new FeatureServiceAppendItemRequest {
                Layers = [0],
                AppendItemId = "894d8c12438v4baaac164b636f7e1e2f",
                AppendUploadFormat = FeatureServiceAppendSourceFormat.FileGeodatabase,
                LayerMappings =
                [
                    new FeatureServiceAppendLayerMapping
                {
                    Id = 0,
                    SourceTableName = "Countries",
                    FieldMappings =
                    [
                        new FeatureServiceAppendFieldMapping
                        {
                            Name = "NAME",
                            Source = "Country"
                        }
                    ]
                }
                ],
                RollbackOnFailure = true
            },
            cancellationToken);

        Assert.Equal("processing", submission.Status);
        Assert.Equal("b62e9db7-507c-443d-3473-8a1f7a7e9fac", submission.JobId);
        Assert.NotNull(submission.StatusUrl);

        var body = Assert.Single(requestBodies);
        var form = ParseFormBody(body);

        Assert.Equal("json", form["f"]);
        Assert.Equal("[0]", form["layers"]);
        Assert.Equal("894d8c12438v4baaac164b636f7e1e2f", form["appendItemId"]);
        Assert.Equal("filegdb", form["appendUploadFormat"]);
        Assert.Equal("false", form["upsert"]);
        Assert.Equal("false", form["useGlobalIds"]);
        Assert.Equal("true", form["rollbackOnFailure"]);

        using var document = JsonDocument.Parse(form["layerMappings"]);
        var mappings = document.RootElement;

        Assert.Equal(JsonValueKind.Array, mappings.ValueKind);
        Assert.Equal(1, mappings.GetArrayLength());

        var mapping = mappings[0];

        Assert.Equal(0, mapping.GetProperty("id").GetInt32());
        Assert.Equal("Countries", mapping.GetProperty("sourceTableName").GetString());

        var fieldMappings = mapping.GetProperty("fieldMappings");

        Assert.Equal(JsonValueKind.Array, fieldMappings.ValueKind);
        Assert.Equal(1, fieldMappings.GetArrayLength());
        Assert.Equal("NAME", fieldMappings[0].GetProperty("name").GetString());
        Assert.Equal("Country", fieldMappings[0].GetProperty("source").GetString());
    }

    [Fact]
    public async Task WaitForAppendCompletionAsync_ItemRequest_PollsUntilJobCompletes() {
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
                  "statusMessage": "Job Status for jobId: feeahh1e-e32c-45bf-680c-f4ed70569081",
                  "itemId": "aa7gdww1e55200527d3110c634fa0b30"
                }
                """);
            }

            if (uri.Contains("/FeatureServer/append/jobs/feeahh1e-e32c-45bf-680c-f4ed70569081", StringComparison.OrdinalIgnoreCase)) {
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
            new FeatureServiceAppendItemRequest {
                Layers = [0],
                AppendItemId = "hosted-item-1",
                AppendUploadFormat = FeatureServiceAppendSourceFormat.FeatureService,
                LayerMappings =
                [
                    new FeatureServiceAppendLayerMapping
                    {
                        Id = 0,
                        SourceId = 3
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
    public async Task SubmitAppendAsync_ItemRequest_Throws_WhenServiceDoesNotAdvertiseAppendSupport() {
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
                new FeatureServiceAppendItemRequest {
                    Layers = [0],
                    AppendItemId = "portal-item-1"
                },
                cancellationToken));

        Assert.Contains("append", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single(requestUris);
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
            "feature Service"
          ]
        }
        """);
    }
}