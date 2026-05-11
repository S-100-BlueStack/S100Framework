using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Exceptions;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureServiceClientUnregisterReplicaHardeningTests
{
    [Fact]
    public async Task UnregisterReplicaAsync_ValidatesRequestBeforeMetadataLookup() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var wasCalled = false;

        var client = CreateClient(_ => {
            wasCalled = true;
            return StubHttpMessageHandler.Json("{}");
        });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.UnregisterReplicaAsync(
                new UnregisterReplicaRequest {
                    ReplicaId = "   "
                },
                cancellationToken));

        Assert.Contains("ReplicaId", exception.Message);
        Assert.False(wasCalled);
    }

    [Fact]
    public async Task UnregisterReplicaAsync_Throws_WhenSyncIsNotSupported() {
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

            throw new InvalidOperationException("The unRegisterReplica endpoint should not be called.");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceCapabilityException>(() =>
            client.UnregisterReplicaAsync(
                new UnregisterReplicaRequest {
                    ReplicaId = "A849811F-6FDF-4AEC-9DD0-3E3DF7142603"
                },
                cancellationToken));

        Assert.Contains("sync", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnregisterReplicaAsync_Throws_WhenSuccessValueIsMissing() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateSyncMetadataResponse();
            }

            if (IsUnregisterReplicaRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceException>(() =>
            client.UnregisterReplicaAsync(
                new UnregisterReplicaRequest {
                    ReplicaId = "A849811F-6FDF-4AEC-9DD0-3E3DF7142603"
                },
                cancellationToken));

        Assert.Contains("success", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnregisterReplicaAsync_ThrowsMappedEsriErrorPayload() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateSyncMetadataResponse();
            }

            if (IsUnregisterReplicaRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "error": {
                    "code": 400,
                    "message": "Replica not found.",
                    "details": [
                      "The replica ID does not exist."
                    ]
                  }
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceException>(() =>
            client.UnregisterReplicaAsync(
                new UnregisterReplicaRequest {
                    ReplicaId = "missing-replica"
                },
                cancellationToken));

        Assert.Equal(400, exception.ErrorCode);
        Assert.Contains("Replica not found", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("The replica ID does not exist.", exception.Details);
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

    private static bool IsUnregisterReplicaRequest(HttpRequestMessage request) {
        return request.Method == HttpMethod.Post &&
               request.RequestUri?.AbsoluteUri ==
               "https://example.test/arcgis/rest/services/Test/FeatureServer/unRegisterReplica";
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