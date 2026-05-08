using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Exceptions;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureLayerClientQueryAnalyticAsyncTests
{
    [Fact]
    public async Task SubmitQueryAnalyticAsync_SendsAsyncParameterAndReturnsStatusUrl() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<Uri>();

        var client = CreateClient(request => {
            requestUris.Add(request.RequestUri!);

            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse(supportsAsyncQueryAnalytic: true);
            }

            if (request.RequestUri!.AbsolutePath.EndsWith(
                "/FeatureServer/0/queryAnalytic",
                StringComparison.OrdinalIgnoreCase)) {
                return StubHttpMessageHandler.Json("""
                {
                  "statusUrl": "https://example.test/jobs/queryAnalytic/abc"
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var layerClient = client.GetLayerClient(0);

        var submission = await layerClient.SubmitQueryAnalyticAsync(
            CreateRequest(),
            cancellationToken);

        Assert.Null(submission.Result);
        Assert.Equal(
            new Uri("https://example.test/jobs/queryAnalytic/abc"),
            submission.StatusUrl);
        Assert.True(submission.IsPending);

        var queryAnalyticRequest = Assert.Single(
            requestUris,
            uri => uri.AbsolutePath.EndsWith(
                "/FeatureServer/0/queryAnalytic",
                StringComparison.OrdinalIgnoreCase));

        var query = ParseQuery(queryAnalyticRequest);

        Assert.Equal("true", query["async"]);
        Assert.Equal("json", query["dataFormat"]);
        Assert.Equal("json", query["f"]);
    }

    [Fact]
    public async Task WaitForQueryAnalyticCompletionAsync_PollsStatusAndMapsResult() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var statusCalls = 0;

        var client = CreateClient(request => {
            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse(supportsAsyncQueryAnalytic: true);
            }

            if (request.RequestUri!.AbsolutePath.EndsWith(
                "/FeatureServer/0/queryAnalytic",
                StringComparison.OrdinalIgnoreCase)) {
                return StubHttpMessageHandler.Json("""
                {
                  "statusUrl": "https://example.test/jobs/queryAnalytic/abc"
                }
                """);
            }

            if (request.RequestUri!.AbsoluteUri == "https://example.test/jobs/queryAnalytic/abc") {
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
                        "resultUrl": "https://example.test/jobs/queryAnalytic/abc/result",
                        "submissionTime": 1000,
                        "lastUpdatedTime": 3000
                      }
                      """);
            }

            if (request.RequestUri!.AbsoluteUri == "https://example.test/jobs/queryAnalytic/abc/result") {
                return StubHttpMessageHandler.Json("""
                {
                  "objectIdFieldName": "OBJECTID",
                  "features": [
                    {
                      "attributes": {
                        "OBJECTID": 10,
                        "STATE_NAME": "Texas",
                        "RunningTotal": 1234.5
                      }
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var layerClient = client.GetLayerClient(0);

        var result = await layerClient.WaitForQueryAnalyticCompletionAsync(
            CreateRequest(),
            new QueryAnalyticWaitOptions {
                PollInterval = TimeSpan.Zero,
                Timeout = TimeSpan.FromSeconds(5)
            },
            cancellationToken);

        Assert.Equal(2, statusCalls);

        var row = Assert.Single(result.Rows);
        Assert.Equal(10, row.ObjectId);
        Assert.Equal("Texas", row.Attributes["STATE_NAME"]);
        Assert.Equal(1234.5m, row.Attributes["RunningTotal"]);
    }

    [Fact]
    public async Task GetQueryAnalyticStatusAsync_MapsCompletedStatus() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (request.RequestUri!.AbsoluteUri == "https://example.test/jobs/queryAnalytic/abc") {
                return StubHttpMessageHandler.Json("""
                {
                  "status": "Completed With Errors",
                  "resultUrl": "https://example.test/jobs/queryAnalytic/abc/result",
                  "submissionTime": "1000",
                  "lastUpdatedTime": "3000"
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var status = await client.GetLayerClient(0).GetQueryAnalyticStatusAsync(
            new Uri("https://example.test/jobs/queryAnalytic/abc"),
            cancellationToken);

        Assert.True(status.IsTerminal);
        Assert.True(status.IsCompleted);
        Assert.Equal(
            new Uri("https://example.test/jobs/queryAnalytic/abc/result"),
            status.ResultUrl);
        Assert.Equal(1000, status.SubmissionTime);
        Assert.Equal(3000, status.LastUpdatedTime);
    }

    [Fact]
    public async Task SubmitQueryAnalyticAsync_Throws_WhenLayerDoesNotAdvertiseAsyncSupport() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse(supportsAsyncQueryAnalytic: false);
            }

            throw new InvalidOperationException("HTTP should not be called after schema lookup.");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceCapabilityException>(() =>
            client.GetLayerClient(0).SubmitQueryAnalyticAsync(
                CreateRequest(),
                cancellationToken));

        Assert.Contains("queryAnalytic", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetSchemaAsync_MapsAsyncQueryAnalyticCapability() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse(supportsAsyncQueryAnalytic: true);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var schema = await client.GetLayerClient(0).GetSchemaAsync(cancellationToken);

        Assert.True(schema.Capabilities.SupportsAsyncQueryAnalytic);
    }

    private static QueryAnalyticRequest CreateRequest() {
        return new QueryAnalyticRequest {
            Where = "POP1990 > 0",
            OutAnalyticsJson = """
            [
              {
                "analyticType": "SUM",
                "onAnalyticField": "POP1990",
                "outAnalyticFieldName": "RunningTotal",
                "analyticParameters": {
                  "partitionBy": "STATE_NAME",
                  "orderBy": "POP1990 ASC"
                }
              }
            ]
            """,
            OutFields = ["OBJECTID", "STATE_NAME"],
            ReturnGeometry = false
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

    private static HttpResponseMessage CreateLayerMetadataResponse(bool supportsAsyncQueryAnalytic) {
        return StubHttpMessageHandler.Json($$"""
        {
          "id": 0,
          "name": "Layer 0",
          "geometryType": "esriGeometryPoint",
          "objectIdField": "OBJECTID",
          "maxRecordCount": 1000,
          "advancedQueryCapabilities": {
            "supportsPagination": true,
            "supportsQueryAnalytic": true
          },
          "advancedQueryAnalyticCapabilities": {
            "supportsAsync": {{supportsAsyncQueryAnalytic.ToString().ToLowerInvariant()}}
          },
          "fields": [
            { "name": "OBJECTID", "type": "esriFieldTypeOID", "nullable": false },
            { "name": "STATE_NAME", "type": "esriFieldTypeString", "nullable": true },
            { "name": "POP1990", "type": "esriFieldTypeInteger", "nullable": true }
          ],
          "relationships": [],
          "extent": {
            "spatialReference": { "wkid": 4326, "latestWkid": 4326 }
          }
        }
        """);
    }

    private static Dictionary<string, string> ParseQuery(Uri uri) {
        return uri.Query
            .TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .ToDictionary(
                parts => Uri.UnescapeDataString(parts[0]),
                parts => parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty,
                StringComparer.Ordinal);
    }

    [Theory]
    [InlineData("Completed")]
    [InlineData("CompletedWithErrors")]
    [InlineData("Completed With Errors")]
    [InlineData("Succeeded")]
    [InlineData("esriJobSucceeded")]
    public void QueryAnalyticJobStatus_IsCompleted_ForSuccessfulStatuses(string status) {
        var jobStatus = new QueryAnalyticJobStatus(
            status,
            ResultUrl: new Uri("https://example.test/jobs/queryAnalytic/abc/result"),
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
    public void QueryAnalyticJobStatus_IsFailed_ForFailureStatuses(string status) {
        var jobStatus = new QueryAnalyticJobStatus(
            status,
            ResultUrl: null,
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
    public void QueryAnalyticJobStatus_IsCancelled_ForCancelledStatuses(string status) {
        var jobStatus = new QueryAnalyticJobStatus(
            status,
            ResultUrl: null,
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
    public void QueryAnalyticJobStatus_IsTimedOut_ForTimedOutStatuses(string status) {
        var jobStatus = new QueryAnalyticJobStatus(
            status,
            ResultUrl: null,
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
    public void QueryAnalyticJobStatus_IsRunning_ForNonTerminalStatuses(string status) {
        var jobStatus = new QueryAnalyticJobStatus(
            status,
            ResultUrl: null,
            SubmissionTime: null,
            LastUpdatedTime: null);

        Assert.True(jobStatus.IsRunning);
        Assert.False(jobStatus.IsTerminal);
        Assert.False(jobStatus.IsCompleted);
    }

    [Fact]
    public async Task GetQueryAnalyticStatusAsync_MapsEsriJobStatusProperty() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (request.RequestUri!.AbsoluteUri == "https://example.test/jobs/queryAnalytic/abc") {
                return StubHttpMessageHandler.Json("""
            {
              "jobStatus": "esriJobSucceeded",
              "resultUrl": "https://example.test/jobs/queryAnalytic/abc/result",
              "submissionTime": "1000",
              "lastUpdatedTime": "3000"
            }
            """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var status = await client.GetLayerClient(0).GetQueryAnalyticStatusAsync(
            new Uri("https://example.test/jobs/queryAnalytic/abc"),
            cancellationToken);

        Assert.True(status.IsTerminal);
        Assert.True(status.IsCompleted);
        Assert.False(status.IsFailed);
        Assert.Equal(
            new Uri("https://example.test/jobs/queryAnalytic/abc/result"),
            status.ResultUrl);
        Assert.Equal(1000, status.SubmissionTime);
        Assert.Equal(3000, status.LastUpdatedTime);
    }
}