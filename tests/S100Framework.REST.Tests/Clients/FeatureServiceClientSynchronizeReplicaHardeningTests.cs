using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Exceptions;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureServiceClientSynchronizeReplicaHardeningTests
{
    [Fact]
    public async Task SubmitSynchronizeReplicaAsync_ValidatesRequestBeforeMetadataLookup() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var wasCalled = false;

        var client = CreateClient(_ => {
            wasCalled = true;
            return StubHttpMessageHandler.Json("{}");
        });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.SubmitSynchronizeReplicaAsync(
                new SynchronizeReplicaRequest {
                    ReplicaId = "   ",
                    ReplicaServerGen = 1
                },
                cancellationToken));

        Assert.Contains("ReplicaId", exception.Message);
        Assert.False(wasCalled);
    }

    [Fact]
    public async Task SubmitSynchronizeReplicaAsync_Throws_WhenSyncIsNotSupported() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "layers": [
                    { "id": 0, "name": "Layer 0" }
                  ],
                  "tables": [],
                  "capabilities": "Query",
                  "syncEnabled": false
                }
                """);
            }

            throw new InvalidOperationException("The synchronizeReplica endpoint should not be called.");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceCapabilityException>(() =>
            client.SubmitSynchronizeReplicaAsync(
                CreateValidRequest(),
                cancellationToken));

        Assert.Contains("sync", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SubmitSynchronizeReplicaAsync_Throws_WhenSyncCapabilitiesAreMissing() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "layers": [
                    { "id": 0, "name": "Layer 0" }
                  ],
                  "tables": [],
                  "capabilities": "Create,Update,Delete,Query,Sync",
                  "syncEnabled": true
                }
                """);
            }

            throw new InvalidOperationException("The synchronizeReplica endpoint should not be called.");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceCapabilityException>(() =>
            client.SubmitSynchronizeReplicaAsync(
                CreateValidRequest(),
                cancellationToken));

        Assert.Contains("sync capabilities", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SubmitSynchronizeReplicaAsync_Throws_WhenAsyncIsNotSupported() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
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
                    "supportsAsync": false
                  }
                }
                """);
            }

            throw new InvalidOperationException("The synchronizeReplica endpoint should not be called.");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceCapabilityException>(() =>
            client.SubmitSynchronizeReplicaAsync(
                CreateValidRequest() with {
                    IsAsync = true
                },
                cancellationToken));

        Assert.Contains("asynchronous", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SubmitSynchronizeReplicaAsync_Throws_WhenStatusUrlIsInvalid() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateSyncMetadataResponse();
            }

            if (IsSynchronizeReplicaRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "statusUrl": "not an absolute URL"
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceException>(() =>
            client.SubmitSynchronizeReplicaAsync(
                CreateValidRequest() with {
                    IsAsync = true
                },
                cancellationToken));

        Assert.Contains("statusUrl", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SubmitSynchronizeReplicaAsync_IgnoresNullLayerServerGensAndParsesStringNumbers() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateSyncMetadataResponse();
            }

            if (IsSynchronizeReplicaRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "transportType": "esriTransportTypeEmbedded",
                  "responseType": "esriReplicaResponseTypeEdits",
                  "replicaServerGen": "1526605677000",
                  "submissionTime": "1653614927000",
                  "lastUpdatedTime": "1653614930000",
                  "layerServerGens": [
                    null,
                    { "id": "0", "serverGen": "1526605677436" }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var submission = await client.SubmitSynchronizeReplicaAsync(
            CreateValidRequest() with {
                SyncDirection = SynchronizeReplicaSyncDirection.Snapshot,
                ReplicaServerGen = null,
                TransportType = SynchronizeReplicaTransportType.Embedded,
                DataFormat = SynchronizeReplicaDataFormat.Json
            },
            cancellationToken);

        var result = Assert.IsType<SynchronizeReplicaResult>(submission.Result);
        var layerServerGen = Assert.Single(result.LayerServerGens);

        Assert.Equal(1526605677000, result.ReplicaServerGen);
        Assert.Equal(1653614927000, result.SubmissionTime);
        Assert.Equal(1653614930000, result.LastUpdatedTime);
        Assert.Equal(0, layerServerGen.Id);
        Assert.Equal(1526605677436, layerServerGen.ServerGen);
    }

    [Fact]
    public async Task SubmitSynchronizeReplicaAsync_Throws_WhenLayerServerGenIdIsMissing() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateSyncMetadataResponse();
            }

            if (IsSynchronizeReplicaRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "layerServerGens": [
                    { "serverGen": 1526605677436 }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceException>(() =>
            client.SubmitSynchronizeReplicaAsync(
                CreateValidRequest(),
                cancellationToken));

        Assert.Contains("layer ID", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SubmitSynchronizeReplicaAsync_Throws_WhenLayerServerGenIsNegative() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateSyncMetadataResponse();
            }

            if (IsSynchronizeReplicaRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "layerServerGens": [
                    { "id": 0, "serverGen": -1 }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceException>(() =>
            client.SubmitSynchronizeReplicaAsync(
                CreateValidRequest(),
                cancellationToken));

        Assert.Contains("negative serverGen", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetSynchronizeReplicaStatusAsync_Throws_WhenResultUrlIsInvalid() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(_ =>
            StubHttpMessageHandler.Json("""
            {
              "status": "Completed",
              "resultUrl": "not an absolute URL"
            }
            """));

        var exception = await Assert.ThrowsAsync<FeatureServiceException>(() =>
            client.GetSynchronizeReplicaStatusAsync(
                new Uri("https://example.test/jobs/j123"),
                cancellationToken));

        Assert.Contains("resultUrl", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SubmitSynchronizeReplicaAsync_Throws_WhenPerReplicaSyncIsNotSupported() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return StubHttpMessageHandler.Json("""
            {
              "layers": [
                { "id": 0, "name": "Layer 0" }
              ],
              "tables": [],
              "capabilities": "Create,Update,Delete,Query,Sync",
              "syncEnabled": true,
              "syncCapabilities": {
                "supportsPerReplicaSync": false,
                "supportsPerLayerSync": true,
                "supportsSyncModelNone": true,
                "supportsAsync": true
              }
            }
            """);
            }

            throw new InvalidOperationException("The synchronizeReplica endpoint should not be called.");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceCapabilityException>(() =>
            client.SubmitSynchronizeReplicaAsync(
                CreateValidRequest(),
                cancellationToken));

        Assert.Contains("perReplica", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SubmitSynchronizeReplicaAsync_Throws_WhenPerLayerSyncIsNotSupported() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
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
                "supportsPerLayerSync": false,
                "supportsSyncModelNone": true,
                "supportsAsync": true
              }
            }
            """);
            }

            throw new InvalidOperationException("The synchronizeReplica endpoint should not be called.");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceCapabilityException>(() =>
            client.SubmitSynchronizeReplicaAsync(
                new SynchronizeReplicaRequest {
                    ReplicaId = "replica-1",
                    SyncModel = SynchronizeReplicaSyncModel.PerLayer,
                    SyncLayers = [
                        new SynchronizeReplicaSyncLayer {
                        Id = 0,
                        ServerGen = 1526605677436
                    }
                    ]
                },
                cancellationToken));

        Assert.Contains("perLayer", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static SynchronizeReplicaRequest CreateValidRequest() {
        return new SynchronizeReplicaRequest {
            ReplicaId = "replica-1",
            ReplicaServerGen = 1526605677436
        };
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