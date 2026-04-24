using System.Net.Http;
using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Extensions;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Extensions;

public sealed class FeatureServiceClientAppendExtensionsTests
{
    [Fact]
    public async Task SubmitAndWaitForAppendAsync_ReturnsCompletedStatus_WhenJobCompletes() {
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
                  "statusMessage": "Job Status for jobId: b62e9db7-507c-443d-3473-8a1f7a7e9fac",
                  "itemId": "cc7ddbc1e33440688d3110c885fa0b30"
                }
                """);
            }

            if (uri.Contains("/FeatureServer/append/jobs/b62e9db7-507c-443d-3473-8a1f7a7e9fac", StringComparison.OrdinalIgnoreCase)) {
                statusRequestCount++;

                return StubHttpMessageHandler.Json("""
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

        var status = await FeatureServiceClientAppendExtensions.SubmitAndWaitForAppendAsync(
            client,
            CreateAppendRequest(),
            new AppendWaitOptions {
                PollInterval = TimeSpan.FromMilliseconds(1),
                Timeout = TimeSpan.FromSeconds(5)
            },
            cancellationToken);

        Assert.True(status.IsCompleted);
        Assert.True(status.IsTerminal);
        Assert.Equal("CITIES", status.LayerName);
        Assert.Equal(2, status.RecordCount);
        Assert.Equal(1520876908117L, status.SubmissionTime);
        Assert.Equal(1520876913647L, status.LastUpdatedTime);
        Assert.Equal(1, statusRequestCount);
    }

    [Fact]
    public async Task SubmitAndWaitForAppendAsync_Throws_WhenJobEndsFailed() {
        var cancellationToken = TestContext.Current.CancellationToken;

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
                return StubHttpMessageHandler.Json("""
                {
                  "layerName": "CITIES",
                  "submissionTime": 1520876908117,
                  "lastUpdatedTime": 1520876913647,
                  "recordCount": 2,
                  "status": "Failed"
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            FeatureServiceClientAppendExtensions.SubmitAndWaitForAppendAsync(
                client,
                CreateAppendRequest(),
                new AppendWaitOptions {
                    PollInterval = TimeSpan.FromMilliseconds(1),
                    Timeout = TimeSpan.FromSeconds(5)
                },
                cancellationToken));

        Assert.Contains("append job ended", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Failed", exception.Message, StringComparison.Ordinal);
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

    private static FeatureServiceAppendEditsRequest CreateAppendRequest() {
        return new FeatureServiceAppendEditsRequest {
            Layers = [0],
            EditsJson = """
            {"layers":[{"layerDefinition":{"id":0},"featureSet":{"features":[]}}]}
            """,
            RollbackOnFailure = true
        };
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
            "feature Collection",
            "sqlite"
          ]
        }
        """);
    }
}