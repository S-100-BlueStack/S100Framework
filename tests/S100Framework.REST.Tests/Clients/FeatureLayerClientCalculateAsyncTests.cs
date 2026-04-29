using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Exceptions;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureLayerClientCalculateAsyncTests
{
    [Fact]
    public async Task SubmitCalculateAsync_SendsAsyncParameterAndReturnsStatusUrl() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestBodies = new List<string>();

        var client = CreateClient(request => {
            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse(
                    supportsCalculate: true,
                    supportsAsyncCalculate: true);
            }

            if (request.RequestUri!.AbsolutePath.EndsWith(
                "/FeatureServer/0/calculate",
                StringComparison.OrdinalIgnoreCase)) {
                Assert.Equal(HttpMethod.Post, request.Method);

                requestBodies.Add(ReadRequestBody(request));

                return StubHttpMessageHandler.Json("""
                {
                  "statusUrl": "https://example.test/jobs/calculate/abc"
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var submission = await client.GetLayerClient(0).SubmitCalculateAsync(
            CreateRequest(),
            cancellationToken);

        Assert.Null(submission.Result);
        Assert.Equal(
            new Uri("https://example.test/jobs/calculate/abc"),
            submission.StatusUrl);
        Assert.True(submission.IsPending);

        var form = ParseFormBody(Assert.Single(requestBodies));

        Assert.Equal("json", form["f"]);
        Assert.Equal("true", form["async"]);
        Assert.Equal("STATUS = 'Pending'", form["where"]);
    }

    [Fact]
    public async Task SubmitCalculateAsync_Throws_WhenLayerDoesNotAdvertiseAsyncCalculateSupport() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse(
                    supportsCalculate: true,
                    supportsAsyncCalculate: false);
            }

            throw new InvalidOperationException("HTTP should not be called after schema lookup.");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceCapabilityException>(() =>
            client.GetLayerClient(0).SubmitCalculateAsync(
                CreateRequest(),
                cancellationToken));

        Assert.Contains("calculate", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WaitForCalculateCompletionAsync_PollsStatusAndMapsResult() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var statusCalls = 0;

        var client = CreateClient(request => {
            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse(
                    supportsCalculate: true,
                    supportsAsyncCalculate: true);
            }

            if (request.RequestUri!.AbsolutePath.EndsWith(
                "/FeatureServer/0/calculate",
                StringComparison.OrdinalIgnoreCase)) {
                return StubHttpMessageHandler.Json("""
                {
                  "statusUrl": "https://example.test/jobs/calculate/abc"
                }
                """);
            }

            if (request.RequestUri!.AbsoluteUri == "https://example.test/jobs/calculate/abc") {
                statusCalls++;

                return StubHttpMessageHandler.Json(statusCalls == 1
                    ? """
                      {
                        "status": "InProgress",
                        "submissionTime": 1000,
                        "lastUpdatedTime": 2000
                      }
                      """
                    : """
                      {
                        "status": "Completed",
                        "submissionTime": 1000,
                        "lastUpdatedTime": 3000,
                        "recordCount": 3140
                      }
                      """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var result = await client.GetLayerClient(0).WaitForCalculateCompletionAsync(
            CreateRequest(),
            new CalculateWaitOptions {
                PollInterval = TimeSpan.Zero,
                Timeout = TimeSpan.FromSeconds(5)
            },
            cancellationToken);

        Assert.Equal(2, statusCalls);
        Assert.True(result.Success);
        Assert.Equal(3140, result.UpdatedFeatureCount);
        Assert.Null(result.EditMoment);
    }

    [Fact]
    public async Task GetCalculateStatusAsync_MapsEsriJobStatusProperty() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (request.RequestUri!.AbsoluteUri == "https://example.test/jobs/calculate/abc") {
                return StubHttpMessageHandler.Json("""
                {
                  "jobStatus": "esriJobSucceeded",
                  "submissionTime": "1000",
                  "lastUpdatedTime": "3000",
                  "recordCount": "3140"
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var status = await client.GetLayerClient(0).GetCalculateStatusAsync(
            new Uri("https://example.test/jobs/calculate/abc"),
            cancellationToken);

        Assert.True(status.IsTerminal);
        Assert.True(status.IsCompleted);
        Assert.False(status.IsFailed);
        Assert.Equal(1000, status.SubmissionTime);
        Assert.Equal(3000, status.LastUpdatedTime);
        Assert.Equal(3140, status.RecordCount);
    }

    [Fact]
    public async Task GetSchemaAsync_MapsAsyncCalculateCapability() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse(
                    supportsCalculate: true,
                    supportsAsyncCalculate: true);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var schema = await client.GetLayerClient(0).GetSchemaAsync(cancellationToken);

        Assert.True(schema.Capabilities.SupportsCalculate);
        Assert.True(schema.Capabilities.SupportsAsyncCalculate);
    }

    [Theory]
    [InlineData("Completed")]
    [InlineData("CompletedWithErrors")]
    [InlineData("Completed With Errors")]
    [InlineData("Succeeded")]
    [InlineData("esriJobSucceeded")]
    public void CalculateJobStatus_IsCompleted_ForSuccessfulStatuses(string status) {
        var jobStatus = new CalculateJobStatus(
            status,
            RecordCount: 1,
            SubmissionTime: null,
            LastUpdatedTime: null);

        Assert.True(jobStatus.IsCompleted);
        Assert.True(jobStatus.IsTerminal);
        Assert.False(jobStatus.IsFailed);
        Assert.False(jobStatus.IsCancelled);
        Assert.False(jobStatus.IsTimedOut);
    }

    [Theory]
    [InlineData("Failed")]
    [InlineData("Error")]
    [InlineData("esriJobFailed")]
    public void CalculateJobStatus_IsFailed_ForFailureStatuses(string status) {
        var jobStatus = new CalculateJobStatus(
            status,
            RecordCount: null,
            SubmissionTime: null,
            LastUpdatedTime: null);

        Assert.True(jobStatus.IsFailed);
        Assert.True(jobStatus.IsTerminal);
        Assert.False(jobStatus.IsCompleted);
    }

    [Theory]
    [InlineData("Cancelled")]
    [InlineData("Canceled")]
    [InlineData("esriJobCancelled")]
    [InlineData("esriJobCanceled")]
    public void CalculateJobStatus_IsCancelled_ForCancelledStatuses(string status) {
        var jobStatus = new CalculateJobStatus(
            status,
            RecordCount: null,
            SubmissionTime: null,
            LastUpdatedTime: null);

        Assert.True(jobStatus.IsCancelled);
        Assert.True(jobStatus.IsTerminal);
        Assert.False(jobStatus.IsCompleted);
    }

    [Theory]
    [InlineData("TimedOut")]
    [InlineData("Timed Out")]
    [InlineData("Timeout")]
    [InlineData("esriJobTimedOut")]
    public void CalculateJobStatus_IsTimedOut_ForTimedOutStatuses(string status) {
        var jobStatus = new CalculateJobStatus(
            status,
            RecordCount: null,
            SubmissionTime: null,
            LastUpdatedTime: null);

        Assert.True(jobStatus.IsTimedOut);
        Assert.True(jobStatus.IsTerminal);
        Assert.False(jobStatus.IsCompleted);
    }

    [Theory]
    [InlineData("Submitted")]
    [InlineData("Waiting")]
    [InlineData("Executing")]
    [InlineData("InProgress")]
    [InlineData("Processing")]
    [InlineData("Running")]
    [InlineData("Cancelling")]
    [InlineData("esriJobSubmitted")]
    [InlineData("esriJobWaiting")]
    [InlineData("esriJobExecuting")]
    [InlineData("esriJobCancelling")]
    public void CalculateJobStatus_IsRunning_ForNonTerminalStatuses(string status) {
        var jobStatus = new CalculateJobStatus(
            status,
            RecordCount: null,
            SubmissionTime: null,
            LastUpdatedTime: null);

        Assert.True(jobStatus.IsRunning);
        Assert.False(jobStatus.IsTerminal);
        Assert.False(jobStatus.IsCompleted);
    }

    private static CalculateRequest CreateRequest() {
        return new CalculateRequest {
            Where = "STATUS = 'Pending'",
            Expressions = [
                CalculateExpression.ForValue("STATUS", "Reviewed")
            ]
        };
    }

    private static FeatureServiceClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> handler) {
        return new FeatureServiceClient(
            new HttpClient(new StubHttpMessageHandler(handler)),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });
    }

    private static bool IsLayerMetadataRequest(HttpRequestMessage request) {
        return request.RequestUri?.AbsolutePath.EndsWith(
            "/FeatureServer/0",
            StringComparison.OrdinalIgnoreCase) == true;
    }

    private static HttpResponseMessage CreateLayerMetadataResponse(
        bool supportsCalculate,
        bool supportsAsyncCalculate) {
        return StubHttpMessageHandler.Json($$"""
        {
          "id": 0,
          "name": "Layer 0",
          "geometryType": "esriGeometryPoint",
          "objectIdField": "OBJECTID",
          "supportsCalculate": {{supportsCalculate.ToString().ToLowerInvariant()}},
          "supportsAsyncCalculate": {{supportsAsyncCalculate.ToString().ToLowerInvariant()}},
          "maxRecordCount": 1000,
          "advancedQueryCapabilities": {
            "supportsPagination": true
          },
          "fields": [
            { "name": "OBJECTID", "type": "esriFieldTypeOID", "nullable": false },
            { "name": "STATUS", "type": "esriFieldTypeString", "nullable": true }
          ],
          "relationships": [],
          "extent": {
            "spatialReference": { "wkid": 4326, "latestWkid": 4326 }
          }
        }
        """);
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
                parts => Uri.UnescapeDataString(parts[0].Replace("+", " ", StringComparison.Ordinal)),
                parts => parts.Length > 1
                    ? Uri.UnescapeDataString(parts[1].Replace("+", " ", StringComparison.Ordinal))
                    : string.Empty,
                StringComparer.Ordinal);
    }
}