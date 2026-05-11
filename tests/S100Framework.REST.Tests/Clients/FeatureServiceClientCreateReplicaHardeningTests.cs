using NetTopologySuite.Geometries;
using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Exceptions;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureServiceClientCreateReplicaHardeningTests
{
    [Fact]
    public async Task SubmitCreateReplicaAsync_ValidatesRequestBeforeMetadataLookup() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var wasCalled = false;

        var client = CreateClient(_ => {
            wasCalled = true;
            return StubHttpMessageHandler.Json("{}");
        });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.SubmitCreateReplicaAsync(
                new CreateReplicaRequest {
                    Layers = []
                },
                cancellationToken));

        Assert.Contains("At least one layer ID", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(wasCalled);
    }

    [Fact]
    public async Task SubmitCreateReplicaAsync_Throws_WhenSyncIsNotSupported() {
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

            throw new InvalidOperationException("createReplica endpoint should not be called.");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceCapabilityException>(() =>
            client.SubmitCreateReplicaAsync(
                CreateValidRequest(),
                cancellationToken));

        Assert.Contains("sync", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SubmitCreateReplicaAsync_Throws_WhenSyncCapabilitiesAreMissing() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "layers": [
                    { "id": 0, "name": "Layer 0" }
                  ],
                  "tables": [],
                  "capabilities": "Query,Sync",
                  "syncEnabled": true
                }
                """);
            }

            throw new InvalidOperationException("createReplica endpoint should not be called.");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceCapabilityException>(() =>
            client.SubmitCreateReplicaAsync(
                CreateValidRequest(),
                cancellationToken));

        Assert.Contains("sync capabilities", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SubmitCreateReplicaAsync_Throws_WhenRequestedSyncModelIsNotSupported() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "layers": [
                    { "id": 0, "name": "Layer 0" }
                  ],
                  "tables": [],
                  "capabilities": "Query,Sync",
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

            throw new InvalidOperationException("createReplica endpoint should not be called.");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceCapabilityException>(() =>
            client.SubmitCreateReplicaAsync(
                CreateValidRequest() with {
                    SyncModel = CreateReplicaSyncModel.PerLayer
                },
                cancellationToken));

        Assert.Contains("perLayer", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SubmitCreateReplicaAsync_Throws_WhenAsyncIsNotSupported() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "layers": [
                    { "id": 0, "name": "Layer 0" }
                  ],
                  "tables": [],
                  "capabilities": "Query,Sync",
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

            throw new InvalidOperationException("createReplica endpoint should not be called.");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceCapabilityException>(() =>
            client.SubmitCreateReplicaAsync(
                CreateValidRequest() with {
                    IsAsync = true
                },
                cancellationToken));

        Assert.Contains("asynchronous", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SubmitCreateReplicaAsync_Throws_WhenStatusUrlIsInvalid() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateSyncMetadataResponse();
            }

            if (IsCreateReplicaRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "statusUrl": "not an absolute URL"
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceException>(() =>
            client.SubmitCreateReplicaAsync(
                CreateValidRequest() with {
                    IsAsync = true
                },
                cancellationToken));

        Assert.Contains("statusUrl", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SubmitCreateReplicaAsync_IgnoresNullLayerServerGensAndParsesStringNumbers() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateSyncMetadataResponse();
            }

            if (IsCreateReplicaRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "transportType": "esriTransportTypeEmbedded",
                  "responseType": "esriReplicaResponseTypeData",
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

        var submission = await client.SubmitCreateReplicaAsync(
            CreateValidRequest() with {
                TransportType = CreateReplicaTransportType.Embedded,
                DataFormat = CreateReplicaDataFormat.Json
            },
            cancellationToken);

        var result = Assert.IsType<CreateReplicaResult>(submission.Result);
        var layerServerGen = Assert.Single(result.LayerServerGens);

        Assert.Equal(1526605677000, result.ReplicaServerGen);
        Assert.Equal(1653614927000, result.SubmissionTime);
        Assert.Equal(1653614930000, result.LastUpdatedTime);
        Assert.Equal(0, layerServerGen.Id);
        Assert.Equal(1526605677436, layerServerGen.ServerGen);
    }

    [Fact]
    public async Task SubmitCreateReplicaAsync_Throws_WhenLayerServerGenIdIsMissing() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateSyncMetadataResponse();
            }

            if (IsCreateReplicaRequest(request)) {
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
            client.SubmitCreateReplicaAsync(
                CreateValidRequest(),
                cancellationToken));

        Assert.Contains("layer ID", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SubmitCreateReplicaAsync_Throws_WhenLayerServerGenIsNegative() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateSyncMetadataResponse();
            }

            if (IsCreateReplicaRequest(request)) {
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
            client.SubmitCreateReplicaAsync(
                CreateValidRequest(),
                cancellationToken));

        Assert.Contains("negative serverGen", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetCreateReplicaStatusAsync_Throws_WhenResultUrlIsInvalid() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(_ =>
            StubHttpMessageHandler.Json("""
            {
              "status": "Completed",
              "resultUrl": "not an absolute URL"
            }
            """));

        var exception = await Assert.ThrowsAsync<FeatureServiceException>(() =>
            client.GetCreateReplicaStatusAsync(
                new Uri("https://example.test/jobs/j123"),
                cancellationToken));

        Assert.Contains("resultUrl", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static CreateReplicaRequest CreateValidRequest() {
        return new CreateReplicaRequest {
            Layers = [0],
            SpatialFilter = FeatureSpatialFilter.FromEnvelope(
                new Envelope(10, 20, 30, 40),
                inSrid: 4326)
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

    private static bool IsCreateReplicaRequest(HttpRequestMessage request) {
        return request.Method == HttpMethod.Post &&
               request.RequestUri?.AbsoluteUri ==
               "https://example.test/arcgis/rest/services/Test/FeatureServer/createReplica";
    }

    private static HttpResponseMessage CreateSyncMetadataResponse() {
        return StubHttpMessageHandler.Json("""
        {
          "layers": [
            { "id": 0, "name": "Layer 0" }
          ],
          "tables": [],
          "capabilities": "Query,Sync",
          "syncEnabled": true,
          "syncCapabilities": {
            "supportsPerReplicaSync": true,
            "supportsPerLayerSync": true,
            "supportsSyncModelNone": true,
            "supportsAsync": true
          },
          "supportedExportFormats": ["json", "sqlite"]
        }
        """);
    }
}