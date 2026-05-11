using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Exceptions;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureServiceClientReplicasHardeningTests
{
    [Fact]
    public async Task GetReplicasAsync_ValidatesRequestBeforeMetadataLookup() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var wasCalled = false;

        var client = CreateClient(_ => {
            wasCalled = true;
            return StubHttpMessageHandler.Json("{}");
        });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetReplicasAsync(
                new FeatureServiceReplicasRequest {
                    ReplicaVersion = "   "
                },
                cancellationToken));

        Assert.Contains("ReplicaVersion", exception.Message);
        Assert.False(wasCalled);
    }

    [Fact]
    public async Task GetReplicasAsync_Throws_WhenSyncIsNotSupported() {
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

            throw new InvalidOperationException("The replicas endpoint should not be called.");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceCapabilityException>(() =>
            client.GetReplicasAsync(cancellationToken: cancellationToken));

        Assert.Contains("sync", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetReplicasAsync_IgnoresNullArrayItems() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateSyncMetadataResponse();
            }

            if (IsReplicasRequest(request)) {
                return StubHttpMessageHandler.Json("""
                [
                  null,
                  {
                    "replicaName": "Replica A",
                    "replicaID": "A849811F-6FDF-4AEC-9DD0-3E3DF7142603"
                  }
                ]
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var result = await client.GetReplicasAsync(cancellationToken: cancellationToken);

        var replica = Assert.Single(result.Replicas);
        Assert.Equal("Replica A", replica.ReplicaName);
        Assert.Equal("A849811F-6FDF-4AEC-9DD0-3E3DF7142603", replica.ReplicaId);
    }

    [Fact]
    public async Task GetReplicasAsync_Throws_WhenReplicaNameIsMissing() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateSyncMetadataResponse();
            }

            if (IsReplicasRequest(request)) {
                return StubHttpMessageHandler.Json("""
                [
                  {
                    "replicaID": "A849811F-6FDF-4AEC-9DD0-3E3DF7142603"
                  }
                ]
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceException>(() =>
            client.GetReplicasAsync(cancellationToken: cancellationToken));

        Assert.Contains("replicaName", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetReplicasAsync_Throws_WhenReplicaIdIsMissing() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateSyncMetadataResponse();
            }

            if (IsReplicasRequest(request)) {
                return StubHttpMessageHandler.Json("""
                [
                  {
                    "replicaName": "Replica A"
                  }
                ]
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceException>(() =>
            client.GetReplicasAsync(cancellationToken: cancellationToken));

        Assert.Contains("replicaID", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetReplicasAsync_Throws_WhenLastSyncDateIsInvalid() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateSyncMetadataResponse();
            }

            if (IsReplicasRequest(request)) {
                return StubHttpMessageHandler.Json("""
                [
                  {
                    "replicaName": "Replica A",
                    "replicaID": "A849811F-6FDF-4AEC-9DD0-3E3DF7142603",
                    "lastSyncDate": "not a number"
                  }
                ]
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceException>(() =>
            client.GetReplicasAsync(
                new FeatureServiceReplicasRequest {
                    ReturnLastSyncDate = true
                },
                cancellationToken));

        Assert.Contains("lastSyncDate", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetReplicasAsync_Throws_WhenLastSyncDateIsNegative() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateSyncMetadataResponse();
            }

            if (IsReplicasRequest(request)) {
                return StubHttpMessageHandler.Json("""
                [
                  {
                    "replicaName": "Replica A",
                    "replicaID": "A849811F-6FDF-4AEC-9DD0-3E3DF7142603",
                    "lastSyncDate": -1
                  }
                ]
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceException>(() =>
            client.GetReplicasAsync(
                new FeatureServiceReplicasRequest {
                    ReturnLastSyncDate = true
                },
                cancellationToken));

        Assert.Contains("negative", exception.Message, StringComparison.OrdinalIgnoreCase);
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

    private static bool IsReplicasRequest(HttpRequestMessage request) {
        return request.Method == HttpMethod.Get &&
               request.RequestUri?.AbsolutePath.EndsWith("/replicas", StringComparison.Ordinal) == true;
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
          }
        }
        """);
    }
}