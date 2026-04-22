using System.Net;
using System.Net.Http.Headers;
using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Extensions;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Extensions;

public sealed class FeatureServiceClientExtractChangesExtensionsTests
{
    [Fact]
    public async Task WaitForExtractChangesCompletionAsync_ReturnsCompletedStatus_WhenStatusContainsSpaces() {
        const string statusUrl = "https://example.test/arcgis/rest/services/Test/FeatureServer/jobs/extractChanges-123/status";
        const string resultUrl = "https://example.test/arcgis/rest/directories/arcgisoutput/Test_MapServer/extractChanges-123.sqlite";

        var handler = new StubHttpMessageHandler(request => {
            var uri = request.RequestUri!.AbsoluteUri;

            if (uri == statusUrl) {
                return StubHttpMessageHandler.Json($$"""
                {
                  "status": "Completed With Errors",
                  "resultUrl": "{{resultUrl}}",
                  "responseType": "esriReplicaResponseTypeData",
                  "transportType": "esriTransportTypeURL",
                  "submissionTime": 1000,
                  "lastUpdatedTime": 2000
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request URI: {uri}");
        });

        var client = CreateClient(handler);

        var status = await client.WaitForExtractChangesCompletionAsync(
            new Uri(statusUrl),
            new ExtractChangesPollingOptions {
                PollInterval = TimeSpan.FromMilliseconds(1),
                Timeout = TimeSpan.FromSeconds(1)
            });

        Assert.True(status.IsTerminal);
        Assert.True(status.IsCompleted);
        Assert.Equal("Completed With Errors", status.Status);
        Assert.Equal(new Uri(resultUrl), status.ResultUrl);
        Assert.Equal(1000, status.SubmissionTime);
        Assert.Equal(2000, status.LastUpdatedTime);
    }

    [Fact]
    public async Task SubmitAndDownloadExtractChangesFileAsync_SubmitsPollsAndDownloadsSqliteFile() {
        const string statusUrl = "https://example.test/arcgis/rest/services/Test/FeatureServer/jobs/extractChanges-123/status";
        const string resultUrl = "https://example.test/arcgis/rest/directories/arcgisoutput/Test_MapServer/extractChanges-123.sqlite";

        string? requestBody = null;
        var statusCalls = 0;
        byte[] expectedContent = [1, 2, 3, 4];

        var handler = new StubHttpMessageHandler(request => {
            var uri = request.RequestUri!.AbsoluteUri;

            if (uri == "https://example.test/arcgis/rest/services/Test/FeatureServer?f=json") {
                return StubHttpMessageHandler.Json(
                    FeatureServiceTestResponses.CreateExtractChangesSupportedMetadataResponse());
            }

            if (uri == "https://example.test/arcgis/rest/services/Test/FeatureServer/extractChanges") {
                requestBody = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();

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
                      "responseType": "esriReplicaResponseTypeData",
                      "transportType": "esriTransportTypeURL"
                    }
                    """)
                    : StubHttpMessageHandler.Json($$"""
                    {
                      "status": "Completed",
                      "resultUrl": "{{resultUrl}}",
                      "responseType": "esriReplicaResponseTypeData",
                      "transportType": "esriTransportTypeURL"
                    }
                    """);
            }

            if (uri == resultUrl) {
                var response = new HttpResponseMessage(HttpStatusCode.OK) {
                    Content = new ByteArrayContent(expectedContent)
                };

                response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/x-sqlite3");
                response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment") {
                    FileName = "changes.sqlite"
                };

                return response;
            }

            throw new InvalidOperationException($"Unexpected request URI: {uri}");
        });

        var client = CreateClient(handler);

        var file = await client.SubmitAndDownloadExtractChangesFileAsync(
            new ExtractChangesRequest {
                Layers = [0],
                LayerServerGens = [new ExtractChangesLayerServerGen(0, 1653608093000)],
                ReturnInserts = true,
                ReturnUpdates = true,
                ReturnDeletes = true,
                DataFormat = ExtractChangesDataFormat.Sqlite
            },
            new ExtractChangesPollingOptions {
                PollInterval = TimeSpan.FromMilliseconds(1),
                Timeout = TimeSpan.FromSeconds(1)
            });

        Assert.NotNull(requestBody);
        Assert.Contains("async=true", requestBody!);
        Assert.Contains("dataFormat=sqllite", requestBody!);
        Assert.Equal(2, statusCalls);
        Assert.Equal(expectedContent, file.Content);
        Assert.Equal("application/x-sqlite3", file.ContentType);
        Assert.NotNull(file.FileName);
        Assert.Contains("changes.sqlite", file.FileName!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(new Uri(resultUrl), file.ResultUrl);
    }

    [Fact]
    public async Task SubmitAndDownloadExtractChangesFileAsync_Throws_WhenCompletedJobHasNoResultUrl() {
        const string statusUrl = "https://example.test/arcgis/rest/services/Test/FeatureServer/jobs/extractChanges-123/status";

        var handler = FeatureServiceTestHandlers.WithExtractChangesMetadata(request => {
            var uri = request.RequestUri!.AbsoluteUri;

            if (uri == "https://example.test/arcgis/rest/services/Test/FeatureServer/extractChanges") {
                return StubHttpMessageHandler.Json($$"""
            {
              "statusUrl": "{{statusUrl}}"
            }
            """);
            }

            if (uri == statusUrl) {
                return StubHttpMessageHandler.Json("""
            {
              "status": "Completed",
              "responseType": "esriReplicaResponseTypeData",
              "transportType": "esriTransportTypeURL"
            }
            """);
            }

            throw new InvalidOperationException($"Unexpected request URI: {uri}");
        });

        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.SubmitAndDownloadExtractChangesFileAsync(
                new ExtractChangesRequest {
                    Layers = [0],
                    LayerServerGens = [new ExtractChangesLayerServerGen(0, 1653608093000)],
                    ReturnInserts = true,
                    ReturnUpdates = true,
                    ReturnDeletes = true,
                    DataFormat = ExtractChangesDataFormat.Sqlite
                },
                new ExtractChangesPollingOptions {
                    PollInterval = TimeSpan.FromMilliseconds(1),
                    Timeout = TimeSpan.FromSeconds(1)
                }));

        Assert.Contains("completed without a result URL", exception.Message);
    }

    [Fact]
    public async Task SubmitAndDownloadExtractChangesFileAsync_Throws_WhenRequestIsNotSqlite() {
        var client = CreateClient(new StubHttpMessageHandler(_ =>
            throw new InvalidOperationException("The HTTP request should not be executed.")));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.SubmitAndDownloadExtractChangesFileAsync(
                new ExtractChangesRequest {
                    Layers = [0],
                    LayerServerGens = [new ExtractChangesLayerServerGen(0, 1653608093000)],
                    ReturnInserts = true,
                    ReturnUpdates = true,
                    ReturnDeletes = true,
                    DataFormat = ExtractChangesDataFormat.Json
                }));

        Assert.Contains("requires DataFormat.Sqlite", exception.Message);
    }

    private static FeatureServiceClient CreateClient(HttpMessageHandler handler) {
        return new FeatureServiceClient(
            new HttpClient(handler),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });
    }
}