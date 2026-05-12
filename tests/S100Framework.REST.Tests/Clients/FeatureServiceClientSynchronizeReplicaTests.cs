using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Extensions;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using System.Net;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureServiceClientSynchronizeReplicaTests
{
    [Fact]
    public async Task SubmitSynchronizeReplicaAsync_SendsExpectedPerReplicaRequestAndMapsUrlResult() {
        var cancellationToken = TestContext.Current.CancellationToken;
        string? requestBody = null;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateSyncMetadataResponse();
            }

            if (IsSynchronizeReplicaRequest(request)) {
                requestBody = request.Content?.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult();

                return StubHttpMessageHandler.Json("""
                {
                  "transportType": "esriTransportTypeURL",
                  "responseType": "esriReplicaResponseTypeEdits",
                  "replicaID": "replica-1",
                  "replicaName": "Replica A",
                  "replicaServerGen": 1526606896310,
                  "URL": "https://example.test/output/sync.json"
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var submission = await client.SubmitSynchronizeReplicaAsync(
            new SynchronizeReplicaRequest {
                ReplicaId = "replica-1",
                ReplicaServerGen = 1526605677436,
                SyncModel = SynchronizeReplicaSyncModel.PerReplica
            },
            cancellationToken);

        Assert.False(submission.IsPending);
        Assert.Null(submission.StatusUrl);

        var result = Assert.IsType<SynchronizeReplicaResult>(submission.Result);
        Assert.Equal("replica-1", result.ReplicaId);
        Assert.Equal("Replica A", result.ReplicaName);
        Assert.Equal("esriTransportTypeURL", result.TransportType);
        Assert.Equal("esriReplicaResponseTypeEdits", result.ResponseType);
        Assert.Equal(1526606896310, result.ReplicaServerGen);
        Assert.Equal("https://example.test/output/sync.json", result.ResultUrl!.AbsoluteUri);

        Assert.NotNull(requestBody);
        Assert.Contains("f=json", requestBody);
        Assert.Contains("replicaID=replica-1", requestBody);
        Assert.Contains("replicaServerGen=1526605677436", requestBody);
        Assert.Contains("transportType=esriTransportTypeURL", requestBody);
        Assert.Contains("closeReplica=false", requestBody);
        Assert.Contains("returnAttachmentsDataByUrl=false", requestBody);
        Assert.Contains("async=false", requestBody);
        Assert.Contains("syncDirection=download", requestBody);
        Assert.Contains("dataFormat=json", requestBody);
        Assert.Contains("syncModel=perReplica", requestBody);
    }

    [Fact]
    public async Task SubmitSynchronizeReplicaAsync_SendsExpectedPerLayerRequestAndMapsLayerServerGens() {
        var cancellationToken = TestContext.Current.CancellationToken;
        string? requestBody = null;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateSyncMetadataResponse();
            }

            if (IsSynchronizeReplicaRequest(request)) {
                requestBody = request.Content?.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult();

                return StubHttpMessageHandler.Json("""
                {
                  "transportType": "esriTransportTypeURL",
                  "responseType": "esriReplicaResponseTypeEdits",
                  "layerServerGens": [
                    { "id": 0, "serverGen": 1526606896310 },
                    { "id": 1, "serverGen": "1526606896311" }
                  ],
                  "URL": "https://example.test/output/sync.geodatabase"
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var submission = await client.SubmitSynchronizeReplicaAsync(
            new SynchronizeReplicaRequest {
                ReplicaId = "replica-1",
                SyncModel = SynchronizeReplicaSyncModel.PerLayer,
                DataFormat = SynchronizeReplicaDataFormat.Sqlite,
                SyncLayers = [
                    new SynchronizeReplicaSyncLayer {
                        Id = 0,
                        ServerGen = 1526605677436
                    },
                    new SynchronizeReplicaSyncLayer {
                        Id = 1,
                        SyncDirection = SynchronizeReplicaSyncDirection.Snapshot
                    }
                ]
            },
            cancellationToken);

        var result = Assert.IsType<SynchronizeReplicaResult>(submission.Result);

        Assert.Collection(
            result.LayerServerGens,
            first => {
                Assert.Equal(0, first.Id);
                Assert.Equal(1526606896310, first.ServerGen);
            },
            second => {
                Assert.Equal(1, second.Id);
                Assert.Equal(1526606896311, second.ServerGen);
            });

        Assert.Equal("https://example.test/output/sync.geodatabase", result.ResultUrl!.AbsoluteUri);

        Assert.NotNull(requestBody);
        Assert.Contains("syncLayers=", requestBody);
        Assert.Contains("dataFormat=sqlite", requestBody);
        Assert.Contains("syncModel=perLayer", requestBody);
    }

    [Fact]
    public async Task SubmitSynchronizeReplicaAsync_ReturnsStatusUrl_WhenServerRespondsAsynchronously() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateSyncMetadataResponse();
            }

            if (IsSynchronizeReplicaRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "statusUrl": "https://example.test/arcgis/rest/services/Test/FeatureServer/jobs/j123"
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var submission = await client.SubmitSynchronizeReplicaAsync(
            new SynchronizeReplicaRequest {
                ReplicaId = "replica-1",
                ReplicaServerGen = 1526605677436,
                IsAsync = true
            },
            cancellationToken);

        Assert.True(submission.IsPending);
        Assert.Null(submission.Result);
        Assert.Equal(
            "https://example.test/arcgis/rest/services/Test/FeatureServer/jobs/j123",
            submission.StatusUrl!.AbsoluteUri);
    }

    [Fact]
    public async Task GetSynchronizeReplicaStatusAsync_MapsCompletedStatus() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(_ =>
            StubHttpMessageHandler.Json("""
            {
              "transportType": "esriTransportTypeURL",
              "responseType": "esriReplicaResponseTypeData",
              "replicaName": "Replica A",
              "resultUrl": "https://example.test/output/sync.json",
              "submissionTime": "1653614927000",
              "lastUpdatedTime": 1653614930000,
              "status": "Completed"
            }
            """));

        var status = await client.GetSynchronizeReplicaStatusAsync(
            new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer/jobs/j123"),
            cancellationToken);

        Assert.True(status.IsTerminal);
        Assert.True(status.IsCompleted);
        Assert.Equal("Completed", status.Status);
        Assert.Equal("Replica A", status.ReplicaName);
        Assert.Equal("https://example.test/output/sync.json", status.ResultUrl!.AbsoluteUri);
        Assert.Equal(1653614927000, status.SubmissionTime);
        Assert.Equal(1653614930000, status.LastUpdatedTime);
    }

    [Fact]
    public async Task DownloadSynchronizeReplicaFileAsync_DownloadsReplicaFile() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var bytes = new byte[] { 1, 2, 3, 4 };

        var client = CreateClient(_ => {
            var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK) {
                Content = new ByteArrayContent(bytes)
            };

            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
                "application/octet-stream");
            response.Content.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue(
                "attachment") {
                FileName = "sync.geodatabase"
            };

            return response;
        });

        var result = await client.DownloadSynchronizeReplicaFileAsync(
            new Uri("https://example.test/output/sync.geodatabase"),
            cancellationToken);

        Assert.Equal(bytes, result.Content);
        Assert.Equal("application/octet-stream", result.ContentType);
        Assert.Equal("sync.geodatabase", result.FileName);
        Assert.Equal("https://example.test/output/sync.geodatabase", result.ResultUrl.AbsoluteUri);
    }

    [Fact]
    public async Task GetSynchronizeReplicaStatusAsync_MapsUrlFallbackAndErrorPayload() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(_ =>
            StubHttpMessageHandler.Json("""
        {
          "status": "Failed",
          "URL": "https://example.test/output/sync.json",
          "error": {
            "code": 400,
            "message": "Synchronization failed.",
            "details": [
              null,
              "",
              "Replica generation is too old."
            ]
          }
        }
        """));

        var status = await client.GetSynchronizeReplicaStatusAsync(
            new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer/jobs/j123"),
            cancellationToken);

        Assert.True(status.IsFailed);
        Assert.True(status.IsTerminal);
        Assert.Equal("https://example.test/output/sync.json", status.ResultUrl!.AbsoluteUri);
        Assert.True(status.HasError);
        Assert.Equal(400, status.ErrorCode);
        Assert.Equal("Synchronization failed.", status.ErrorMessage);
        Assert.Equal(["Replica generation is too old."], status.ErrorDetails);
    }

    [Fact]
    public async Task SubmitSynchronizeReplicaAsync_SendsExpectedUploadRequestWithRawEditsJson() {
        var cancellationToken = TestContext.Current.CancellationToken;
        string? requestBody = null;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateSyncMetadataResponse();
            }

            if (IsSynchronizeReplicaRequest(request)) {
                requestBody = request.Content?.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult();

                return StubHttpMessageHandler.Json("""
            {
              "transportType": "esriTransportTypeURL",
              "responseType": "esriReplicaResponseTypeEdits",
              "replicaID": "replica-1",
              "replicaName": "Replica A",
              "replicaServerGen": 1526606896310,
              "URL": "https://example.test/output/sync.json"
            }
            """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        await client.SubmitSynchronizeReplicaAsync(
            new SynchronizeReplicaRequest {
                ReplicaId = "replica-1",
                SyncDirection = SynchronizeReplicaSyncDirection.Upload,
                EditsJson = """{"layers":[]}""",
                RollbackOnFailure = true,
                ReturnIdsForAdds = true
            },
            cancellationToken);

        Assert.NotNull(requestBody);

        var decodedBody = WebUtility.UrlDecode(requestBody!);

        Assert.Contains("syncDirection=upload", decodedBody);
        Assert.Contains("edits={\"layers\":[]}", decodedBody);
        Assert.Contains("rollbackOnFailure=true", decodedBody);
        Assert.Contains("returnIdsForAdds=true", decodedBody);
        Assert.DoesNotContain("replicaServerGen=", decodedBody);
    }

    [Fact]
    public async Task SubmitSynchronizeReplicaAsync_SendsExpectedBidirectionalRequestWithEditsUploadId() {
        var cancellationToken = TestContext.Current.CancellationToken;
        string? requestBody = null;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateSyncMetadataResponse();
            }

            if (IsSynchronizeReplicaRequest(request)) {
                requestBody = request.Content?.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult();

                return StubHttpMessageHandler.Json("""
            {
              "transportType": "esriTransportTypeURL",
              "responseType": "esriReplicaResponseTypeEdits",
              "replicaID": "replica-1",
              "replicaName": "Replica A",
              "replicaServerGen": 1526606896310,
              "URL": "https://example.test/output/sync.json"
            }
            """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        await client.SubmitSynchronizeReplicaAsync(
            new SynchronizeReplicaRequest {
                ReplicaId = "replica-1",
                ReplicaServerGen = 1526605677436,
                SyncDirection = SynchronizeReplicaSyncDirection.Bidirectional,
                EditsUploadId = "upload-1"
            },
            cancellationToken);

        Assert.NotNull(requestBody);

        var decodedBody = WebUtility.UrlDecode(requestBody!);

        Assert.Contains("syncDirection=bidirectional", decodedBody);
        Assert.Contains("editsUploadId=upload-1", decodedBody);
        Assert.Contains("replicaServerGen=1526605677436", decodedBody);
        Assert.Contains("rollbackOnFailure=false", decodedBody);
        Assert.Contains("returnIdsForAdds=false", decodedBody);
    }

    [Fact]
    public async Task SubmitSynchronizeReplicaAsync_SendsExpectedPerLayerBidirectionalRequest() {
        var cancellationToken = TestContext.Current.CancellationToken;
        string? requestBody = null;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateSyncMetadataResponse();
            }

            if (IsSynchronizeReplicaRequest(request)) {
                requestBody = request.Content?.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult();

                return StubHttpMessageHandler.Json("""
            {
              "transportType": "esriTransportTypeURL",
              "responseType": "esriReplicaResponseTypeEdits",
              "layerServerGens": [
                { "id": 0, "serverGen": 1526606896310 }
              ],
              "URL": "https://example.test/output/sync.json"
            }
            """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        await client.SubmitSynchronizeReplicaAsync(
            new SynchronizeReplicaRequest {
                ReplicaId = "replica-1",
                SyncModel = SynchronizeReplicaSyncModel.PerLayer,
                SyncDirection = SynchronizeReplicaSyncDirection.Bidirectional,
                EditsJson = """{"layers":[]}""",
                SyncLayers = [
                    new SynchronizeReplicaSyncLayer {
                    Id = 0,
                    ServerGen = 1526605677436,
                    SyncDirection = SynchronizeReplicaSyncDirection.Bidirectional
                }
                ]
            },
            cancellationToken);

        Assert.NotNull(requestBody);

        var decodedBody = WebUtility.UrlDecode(requestBody!);

        Assert.Contains("syncModel=perLayer", decodedBody);
        Assert.Contains("syncDirection=bidirectional", decodedBody);
        Assert.Contains("syncLayers=", decodedBody);
        Assert.Contains("\"syncDirection\":\"bidirectional\"", decodedBody);
        Assert.Contains("edits={\"layers\":[]}", decodedBody);
    }

    [Fact]
    public async Task SubmitSynchronizeReplicaAsync_SendsExpectedUploadRequestWithStructuredEdits() {
        var cancellationToken = TestContext.Current.CancellationToken;
        string? requestBody = null;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateSyncMetadataResponse();
            }

            if (IsSynchronizeReplicaRequest(request)) {
                requestBody = request.Content?.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult();

                return StubHttpMessageHandler.Json("""
            {
              "transportType": "esriTransportTypeURL",
              "responseType": "esriReplicaResponseTypeEdits",
              "replicaID": "replica-1",
              "replicaName": "Replica A",
              "replicaServerGen": 1526606896310,
              "URL": "https://example.test/output/sync.json"
            }
            """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var edits = new ReplicaEdits {
            Layers = [
                new ReplicaLayerEdits {
                Id = 0,
                AddsJson = """
                [
                  {
                    "attributes": {
                      "globalID": "{11111111-1111-1111-1111-111111111111}",
                      "name": "New feature"
                    }
                  }
                ]
                """
            }
            ]
        };

        var request = new SynchronizeReplicaRequest {
            ReplicaId = "replica-1",
            SyncDirection = SynchronizeReplicaSyncDirection.Upload,
            RollbackOnFailure = true
        }.WithEdits(edits);

        await client.SubmitSynchronizeReplicaAsync(request, cancellationToken);

        Assert.NotNull(requestBody);

        var decodedBody = System.Net.WebUtility.UrlDecode(requestBody!);

        Assert.Contains("syncDirection=upload", decodedBody);
        Assert.Contains("rollbackOnFailure=true", decodedBody);
        Assert.Contains("edits=[{\"id\":0,\"adds\":[", decodedBody);
        Assert.Contains("\"globalID\":\"{11111111-1111-1111-1111-111111111111}\"", decodedBody);
    }

    private static FeatureServiceClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> handler) {
        return new FeatureServiceClient(
            new HttpClient(new StubHttpMessageHandler(handler)),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });
    }

    private static bool IsServiceMetadataRequest(HttpRequestMessage request) {
        return request.RequestUri?.AbsoluteUri ==
               "https://example.test/arcgis/rest/services/Test/FeatureServer?f=json";
    }

    private static bool IsSynchronizeReplicaRequest(HttpRequestMessage request) {
        return request.Method == HttpMethod.Post &&
               request.RequestUri?.AbsoluteUri ==
               "https://example.test/arcgis/rest/services/Test/FeatureServer/synchronizeReplica";
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