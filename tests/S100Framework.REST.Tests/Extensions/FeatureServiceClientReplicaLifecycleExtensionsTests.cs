using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Extensions;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Extensions;

public sealed class FeatureServiceClientReplicaLifecycleExtensionsTests
{
    [Fact]
    public async Task UnregisterReplicaStateAsync_SendsReplicaIdFromState() {
        var cancellationToken = TestContext.Current.CancellationToken;
        string? requestBody = null;

        var client = CreateClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;

            if (uri == "https://example.test/arcgis/rest/services/Test/FeatureServer?f=json") {
                return CreateSyncMetadataResponse();
            }

            if (uri == "https://example.test/arcgis/rest/services/Test/FeatureServer/unRegisterReplica") {
                requestBody = request.Content?.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult();

                return StubHttpMessageHandler.Json("""
                {
                  "success": true
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request URI: {uri}");
        });

        var result = await client.UnregisterReplicaStateAsync(
            new ReplicaSynchronizationState {
                ReplicaId = "replica-1",
                SyncModel = SynchronizeReplicaSyncModel.PerReplica,
                ReplicaServerGen = 10
            },
            cancellationToken);

        Assert.True(result.Success);
        Assert.Equal("replica-1", result.ReplicaId);

        Assert.NotNull(requestBody);
        Assert.Contains("f=json", requestBody);
        Assert.Contains("replicaID=replica-1", requestBody);
    }

    [Fact]
    public async Task UnregisterReplicaStateAsync_ValidatesStateBeforeHttp() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var wasCalled = false;

        var client = CreateClient(_ => {
            wasCalled = true;
            return StubHttpMessageHandler.Json("{}");
        });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.UnregisterReplicaStateAsync(
                new ReplicaSynchronizationState {
                    ReplicaId = " ",
                    SyncModel = SynchronizeReplicaSyncModel.PerReplica,
                    ReplicaServerGen = 10
                },
                cancellationToken));

        Assert.Contains("ReplicaId", exception.Message);
        Assert.False(wasCalled);
    }

    [Fact]
    public async Task UnregisterReplicaStateAsync_RejectsWildcardReplicaIdBeforeHttp() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var wasCalled = false;

        var client = CreateClient(_ => {
            wasCalled = true;
            return StubHttpMessageHandler.Json("{}");
        });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.UnregisterReplicaStateAsync(
                new ReplicaSynchronizationState {
                    ReplicaId = "*",
                    SyncModel = SynchronizeReplicaSyncModel.PerReplica,
                    ReplicaServerGen = 10
                },
                cancellationToken));

        Assert.Contains("concrete replica ID", exception.Message);
        Assert.Contains("UnregisterReplicaAsync directly", exception.Message);
        Assert.False(wasCalled);
    }

    [Fact]
    public async Task UnregisterReplicaStateAsync_PropagatesFalseSuccessValue() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;

            if (uri == "https://example.test/arcgis/rest/services/Test/FeatureServer?f=json") {
                return CreateSyncMetadataResponse();
            }

            if (uri == "https://example.test/arcgis/rest/services/Test/FeatureServer/unRegisterReplica") {
                return StubHttpMessageHandler.Json("""
                {
                  "success": false
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request URI: {uri}");
        });

        var result = await client.UnregisterReplicaStateAsync(
            new ReplicaSynchronizationState {
                ReplicaId = "replica-1",
                SyncModel = SynchronizeReplicaSyncModel.PerReplica,
                ReplicaServerGen = 10
            },
            cancellationToken);

        Assert.False(result.Success);
        Assert.Equal("replica-1", result.ReplicaId);
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