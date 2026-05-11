using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureServiceClientUnregisterReplicaTests
{
    [Fact]
    public async Task UnregisterReplicaAsync_SendsExpectedRequestAndMapsSuccess() {
        var cancellationToken = TestContext.Current.CancellationToken;
        string? requestBody = null;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateSyncMetadataResponse();
            }

            if (IsUnregisterReplicaRequest(request)) {
                requestBody = request.Content?.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult();

                return StubHttpMessageHandler.Json("""
                {
                  "success": true
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var result = await client.UnregisterReplicaAsync(
            new UnregisterReplicaRequest {
                ReplicaId = "A849811F-6FDF-4AEC-9DD0-3E3DF7142603"
            },
            cancellationToken);

        Assert.True(result.Success);
        Assert.Equal("A849811F-6FDF-4AEC-9DD0-3E3DF7142603", result.ReplicaId);

        Assert.NotNull(requestBody);
        Assert.Contains("f=json", requestBody);
        Assert.Contains("replicaID=A849811F-6FDF-4AEC-9DD0-3E3DF7142603", requestBody);
    }

    [Fact]
    public async Task UnregisterReplicaAsync_AllowsWildcardReplicaId() {
        var cancellationToken = TestContext.Current.CancellationToken;
        string? requestBody = null;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateSyncMetadataResponse();
            }

            if (IsUnregisterReplicaRequest(request)) {
                requestBody = request.Content?.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult();

                return StubHttpMessageHandler.Json("""
                {
                  "success": true
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var result = await client.UnregisterReplicaAsync(
            new UnregisterReplicaRequest {
                ReplicaId = "*"
            },
            cancellationToken);

        Assert.True(result.Success);
        Assert.Equal("*", result.ReplicaId);

        Assert.NotNull(requestBody);
        Assert.Contains("replicaID=%2A", requestBody);
    }

    [Fact]
    public async Task UnregisterReplicaAsync_MapsFalseSuccessValue() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateSyncMetadataResponse();
            }

            if (IsUnregisterReplicaRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "success": false
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var result = await client.UnregisterReplicaAsync(
            new UnregisterReplicaRequest {
                ReplicaId = "A849811F-6FDF-4AEC-9DD0-3E3DF7142603"
            },
            cancellationToken);

        Assert.False(result.Success);
        Assert.Equal("A849811F-6FDF-4AEC-9DD0-3E3DF7142603", result.ReplicaId);
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