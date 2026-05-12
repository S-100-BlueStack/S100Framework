using System.Net;
using System.Net.Http.Headers;
using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Extensions;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Extensions;

public sealed class FeatureServiceClientSynchronizeReplicaExtensionsTests
{
    [Fact]
    public async Task WaitForSynchronizeReplicaCompletionAsync_ReturnsCompletedStatus_WhenStatusContainsSpaces() {
        var cancellationToken = TestContext.Current.CancellationToken;
        const string statusUrl = "https://example.test/arcgis/rest/services/Test/FeatureServer/jobs/synchronizeReplica-123/status";
        const string resultUrl = "https://example.test/arcgis/rest/directories/arcgisoutput/Test_MapServer/synchronizeReplica-123.sqlite";

        var handler = new StubHttpMessageHandler(request => {
            var uri = request.RequestUri!.AbsoluteUri;

            if (uri == statusUrl) {
                return StubHttpMessageHandler.Json($$"""
                {
                  "status": "Completed With Errors",
                  "resultUrl": "{{resultUrl}}",
                  "responseType": "esriReplicaResponseTypeEdits",
                  "transportType": "esriTransportTypeURL",
                  "submissionTime": 1000,
                  "lastUpdatedTime": 2000
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request URI: {uri}");
        });

        var client = CreateClient(handler);

        var status = await client.WaitForSynchronizeReplicaCompletionAsync(
            new Uri(statusUrl),
            new ReplicaPollingOptions {
                PollInterval = TimeSpan.FromMilliseconds(1),
                Timeout = TimeSpan.FromSeconds(1)
            },
            cancellationToken);

        Assert.True(status.IsTerminal);
        Assert.True(status.IsCompleted);
        Assert.Equal("Completed With Errors", status.Status);
        Assert.Equal(new Uri(resultUrl), status.ResultUrl);
        Assert.Equal(1000, status.SubmissionTime);
        Assert.Equal(2000, status.LastUpdatedTime);
    }

    [Fact]
    public async Task SubmitAndDownloadSynchronizeReplicaFileAsync_SubmitsPollsAndDownloadsFile() {
        var cancellationToken = TestContext.Current.CancellationToken;
        const string statusUrl = "https://example.test/arcgis/rest/services/Test/FeatureServer/jobs/synchronizeReplica-123/status";
        const string resultUrl = "https://example.test/arcgis/rest/directories/arcgisoutput/Test_MapServer/synchronizeReplica-123.sqlite";

        var statusCalls = 0;
        var expectedContent = new byte[] { 1, 2, 3, 4 };
        string? requestBody = null;

        var handler = new StubHttpMessageHandler(request => {
            var uri = request.RequestUri!.AbsoluteUri;

            if (uri == "https://example.test/arcgis/rest/services/Test/FeatureServer?f=json") {
                return CreateSyncMetadataResponse();
            }

            if (uri == "https://example.test/arcgis/rest/services/Test/FeatureServer/synchronizeReplica") {
                requestBody = request.Content?.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult();

                return StubHttpMessageHandler.Json($$"""
                {
                  "statusUrl": "{{statusUrl}}"
                }
                """);
            }

            if (uri == statusUrl) {
                statusCalls++;

                return statusCalls == 1
                    ? StubHttpMessageHandler.Json("""
                    {
                      "status": "InProgress",
                      "responseType": "esriReplicaResponseTypeEdits",
                      "transportType": "esriTransportTypeURL"
                    }
                    """)
                    : StubHttpMessageHandler.Json($$"""
                    {
                      "status": "Completed",
                      "resultUrl": "{{resultUrl}}",
                      "responseType": "esriReplicaResponseTypeEdits",
                      "transportType": "esriTransportTypeURL"
                    }
                    """);
            }

            if (uri == resultUrl) {
                var response = new HttpResponseMessage(HttpStatusCode.OK) {
                    Content = new ByteArrayContent(expectedContent)
                };

                response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

                return response;
            }

            throw new InvalidOperationException($"Unexpected request URI: {uri}");
        });

        var client = CreateClient(handler);

        var file = await client.SubmitAndDownloadSynchronizeReplicaFileAsync(
            CreateValidSynchronizeReplicaRequest() with {
                IsAsync = true,
                DataFormat = SynchronizeReplicaDataFormat.Sqlite
            },
            new ReplicaPollingOptions {
                PollInterval = TimeSpan.FromMilliseconds(1),
                Timeout = TimeSpan.FromSeconds(1)
            },
            cancellationToken);

        Assert.Equal(expectedContent, file.Content);
        Assert.Equal("application/octet-stream", file.ContentType);
        Assert.Equal(new Uri(resultUrl), file.ResultUrl);
        Assert.Equal(2, statusCalls);

        Assert.NotNull(requestBody);
        Assert.Contains("async=true", requestBody);
        Assert.Contains("transportType=esriTransportTypeURL", requestBody);
        Assert.Contains("dataFormat=sqlite", requestBody);
    }

    [Fact]
    public async Task SubmitAndDownloadSynchronizeReplicaFileAsync_DownloadsImmediateUrlResult() {
        var cancellationToken = TestContext.Current.CancellationToken;
        const string resultUrl = "https://example.test/arcgis/rest/directories/arcgisoutput/Test_MapServer/synchronizeReplica-123.json";

        var expectedContent = new byte[] { 5, 6, 7, 8 };
        var statusWasCalled = false;

        var handler = new StubHttpMessageHandler(request => {
            var uri = request.RequestUri!.AbsoluteUri;

            if (uri == "https://example.test/arcgis/rest/services/Test/FeatureServer?f=json") {
                return CreateSyncMetadataResponse();
            }

            if (uri == "https://example.test/arcgis/rest/services/Test/FeatureServer/synchronizeReplica") {
                return StubHttpMessageHandler.Json($$"""
                {
                  "transportType": "esriTransportTypeURL",
                  "responseType": "esriReplicaResponseTypeEdits",
                  "URL": "{{resultUrl}}"
                }
                """);
            }

            if (uri.Contains("/jobs/", StringComparison.Ordinal)) {
                statusWasCalled = true;
                return StubHttpMessageHandler.Json("{}");
            }

            if (uri == resultUrl) {
                return new HttpResponseMessage(HttpStatusCode.OK) {
                    Content = new ByteArrayContent(expectedContent)
                };
            }

            throw new InvalidOperationException($"Unexpected request URI: {uri}");
        });

        var client = CreateClient(handler);

        var file = await client.SubmitAndDownloadSynchronizeReplicaFileAsync(
            CreateValidSynchronizeReplicaRequest(),
            cancellationToken: cancellationToken);

        Assert.Equal(expectedContent, file.Content);
        Assert.Equal(new Uri(resultUrl), file.ResultUrl);
        Assert.False(statusWasCalled);
    }

    [Fact]
    public async Task SubmitAndDownloadSynchronizeReplicaFileAsync_Throws_WhenRequestDoesNotUseUrlTransport() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(new StubHttpMessageHandler(_ =>
            throw new InvalidOperationException("The HTTP request should not be executed.")));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.SubmitAndDownloadSynchronizeReplicaFileAsync(
                CreateValidSynchronizeReplicaRequest() with {
                    TransportType = SynchronizeReplicaTransportType.Embedded
                },
                cancellationToken: cancellationToken));

        Assert.Contains("TransportType.Url", exception.Message);
    }

    [Fact]
    public async Task SubmitAndDownloadSynchronizeReplicaFileAsync_Throws_WhenJobFails() {
        var cancellationToken = TestContext.Current.CancellationToken;
        const string statusUrl = "https://example.test/arcgis/rest/services/Test/FeatureServer/jobs/synchronizeReplica-123/status";

        var handler = new StubHttpMessageHandler(request => {
            var uri = request.RequestUri!.AbsoluteUri;

            if (uri == "https://example.test/arcgis/rest/services/Test/FeatureServer?f=json") {
                return CreateSyncMetadataResponse();
            }

            if (uri == "https://example.test/arcgis/rest/services/Test/FeatureServer/synchronizeReplica") {
                return StubHttpMessageHandler.Json($$"""
                {
                  "statusUrl": "{{statusUrl}}"
                }
                """);
            }

            if (uri == statusUrl) {
                return StubHttpMessageHandler.Json("""
{
  "status": "Failed",
  "responseType": "esriReplicaResponseTypeEdits",
  "transportType": "esriTransportTypeURL",
  "error": {
    "code": 400,
    "message": "Synchronization failed."
  }
}
""");
            }

            throw new InvalidOperationException($"Unexpected request URI: {uri}");
        });

        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.SubmitAndDownloadSynchronizeReplicaFileAsync(
                CreateValidSynchronizeReplicaRequest() with {
                    IsAsync = true
                },
                new ReplicaPollingOptions {
                    PollInterval = TimeSpan.FromMilliseconds(1),
                    Timeout = TimeSpan.FromSeconds(1)
                },
                cancellationToken));

        Assert.Contains("ended with status 'Failed'", exception.Message);
        Assert.Contains("Synchronization failed.", exception.Message);
    }

    private static SynchronizeReplicaRequest CreateValidSynchronizeReplicaRequest() {
        return new SynchronizeReplicaRequest {
            ReplicaId = "replica-1",
            ReplicaServerGen = 1526605677436
        };
    }

    private static FeatureServiceClient CreateClient(HttpMessageHandler handler) {
        return new FeatureServiceClient(
            new HttpClient(handler),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });
    }

    private static HttpResponseMessage CreateSyncMetadataResponse() {
        return StubHttpMessageHandler.Json("""
        {
          "layers": [
            { "id": 0, "name": "Layer 0" }
          ],
          "tables": [],
          "capabilities": "Create,Update,Delete,Query,Sync",
          "syncEnabled": true,
          "syncCapabilities": {
            "supportsPerReplicaSync": true,
            "supportsPerLayerSync": true,
            "supportsSyncModelNone": true,
            "supportsAsync": true
          }
        }
        """);
    }
}