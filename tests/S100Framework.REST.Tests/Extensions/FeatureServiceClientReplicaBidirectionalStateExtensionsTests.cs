using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Exceptions;
using S100Framework.REST.Extensions;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using System.Net;
using System.Text;
using Xunit;

namespace S100Framework.REST.Tests.Extensions;

public sealed class FeatureServiceClientReplicaBidirectionalStateExtensionsTests
{
    [Fact]
    public async Task SynchronizeReplicaStateBidirectionalAsync_SendsPerReplicaStructuredEditsDownloadsJsonAndUpdatesState() {
        var cancellationToken = TestContext.Current.CancellationToken;
        const string resultUrl = "https://example.test/output/sync.json";
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
                      "replicaName": "Replica A",
                      "responseType": "esriReplicaResponseTypeEdits",
                      "replicaServerGen": 25,
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

        var result = await client.SynchronizeReplicaStateBidirectionalAsync(
            new ReplicaSynchronizationState {
                ReplicaId = "replica-1",
                ReplicaName = "Replica A",
                SyncModel = SynchronizeReplicaSyncModel.PerReplica,
                ReplicaServerGen = 10
            },
            new SynchronizeReplicaStateBidirectionalRequest {
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

        Assert.Equal(new Uri(resultUrl), result.File.ResultUrl);
        Assert.Equal("replica-1", result.UpdatedState.ReplicaId);
        Assert.Equal("Replica A", result.UpdatedState.ReplicaName);
        Assert.Equal(SynchronizeReplicaSyncModel.PerReplica, result.UpdatedState.SyncModel);
        Assert.Equal(25, result.UpdatedState.ReplicaServerGen);
        Assert.Equal(25, result.SynchronizationResult.ReplicaServerGen);

        Assert.True(result.JsonResult.HasEditResults);
        result.JsonResult.ThrowIfEditErrors();
        var layer = Assert.Single(result.JsonResult.Layers);
        var addResult = Assert.Single(layer.AddResults);
        Assert.Equal(101, addResult.ObjectId);
        Assert.True(addResult.Success);

        Assert.NotNull(requestBody);

        var decodedBody = WebUtility.UrlDecode(requestBody!);

        Assert.Contains("syncDirection=bidirectional", decodedBody);
        Assert.Contains("syncModel=perReplica", decodedBody);
        Assert.Contains("replicaServerGen=10", decodedBody);
        Assert.Contains("rollbackOnFailure=true", decodedBody);
        Assert.Contains("returnIdsForAdds=true", decodedBody);
        Assert.Contains("edits=[{\"id\":0,\"adds\":[", decodedBody);
    }

    [Fact]
    public async Task SynchronizeReplicaStateBidirectionalAsync_SendsPerLayerUploadIdAndUpdatesState() {
        var cancellationToken = TestContext.Current.CancellationToken;
        const string resultUrl = "https://example.test/output/sync.json";
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
                      "replicaName": "Replica A",
                      "responseType": "esriReplicaResponseTypeEdits",
                      "layerServerGens": [
                        { "id": 0, "serverGen": 30 },
                        { "id": 1, "serverGen": 31 }
                      ]
                    }
                    """))
                };
            }

            throw new InvalidOperationException($"Unexpected request URI: {uri}");
        });

        var result = await client.SynchronizeReplicaStateBidirectionalAsync(
            new ReplicaSynchronizationState {
                ReplicaId = "replica-1",
                ReplicaName = "Replica A",
                SyncModel = SynchronizeReplicaSyncModel.PerLayer,
                LayerServerGens = [
                    new ReplicaLayerServerGeneration(0, 20),
                    new ReplicaLayerServerGeneration(1, 21)
                ]
            },
            new SynchronizeReplicaStateBidirectionalRequest {
                EditsUploadId = "upload-1"
            },
            cancellationToken);

        Assert.Equal(SynchronizeReplicaSyncModel.PerLayer, result.UpdatedState.SyncModel);
        Assert.Null(result.UpdatedState.ReplicaServerGen);

        Assert.Collection(
            result.UpdatedState.LayerServerGens,
            first => {
                Assert.Equal(0, first.Id);
                Assert.Equal(30, first.ServerGen);
            },
            second => {
                Assert.Equal(1, second.Id);
                Assert.Equal(31, second.ServerGen);
            });

        Assert.NotNull(requestBody);

        var decodedBody = WebUtility.UrlDecode(requestBody!);

        Assert.Contains("syncModel=perLayer", decodedBody);
        Assert.Contains("syncDirection=bidirectional", decodedBody);
        Assert.Contains("editsUploadId=upload-1", decodedBody);
        Assert.Contains("syncLayers=", decodedBody);
        Assert.Contains("\"syncDirection\":\"bidirectional\"", decodedBody);
    }

    [Fact]
    public async Task SynchronizeReplicaStateBidirectionalAsync_SubmitsAsyncPollsDownloadsAndUpdatesState() {
        var cancellationToken = TestContext.Current.CancellationToken;
        const string statusUrl = "https://example.test/arcgis/rest/services/Test/FeatureServer/jobs/bidir-1/status";
        const string resultUrl = "https://example.test/output/sync.json";
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
                      "replicaServerGen": 25
                    }
                    """))
                };
            }

            throw new InvalidOperationException($"Unexpected request URI: {uri}");
        });

        var result = await client.SynchronizeReplicaStateBidirectionalAsync(
            new ReplicaSynchronizationState {
                ReplicaId = "replica-1",
                SyncModel = SynchronizeReplicaSyncModel.PerReplica,
                ReplicaServerGen = 10
            },
            new SynchronizeReplicaStateBidirectionalRequest {
                EditsJson = """{"layers":[]}""",
                IsAsync = true,
                PollingOptions = new ReplicaPollingOptions {
                    PollInterval = TimeSpan.FromMilliseconds(1),
                    Timeout = TimeSpan.FromSeconds(1)
                }
            },
            cancellationToken);

        Assert.Equal(2, statusCalls);
        Assert.Equal(25, result.UpdatedState.ReplicaServerGen);
    }

    [Fact]
    public async Task SynchronizeReplicaStateBidirectionalAsync_ValidatesRequestBeforeHttp() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var wasCalled = false;

        var client = CreateClient(_ => {
            wasCalled = true;
            return StubHttpMessageHandler.Json("{}");
        });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.SynchronizeReplicaStateBidirectionalAsync(
                new ReplicaSynchronizationState {
                    ReplicaId = "replica-1",
                    SyncModel = SynchronizeReplicaSyncModel.PerReplica,
                    ReplicaServerGen = 10
                },
                new SynchronizeReplicaStateBidirectionalRequest(),
                cancellationToken));

        Assert.Contains("requires Edits, EditsJson, or EditsUploadId", exception.Message);
        Assert.False(wasCalled);
    }

    [Fact]
    public async Task SynchronizeReplicaStateBidirectionalAsync_Throws_WhenJsonResultCannotUpdateState() {
        var cancellationToken = TestContext.Current.CancellationToken;
        const string resultUrl = "https://example.test/output/sync.json";

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
                      "replicaID": "replica-1"
                    }
                    """))
                };
            }

            throw new InvalidOperationException($"Unexpected request URI: {uri}");
        });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.SynchronizeReplicaStateBidirectionalAsync(
                new ReplicaSynchronizationState {
                    ReplicaId = "replica-1",
                    SyncModel = SynchronizeReplicaSyncModel.PerReplica,
                    ReplicaServerGen = 10
                },
                new SynchronizeReplicaStateBidirectionalRequest {
                    EditsJson = """{"layers":[]}"""
                },
                cancellationToken));

        Assert.Contains("ReplicaServerGen", exception.Message);
    }

    [Fact]
    public async Task SynchronizeReplicaStateBidirectionalAsync_ThrowsReplicaEditResultsException_WhenThrowOnEditErrorsIsEnabled() {
        var cancellationToken = TestContext.Current.CancellationToken;
        const string resultUrl = "https://example.test/output/sync.json";

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
                  "replicaName": "Replica A",
                  "responseType": "esriReplicaResponseTypeEdits",
                  "replicaServerGen": 25,
                  "edits": [
                    {
                      "id": 0,
                      "updateResults": [
                        {
                          "objectId": 102,
                          "globalId": "{22222222-2222-2222-2222-222222222222}",
                          "success": false,
                          "error": {
                            "code": 400,
                            "description": "Update failed.",
                            "details": [
                              "Missing required field."
                            ]
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
            client.SynchronizeReplicaStateBidirectionalAsync(
                new ReplicaSynchronizationState {
                    ReplicaId = "replica-1",
                    ReplicaName = "Replica A",
                    SyncModel = SynchronizeReplicaSyncModel.PerReplica,
                    ReplicaServerGen = 10
                },
                new SynchronizeReplicaStateBidirectionalRequest {
                    EditsJson = """{"layers":[]}""",
                    ThrowOnEditErrors = true
                },
                cancellationToken));

        var error = Assert.Single(exception.Errors);

        Assert.Equal(0, error.LayerId);
        Assert.Equal(ReplicaEditOperation.Update, error.Operation);
        Assert.Equal(102, error.ObjectId);
        Assert.Equal("{22222222-2222-2222-2222-222222222222}", error.GlobalId);
        Assert.Equal(400, error.ErrorCode);
        Assert.Equal("Update failed.", error.ErrorDescription);
        Assert.Equal(["Missing required field."], error.ErrorDetails);
    }

    [Fact]
    public async Task SynchronizeReplicaStateBidirectionalAsync_ReturnsResultWithEditErrors_WhenThrowOnEditErrorsIsDisabled() {
        var cancellationToken = TestContext.Current.CancellationToken;
        const string resultUrl = "https://example.test/output/sync.json";

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
                  "replicaName": "Replica A",
                  "responseType": "esriReplicaResponseTypeEdits",
                  "replicaServerGen": 25,
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

        var result = await client.SynchronizeReplicaStateBidirectionalAsync(
            new ReplicaSynchronizationState {
                ReplicaId = "replica-1",
                ReplicaName = "Replica A",
                SyncModel = SynchronizeReplicaSyncModel.PerReplica,
                ReplicaServerGen = 10
            },
            new SynchronizeReplicaStateBidirectionalRequest {
                EditsJson = """{"layers":[]}""",
                ThrowOnEditErrors = false
            },
            cancellationToken);

        Assert.Equal(25, result.UpdatedState.ReplicaServerGen);
        Assert.True(result.JsonResult.HasEditErrors);

        var error = Assert.Single(result.JsonResult.GetLayerEditErrors());

        Assert.Equal(0, error.LayerId);
        Assert.Equal(ReplicaEditOperation.Update, error.Operation);
        Assert.Equal(102, error.ObjectId);
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