using System.Net;
using System.Text;
using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Exceptions;
using S100Framework.REST.Extensions;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Extensions;

public sealed class FeatureServiceClientReplicaUploadStateExtensionsTests
{
    [Fact]
    public async Task SynchronizeReplicaStateUploadAsync_SendsPerReplicaStructuredEditsDownloadsJsonAndKeepsState() {
        var cancellationToken = TestContext.Current.CancellationToken;
        const string resultUrl = "https://example.test/output/upload.json";
        string? requestBody = null;

        var initialState = new ReplicaSynchronizationState {
            ReplicaId = "replica-1",
            ReplicaName = "Replica A",
            SyncModel = SynchronizeReplicaSyncModel.PerReplica,
            ReplicaServerGen = 10
        };

        var client = CreateClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;

            if (uri == "https://example.test/arcgis/rest/services/Test/FeatureServer?f=json") {
                return CreateSyncMetadataResponse();
            }

            if (uri == "https://example.test/arcgis/rest/services/Test/FeatureServer/synchronizeReplica") {
                requestBody = request.Content?.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult();

                return StubHttpMessageHandler.Json($$"""
                {
                  "transportType": "esriTransportTypeURL",
                  "responseType": "esriReplicaResponseTypeEdits",
                  "URL": "{{resultUrl}}"
                }
                """);
            }

            if (uri == resultUrl) {
                return new HttpResponseMessage(HttpStatusCode.OK) {
                    Content = new ByteArrayContent(Encoding.UTF8.GetBytes("""
                    {
                      "replicaID": "replica-1",
                      "replicaName": "Replica A",
                      "responseType": "esriReplicaResponseTypeEdits",
                      "edits": [
                        {
                          "id": 0,
                          "addResults": [
                            {
                              "objectId": 101,
                              "globalId": "{11111111-1111-1111-1111-111111111111}",
                              "success": true
                            }
                          ]
                        }
                      ]
                    }
                    """))
                };
            }

            throw new InvalidOperationException($"Unexpected request URI: {uri}");
        });

        var result = await client.SynchronizeReplicaStateUploadAsync(
            initialState,
            new SynchronizeReplicaStateUploadRequest {
                Edits = new ReplicaEdits {
                    Layers = [
                        new ReplicaLayerEdits {
                            Id = 0,
                            AddsJson = """
                            [
                              {
                                "attributes": {
                                  "globalID": "{11111111-1111-1111-1111-111111111111}"
                                }
                              }
                            ]
                            """
                        }
                    ]
                },
                RollbackOnFailure = true,
                ReturnIdsForAdds = true
            },
            cancellationToken);

        Assert.Same(initialState, result.State);
        Assert.Equal(10, result.State.ReplicaServerGen);
        Assert.Equal(new Uri(resultUrl), result.File.ResultUrl);
        Assert.True(result.JsonResult.HasEditResults);
        Assert.False(result.JsonResult.HasEditErrors);

        var addResult = Assert.Single(result.JsonResult.Layers.Single().AddResults);
        Assert.Equal(101, addResult.ObjectId);
        Assert.True(addResult.Success);

        Assert.NotNull(requestBody);

        var decodedBody = WebUtility.UrlDecode(requestBody!);

        Assert.Contains("syncDirection=upload", decodedBody);
        Assert.Contains("syncModel=perReplica", decodedBody);
        Assert.DoesNotContain("replicaServerGen=", decodedBody);
        Assert.Contains("rollbackOnFailure=true", decodedBody);
        Assert.Contains("returnIdsForAdds=true", decodedBody);
        Assert.Contains("edits=[{\"id\":0,\"adds\":[", decodedBody);
    }

    [Fact]
    public async Task SynchronizeReplicaStateUploadAsync_SendsPerLayerUploadIdWithoutServerGens() {
        var cancellationToken = TestContext.Current.CancellationToken;
        const string resultUrl = "https://example.test/output/upload.json";
        string? requestBody = null;

        var client = CreateClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;

            if (uri == "https://example.test/arcgis/rest/services/Test/FeatureServer?f=json") {
                return CreateSyncMetadataResponse();
            }

            if (uri == "https://example.test/arcgis/rest/services/Test/FeatureServer/synchronizeReplica") {
                requestBody = request.Content?.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult();

                return StubHttpMessageHandler.Json($$"""
                {
                  "transportType": "esriTransportTypeURL",
                  "responseType": "esriReplicaResponseTypeEdits",
                  "URL": "{{resultUrl}}"
                }
                """);
            }

            if (uri == resultUrl) {
                return new HttpResponseMessage(HttpStatusCode.OK) {
                    Content = new ByteArrayContent(Encoding.UTF8.GetBytes("""
                    {
                      "replicaID": "replica-1",
                      "responseType": "esriReplicaResponseTypeEdits"
                    }
                    """))
                };
            }

            throw new InvalidOperationException($"Unexpected request URI: {uri}");
        });

        await client.SynchronizeReplicaStateUploadAsync(
            new ReplicaSynchronizationState {
                ReplicaId = "replica-1",
                SyncModel = SynchronizeReplicaSyncModel.PerLayer,
                LayerServerGens = [
                    new ReplicaLayerServerGeneration(0, 20),
                    new ReplicaLayerServerGeneration(1, 21)
                ]
            },
            new SynchronizeReplicaStateUploadRequest {
                EditsUploadId = "upload-1"
            },
            cancellationToken);

        Assert.NotNull(requestBody);

        var decodedBody = WebUtility.UrlDecode(requestBody!);

        Assert.Contains("syncModel=perLayer", decodedBody);
        Assert.Contains("syncDirection=upload", decodedBody);
        Assert.Contains("editsUploadId=upload-1", decodedBody);
        Assert.Contains("syncLayers=", decodedBody);
        Assert.Contains("\"syncDirection\":\"upload\"", decodedBody);
        Assert.DoesNotContain("\"serverGen\"", decodedBody);
    }

    [Fact]
    public async Task SynchronizeReplicaStateUploadAsync_SubmitsAsyncPollsAndDownloadsJson() {
        var cancellationToken = TestContext.Current.CancellationToken;
        const string statusUrl = "https://example.test/arcgis/rest/services/Test/FeatureServer/jobs/upload-1/status";
        const string resultUrl = "https://example.test/output/upload.json";
        var statusCalls = 0;

        var client = CreateClient(request => {
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
                statusCalls++;

                return statusCalls == 1
                    ? StubHttpMessageHandler.Json("""
                    {
                      "status": "In Progress"
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
                return new HttpResponseMessage(HttpStatusCode.OK) {
                    Content = new ByteArrayContent(Encoding.UTF8.GetBytes("""
                    {
                      "replicaID": "replica-1",
                      "responseType": "esriReplicaResponseTypeEdits"
                    }
                    """))
                };
            }

            throw new InvalidOperationException($"Unexpected request URI: {uri}");
        });

        await client.SynchronizeReplicaStateUploadAsync(
            new ReplicaSynchronizationState {
                ReplicaId = "replica-1",
                SyncModel = SynchronizeReplicaSyncModel.PerReplica,
                ReplicaServerGen = 10
            },
            new SynchronizeReplicaStateUploadRequest {
                EditsJson = """{"layers":[]}""",
                IsAsync = true,
                PollingOptions = new ReplicaPollingOptions {
                    PollInterval = TimeSpan.FromMilliseconds(1),
                    Timeout = TimeSpan.FromSeconds(1)
                }
            },
            cancellationToken);

        Assert.Equal(2, statusCalls);
    }

    [Fact]
    public async Task SynchronizeReplicaStateUploadAsync_ThrowsReplicaEditResultsException_WhenThrowOnEditErrorsIsEnabled() {
        var cancellationToken = TestContext.Current.CancellationToken;
        const string resultUrl = "https://example.test/output/upload.json";

        var client = CreateClient(request => {
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

            if (uri == resultUrl) {
                return new HttpResponseMessage(HttpStatusCode.OK) {
                    Content = new ByteArrayContent(Encoding.UTF8.GetBytes("""
                    {
                      "replicaID": "replica-1",
                      "responseType": "esriReplicaResponseTypeEdits",
                      "edits": [
                        {
                          "id": 0,
                          "updateResults": [
                            {
                              "objectId": 102,
                              "success": false,
                              "error": {
                                "code": 400,
                                "description": "Update failed."
                              }
                            }
                          ]
                        }
                      ]
                    }
                    """))
                };
            }

            throw new InvalidOperationException($"Unexpected request URI: {uri}");
        });

        var exception = await Assert.ThrowsAsync<ReplicaEditResultsException>(() =>
            client.SynchronizeReplicaStateUploadAsync(
                new ReplicaSynchronizationState {
                    ReplicaId = "replica-1",
                    SyncModel = SynchronizeReplicaSyncModel.PerReplica,
                    ReplicaServerGen = 10
                },
                new SynchronizeReplicaStateUploadRequest {
                    EditsJson = """{"layers":[]}""",
                    ThrowOnEditErrors = true
                },
                cancellationToken));

        var error = Assert.Single(exception.Errors);

        Assert.Equal(0, error.LayerId);
        Assert.Equal(ReplicaEditOperation.Update, error.Operation);
        Assert.Equal(102, error.ObjectId);
        Assert.Equal(400, error.ErrorCode);
    }

    [Fact]
    public async Task SynchronizeReplicaStateUploadAsync_ValidatesRequestBeforeHttp() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var wasCalled = false;

        var client = CreateClient(_ => {
            wasCalled = true;
            return StubHttpMessageHandler.Json("{}");
        });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.SynchronizeReplicaStateUploadAsync(
                new ReplicaSynchronizationState {
                    ReplicaId = "replica-1",
                    SyncModel = SynchronizeReplicaSyncModel.PerReplica,
                    ReplicaServerGen = 10
                },
                new SynchronizeReplicaStateUploadRequest(),
                cancellationToken));

        Assert.Contains("requires Edits, EditsJson, or EditsUploadId", exception.Message);
        Assert.False(wasCalled);
    }

    private static FeatureServiceClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> handler) {
        return new FeatureServiceClient(
            new HttpClient(new StubHttpMessageHandler(handler)),
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
            "supportsAsync": true,
            "supportsSyncDirectionControl": true,
            "supportsRollbackOnFailure": true
          }
        }
        """);
    }
}