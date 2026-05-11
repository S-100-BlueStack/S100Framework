using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureServiceClientReplicasTests
{
    [Fact]
    public async Task GetReplicasAsync_SendsExpectedRequestAndMapsReplicas() {
        var cancellationToken = TestContext.Current.CancellationToken;
        Uri? replicasRequestUri = null;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateSyncMetadataResponse();
            }

            if (request.RequestUri?.AbsolutePath.EndsWith("/replicas", StringComparison.Ordinal) == true) {
                replicasRequestUri = request.RequestUri;

                return StubHttpMessageHandler.Json("""
                [
                  {
                    "replicaName": "Replica A",
                    "replicaID": "A849811F-6FDF-4AEC-9DD0-3E3DF7142603",
                    "replicaVersion": "SDE.DEFAULT",
                    "lastSyncDate": 1587781611254
                  },
                  {
                    "replicaName": "Replica B",
                    "replicaID": "1FBA41AA-55B0-432A-B918-96024ECF2533",
                    "lastSyncDate": "1587781611286"
                  }
                ]
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var result = await client.GetReplicasAsync(
            new FeatureServiceReplicasRequest {
                ReplicaVersion = "SDE.DEFAULT",
                ReturnVersion = true,
                ReturnLastSyncDate = true
            },
            cancellationToken);

        Assert.NotNull(replicasRequestUri);
        Assert.Equal(
            "https://example.test/arcgis/rest/services/Test/FeatureServer/replicas?f=json&replicaVersion=SDE.DEFAULT&returnVersion=true&returnLastSyncDate=true",
            replicasRequestUri!.AbsoluteUri);

        Assert.Collection(
            result.Replicas,
            first => {
                Assert.Equal("Replica A", first.ReplicaName);
                Assert.Equal("A849811F-6FDF-4AEC-9DD0-3E3DF7142603", first.ReplicaId);
                Assert.Equal("SDE.DEFAULT", first.ReplicaVersion);
                Assert.Equal(1587781611254, first.LastSyncDate);
            },
            second => {
                Assert.Equal("Replica B", second.ReplicaName);
                Assert.Equal("1FBA41AA-55B0-432A-B918-96024ECF2533", second.ReplicaId);
                Assert.Null(second.ReplicaVersion);
                Assert.Equal(1587781611286, second.LastSyncDate);
            });
    }

    [Fact]
    public async Task GetReplicasAsync_ReturnsEmptyResult_WhenServiceReturnsEmptyArray() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateSyncMetadataResponse();
            }

            if (request.RequestUri?.AbsolutePath.EndsWith("/replicas", StringComparison.Ordinal) == true) {
                return StubHttpMessageHandler.Json("[]");
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var result = await client.GetReplicasAsync(cancellationToken: cancellationToken);

        Assert.Empty(result.Replicas);
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