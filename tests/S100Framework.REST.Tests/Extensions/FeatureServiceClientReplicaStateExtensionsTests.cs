using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Extensions;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Xunit;

namespace S100Framework.REST.Tests.Extensions;

public sealed class FeatureServiceClientReplicaStateExtensionsTests
{
    [Fact]
    public async Task SynchronizeReplicaStateAsync_SubmitsImmediatePerReplicaSyncDownloadsFileAndUpdatesState() {
        var cancellationToken = TestContext.Current.CancellationToken;
        const string resultUrl = "https://example.test/output/sync.json";
        var expectedContent = new byte[] { 1, 2, 3, 4 };
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
                  "replicaID": "replica-1",
                  "replicaName": "Replica A",
                  "transportType": "esriTransportTypeURL",
                  "responseType": "esriReplicaResponseTypeEdits",
                  "replicaServerGen": 20,
                  "URL": "{{resultUrl}}"
                }
                """);
            }

            if (uri == resultUrl) {
                var response = new HttpResponseMessage(HttpStatusCode.OK) {
                    Content = new ByteArrayContent(expectedContent)
                };

                response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                return response;
            }

            throw new InvalidOperationException($"Unexpected request URI: {uri}");
        });

        var state = new ReplicaSynchronizationState {
            ReplicaId = "replica-1",
            ReplicaName = "Replica A",
            SyncModel = SynchronizeReplicaSyncModel.PerReplica,
            ReplicaServerGen = 10
        };

        var result = await client.SynchronizeReplicaStateAsync(
            state,
            cancellationToken: cancellationToken);

        Assert.Equal(expectedContent, result.File.Content);
        Assert.Equal("application/json", result.File.ContentType);
        Assert.Equal(new Uri(resultUrl), result.File.ResultUrl);
        Assert.Equal("replica-1", result.UpdatedState.ReplicaId);
        Assert.Equal("Replica A", result.UpdatedState.ReplicaName);
        Assert.Equal(SynchronizeReplicaSyncModel.PerReplica, result.UpdatedState.SyncModel);
        Assert.Equal(20, result.UpdatedState.ReplicaServerGen);
        Assert.Empty(result.UpdatedState.LayerServerGens);
        Assert.Equal(20, result.SynchronizationResult.ReplicaServerGen);

        Assert.NotNull(requestBody);
        Assert.Contains("replicaID=replica-1", requestBody);
        Assert.Contains("syncModel=perReplica", requestBody);
        Assert.Contains("replicaServerGen=10", requestBody);
        Assert.Contains("syncDirection=download", requestBody);
        Assert.Contains("transportType=esriTransportTypeURL", requestBody);
    }

    [Fact]
    public async Task SynchronizeReplicaStateAsync_SubmitsImmediatePerLayerSyncDownloadsFileAndUpdatesState() {
        var cancellationToken = TestContext.Current.CancellationToken;
        const string resultUrl = "https://example.test/output/sync.json";
        var expectedContent = new byte[] { 5, 6, 7, 8 };
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
                  "replicaID": "replica-1",
                  "replicaName": "Replica A",
                  "transportType": "esriTransportTypeURL",
                  "responseType": "esriReplicaResponseTypeEdits",
                  "layerServerGens": [
                    { "id": 0, "serverGen": 30 },
                    { "id": 1, "serverGen": 31 }
                  ],
                  "URL": "{{resultUrl}}"
                }
                """);
            }

            if (uri == resultUrl) {
                return new HttpResponseMessage(HttpStatusCode.OK) {
                    Content = new ByteArrayContent(expectedContent)
                };
            }

            throw new InvalidOperationException($"Unexpected request URI: {uri}");
        });

        var state = new ReplicaSynchronizationState {
            ReplicaId = "replica-1",
            ReplicaName = "Replica A",
            SyncModel = SynchronizeReplicaSyncModel.PerLayer,
            LayerServerGens = [
                new ReplicaLayerServerGeneration(0, 20),
                new ReplicaLayerServerGeneration(1, 21)
            ]
        };

        var result = await client.SynchronizeReplicaStateAsync(
            state,
            cancellationToken: cancellationToken);

        Assert.Equal(expectedContent, result.File.Content);
        Assert.Equal(new Uri(resultUrl), result.File.ResultUrl);
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
        Assert.Contains("syncModel=perLayer", requestBody);
        Assert.Contains("syncLayers=", requestBody);
    }

    [Fact]
    public async Task SynchronizeReplicaStateAsync_ThrowsBeforeHttp_WhenTransportIsEmbedded() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var wasCalled = false;

        var client = CreateClient(_ => {
            wasCalled = true;
            return StubHttpMessageHandler.Json("{}");
        });

        var state = new ReplicaSynchronizationState {
            ReplicaId = "replica-1",
            SyncModel = SynchronizeReplicaSyncModel.PerReplica,
            ReplicaServerGen = 10
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.SynchronizeReplicaStateAsync(
                state,
                transportType: SynchronizeReplicaTransportType.Embedded,
                cancellationToken: cancellationToken));

        Assert.Contains("TransportType.Url", exception.Message);
        Assert.False(wasCalled);
    }

    [Fact]
    public async Task SynchronizeReplicaStateAsync_Throws_WhenImmediateResultHasNoResultUrl() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;

            if (uri == "https://example.test/arcgis/rest/services/Test/FeatureServer?f=json") {
                return CreateSyncMetadataResponse();
            }

            if (uri == "https://example.test/arcgis/rest/services/Test/FeatureServer/synchronizeReplica") {
                return StubHttpMessageHandler.Json("""
                {
                  "replicaID": "replica-1",
                  "replicaName": "Replica A",
                  "replicaServerGen": 20
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request URI: {uri}");
        });

        var state = new ReplicaSynchronizationState {
            ReplicaId = "replica-1",
            ReplicaName = "Replica A",
            SyncModel = SynchronizeReplicaSyncModel.PerReplica,
            ReplicaServerGen = 10
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.SynchronizeReplicaStateAsync(
                state,
                cancellationToken: cancellationToken));

        Assert.Contains("result URL", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SynchronizeReplicaStateAsync_Throws_WhenImmediateResultCannotUpdateState() {
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
                  "replicaID": "replica-1",
                  "replicaName": "Replica A",
                  "URL": "{{resultUrl}}"
                }
                """);
            }

            if (uri == resultUrl) {
                return new HttpResponseMessage(HttpStatusCode.OK) {
                    Content = new ByteArrayContent(Encoding.UTF8.GetBytes("""
    {
    }
    """))
                };
            }

            throw new InvalidOperationException($"Unexpected request URI: {uri}");
        });

        var state = new ReplicaSynchronizationState {
            ReplicaId = "replica-1",
            SyncModel = SynchronizeReplicaSyncModel.PerReplica,
            ReplicaServerGen = 10
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.SynchronizeReplicaStateAsync(
                state,
                cancellationToken: cancellationToken));

        Assert.Contains("ReplicaServerGen", exception.Message);
    }

    [Fact]
    public async Task SynchronizeReplicaStateAsync_UpdatesPerReplicaStateFromAsyncJsonResultFile() {
        var cancellationToken = TestContext.Current.CancellationToken;
        const string statusUrl = "https://example.test/arcgis/rest/services/Test/FeatureServer/jobs/sync-1/status";
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
              "statusUrl": "{{statusUrl}}"
            }
            """);
            }

            if (uri == statusUrl) {
                return StubHttpMessageHandler.Json($$"""
            {
              "status": "Completed",
              "responseType": "esriReplicaResponseTypeEdits",
              "transportType": "esriTransportTypeURL",
              "resultUrl": "{{resultUrl}}"
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
                  "replicaServerGen": 25
                }
                """))
                };
            }

            throw new InvalidOperationException($"Unexpected request URI: {uri}");
        });

        var state = new ReplicaSynchronizationState {
            ReplicaId = "replica-1",
            ReplicaName = "Replica A",
            SyncModel = SynchronizeReplicaSyncModel.PerReplica,
            ReplicaServerGen = 10
        };

        var result = await client.SynchronizeReplicaStateAsync(
            state,
            isAsync: true,
            pollingOptions: new ReplicaPollingOptions {
                PollInterval = TimeSpan.FromMilliseconds(1),
                Timeout = TimeSpan.FromSeconds(1)
            },
            cancellationToken: cancellationToken);

        Assert.Equal(new Uri(resultUrl), result.File.ResultUrl);
        Assert.Equal("replica-1", result.UpdatedState.ReplicaId);
        Assert.Equal("Replica A", result.UpdatedState.ReplicaName);
        Assert.Equal(SynchronizeReplicaSyncModel.PerReplica, result.UpdatedState.SyncModel);
        Assert.Equal(25, result.UpdatedState.ReplicaServerGen);
        Assert.Empty(result.UpdatedState.LayerServerGens);
        Assert.Equal(25, result.SynchronizationResult.ReplicaServerGen);

        Assert.NotNull(requestBody);
        Assert.Contains("async=true", requestBody);
        Assert.Contains("dataFormat=json", requestBody);
    }

    [Fact]
    public async Task SynchronizeReplicaStateAsync_UpdatesPerLayerStateFromImmediateJsonResultFile() {
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
                  "layerServerGens": [
                    null,
                    { "id": "0", "serverGen": "30" },
                    { "id": 1, "serverGen": 31 }
                  ]
                }
                """))
                };
            }

            throw new InvalidOperationException($"Unexpected request URI: {uri}");
        });

        var state = new ReplicaSynchronizationState {
            ReplicaId = "replica-1",
            ReplicaName = "Replica A",
            SyncModel = SynchronizeReplicaSyncModel.PerLayer,
            LayerServerGens = [
                new ReplicaLayerServerGeneration(0, 20),
            new ReplicaLayerServerGeneration(1, 21)
            ]
        };

        var result = await client.SynchronizeReplicaStateAsync(
            state,
            cancellationToken: cancellationToken);

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

        Assert.Collection(
            result.SynchronizationResult.LayerServerGens,
            first => {
                Assert.Equal(0, first.Id);
                Assert.Equal(30, first.ServerGen);
            },
            second => {
                Assert.Equal(1, second.Id);
                Assert.Equal(31, second.ServerGen);
            });
    }

    [Fact]
    public async Task SynchronizeReplicaStateAsync_Throws_WhenSqliteResultDoesNotExposeGenerationValues() {
        var cancellationToken = TestContext.Current.CancellationToken;
        const string resultUrl = "https://example.test/output/sync.geodatabase";

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
                    Content = new ByteArrayContent([1, 2, 3, 4])
                };
            }

            throw new InvalidOperationException($"Unexpected request URI: {uri}");
        });

        var state = new ReplicaSynchronizationState {
            ReplicaId = "replica-1",
            ReplicaName = "Replica A",
            SyncModel = SynchronizeReplicaSyncModel.PerReplica,
            ReplicaServerGen = 10
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.SynchronizeReplicaStateAsync(
                state,
                dataFormat: SynchronizeReplicaDataFormat.Sqlite,
                cancellationToken: cancellationToken));

        Assert.Contains("generation values", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("non-JSON", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SynchronizeReplicaStateAsync_ThrowsBeforeHttp_WhenCloseReplicaIsTrue() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var wasCalled = false;

        var client = CreateClient(_ => {
            wasCalled = true;
            return StubHttpMessageHandler.Json("{}");
        });

        var state = new ReplicaSynchronizationState {
            ReplicaId = "replica-1",
            SyncModel = SynchronizeReplicaSyncModel.PerReplica,
            ReplicaServerGen = 10
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.SynchronizeReplicaStateAsync(
                state,
                closeReplica: true,
                cancellationToken: cancellationToken));

        Assert.Contains("cannot close the replica", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("future use", exception.Message, StringComparison.OrdinalIgnoreCase);
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
            "supportsAsync": true
          }
        }
        """);
    }
}