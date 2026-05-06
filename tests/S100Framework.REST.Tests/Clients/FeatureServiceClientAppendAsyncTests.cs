using System.Net.Http;
using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Exceptions;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureServiceClientAppendAsyncTests
{
    [Fact]
    public async Task SubmitAppendAsync_ReturnsJobIdStatusUrlAndItemId_WhenServerReturnsProcessing() {
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
                  "statusMessage": "Job Status for jobId: b62e9db7-507c-443d-3473-8a1f7a7e9fac",
                  "itemId": "cc7ddbc1e33440688d3110c885fa0b30"
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        var submission = await client.SubmitAppendAsync(
            CreateAppendRequest(),
            cancellationToken);

        Assert.Equal("processing", submission.Status);
        Assert.Equal("cc7ddbc1e33440688d3110c885fa0b30", submission.ItemId);
        Assert.Equal("b62e9db7-507c-443d-3473-8a1f7a7e9fac", submission.JobId);
        Assert.NotNull(submission.StatusUrl);

        Assert.Equal(
            "https://example.test/arcgis/rest/services/Test/FeatureServer/append/jobs/b62e9db7-507c-443d-3473-8a1f7a7e9fac?f=json",
            submission.StatusUrl!.AbsoluteUri);

        var requestBody = Assert.Single(requestBodies);
        var decodedRequestBody = Uri.UnescapeDataString(requestBody);

        Assert.Contains("layers=[0]", decodedRequestBody, StringComparison.Ordinal);
        Assert.Contains(@"edits={""layers"":[{""layerDefinition"":{""id"":0},""featureSet"":{""features"":[]}}]}", decodedRequestBody, StringComparison.Ordinal);
        Assert.Contains("upsert=false", decodedRequestBody, StringComparison.Ordinal);
        Assert.Contains("useGlobalIds=false", decodedRequestBody, StringComparison.Ordinal);
        Assert.Contains("rollbackOnFailure=true", decodedRequestBody, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("completed")]
    [InlineData("COMPLETED")]
    [InlineData("Completed")]
    [InlineData("completed_with_errors")]
    [InlineData("Completed With Errors")]
    [InlineData("completed-with-errors")]
    public async Task SubmitAppendAsync_TreatsTerminalSubmissionStatusesCaseAndSeparatorInsensitively(
    string statusValue) {
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
                return StubHttpMessageHandler.Json($$"""
            {
              "status": "{{statusValue}}",
              "editMoment": 1735689600000
            }
            """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        var submission = await client.SubmitAppendAsync(
            CreateAppendRequest(),
            cancellationToken);

        Assert.True(submission.IsTerminal);
        Assert.True(submission.IsCompleted);
        Assert.False(submission.IsPending);
        Assert.Equal(1735689600000, submission.EditMoment);
    }

    [Fact]
    public async Task GetAppendStatusAsync_MapsCompletedStatus_WhenServerReturnsCompletedJob() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;

            if (uri.Contains("/FeatureServer/append/jobs/", StringComparison.OrdinalIgnoreCase)) {
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

        var status = await client.GetAppendStatusAsync(
            new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer/append/jobs/b62e9db7-507c-443d-3473-8a1f7a7e9fac?f=json"),
            cancellationToken);

        Assert.True(status.IsTerminal);
        Assert.True(status.IsCompleted);
        Assert.Equal("CITIES", status.LayerName);
        Assert.Equal(2, status.RecordCount);
        Assert.Equal(1520876908117L, status.SubmissionTime);
        Assert.Equal(1520876913647L, status.LastUpdatedTime);
    }

    [Theory]
    [InlineData("completed")]
    [InlineData("COMPLETED")]
    [InlineData("Completed")]
    [InlineData("completed_with_errors")]
    [InlineData("Completed With Errors")]
    [InlineData("FAILED")]
    [InlineData("completed-with-errors")]
    public async Task GetAppendStatusAsync_TreatsTerminalStatusesCaseAndSeparatorInsensitively(
    string statusValue) {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;

            if (uri.Contains("/FeatureServer/append/jobs/", StringComparison.OrdinalIgnoreCase)) {
                return StubHttpMessageHandler.Json($$"""
            {
              "layerName": "CITIES",
              "recordCount": 2,
              "status": "{{statusValue}}"
            }
            """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        var status = await client.GetAppendStatusAsync(
            new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer/append/jobs/b62e9db7-507c-443d-3473-8a1f7a7e9fac?f=json"),
            cancellationToken);

        Assert.True(status.IsTerminal);
    }

    [Theory]
    [InlineData("completed")]
    [InlineData("COMPLETED")]
    [InlineData("Completed")]
    [InlineData("completed_with_errors")]
    [InlineData("Completed With Errors")]
    [InlineData("completed-with-errors")]
    public async Task GetAppendStatusAsync_TreatsCompletedStatusesCaseAndSeparatorInsensitively(
        string statusValue) {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;

            if (uri.Contains("/FeatureServer/append/jobs/", StringComparison.OrdinalIgnoreCase)) {
                return StubHttpMessageHandler.Json($$"""
            {
              "layerName": "CITIES",
              "recordCount": 2,
              "status": "{{statusValue}}"
            }
            """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        var status = await client.GetAppendStatusAsync(
            new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer/append/jobs/b62e9db7-507c-443d-3473-8a1f7a7e9fac?f=json"),
            cancellationToken);

        Assert.True(status.IsCompleted);
    }

    [Fact]
    public async Task WaitForAppendCompletionAsync_PollsUntilJobBecomesCompleted() {
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
            CreateAppendRequest(),
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
    public async Task SubmitAppendAsync_Throws_WhenServiceDoesNotAdvertiseAppendSupport() {
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
                CreateAppendRequest(),
                cancellationToken));

        Assert.Contains("append", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single(requestUris);
    }

    [Fact]
    public async Task SubmitAppendAsync_Throws_WhenUpsertIsUsedAndSyncIsEnabled() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<string>();

        var client = CreateClient(request => {
            requestUris.Add(request.RequestUri!.AbsoluteUri);

            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse(
                    supportsAppend: true,
                    syncEnabled: true,
                    supportsChangeTracking: false);
            }

            throw new InvalidOperationException("HTTP should not be called after metadata lookup.");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceCapabilityException>(() =>
            client.SubmitAppendAsync(
                CreateAppendRequest() with {
                    Upsert = true
                },
                cancellationToken));

        Assert.Contains("upsert", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sync", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single(requestUris);
    }

    [Theory]
    [InlineData("failed", true, false, false)]
    [InlineData("FAILED", true, false, false)]
    [InlineData("error", true, false, false)]
    [InlineData("cancelled", false, true, false)]
    [InlineData("canceled", false, true, false)]
    [InlineData("esriJobCancelled", false, true, false)]
    [InlineData("esriJobCanceled", false, true, false)]
    [InlineData("timed-out", false, false, true)]
    [InlineData("timeout", false, false, true)]
    [InlineData("esriJobTimedOut", false, false, true)]
    public async Task SubmitAppendAsync_TreatsFailedCancelledAndTimedOutStatusesAsTerminalButNotCompleted(
    string statusValue,
    bool isFailed,
    bool isCancelled,
    bool isTimedOut) {
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
                return StubHttpMessageHandler.Json($$"""
            {
              "status": "{{statusValue}}",
              "editMoment": 1735689600000
            }
            """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        var submission = await client.SubmitAppendAsync(
            CreateAppendRequest(),
            cancellationToken);

        Assert.True(submission.IsTerminal);
        Assert.False(submission.IsCompleted);
        Assert.False(submission.IsPending);
        Assert.Equal(isFailed, submission.IsFailed);
        Assert.Equal(isCancelled, submission.IsCancelled);
        Assert.Equal(isTimedOut, submission.IsTimedOut);
    }

    [Theory]
    [InlineData("failed", true, false, false)]
    [InlineData("FAILED", true, false, false)]
    [InlineData("error", true, false, false)]
    [InlineData("cancelled", false, true, false)]
    [InlineData("canceled", false, true, false)]
    [InlineData("esriJobCancelled", false, true, false)]
    [InlineData("esriJobCanceled", false, true, false)]
    [InlineData("timed-out", false, false, true)]
    [InlineData("timeout", false, false, true)]
    [InlineData("esriJobTimedOut", false, false, true)]
    public async Task GetAppendStatusAsync_TreatsFailedCancelledAndTimedOutStatusesAsTerminalButNotCompleted(
    string statusValue,
    bool isFailed,
    bool isCancelled,
    bool isTimedOut) {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;

            if (uri.Contains("/FeatureServer/append/jobs/", StringComparison.OrdinalIgnoreCase)) {
                return StubHttpMessageHandler.Json($$"""
            {
              "layerName": "CITIES",
              "recordCount": 2,
              "status": "{{statusValue}}"
            }
            """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        var status = await client.GetAppendStatusAsync(
            new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer/append/jobs/b62e9db7-507c-443d-3473-8a1f7a7e9fac?f=json"),
            cancellationToken);

        Assert.True(status.IsTerminal);
        Assert.False(status.IsCompleted);
        Assert.Equal(isFailed, status.IsFailed);
        Assert.Equal(isCancelled, status.IsCancelled);
        Assert.Equal(isTimedOut, status.IsTimedOut);
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