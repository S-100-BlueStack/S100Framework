using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Exceptions;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureServiceClientRemainingHardeningTests
{
    [Fact]
    public async Task GetMetadataAsync_MapsSyncCapabilitiesAndStringExportFormats() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return StubHttpMessageHandler.Json("""
            {
              "layers": [{ "id": 0, "name": "Layer 0" }],
              "tables": [],
              "capabilities": "Query,Sync",
              "supportedAppendFormats": "sqlite, feature Service",
              "supportedExportFormats": "sqlite, pbf",
              "syncCapabilities": {
                "supportsPerLayerSync": true,
                "supportsPerReplicaSync": true,
                "supportsSyncModelNone": true,
                "supportsAsync": true,
                "supportedSyncDataOptions": "3",
                "supportsQueryWithDatumTransformatiom": true
              }
            }
            """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var metadata = await client.GetMetadataAsync(cancellationToken);

        Assert.True(metadata.Capabilities.SupportsSync);
        Assert.Equal(["sqlite", "feature Service"], metadata.SupportedAppendFormats);
        Assert.Equal(["sqlite", "pbf"], metadata.SupportedExportFormats);
        Assert.NotNull(metadata.SyncCapabilities);
        Assert.True(metadata.SyncCapabilities!.SupportsPerLayerSync);
        Assert.True(metadata.SyncCapabilities.SupportsPerReplicaSync);
        Assert.True(metadata.SyncCapabilities.SupportsSyncModelNone);
        Assert.True(metadata.SyncCapabilities.SupportsAsync);
        Assert.Equal(3, metadata.SyncCapabilities.SupportedSyncDataOptions);
        Assert.True(metadata.SyncCapabilities.SupportsQueryWithDatumTransformation);
    }

    [Fact]
    public async Task QueryIdsAsync_ThrowsFeatureServiceException_WhenObjectIdsContainNegativeValue() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = CreateClient(request => {
            if (IsLayerQueryRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "objectIdFieldName": "OBJECTID",
                  "objectIds": [1, -2]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceException>(() =>
            client.QueryIdsAsync(0, new FeatureQuery(), cancellationToken));

        Assert.Contains("objectId", exception.Message, StringComparison.Ordinal);
        Assert.Contains("negative", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QueryCountAsync_ThrowsFeatureServiceException_WhenCountIsMissing() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = CreateClient(request => {
            if (IsLayerQueryRequest(request)) {
                return StubHttpMessageHandler.Json("{}");
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceException>(() =>
            client.QueryCountAsync(0, new FeatureQuery(), cancellationToken));

        Assert.Contains("query count", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("count", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QueryRelatedRecordsAsync_ThrowsFeatureServiceException_WhenCountIsNegative() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = CreateClient(request => {
            if (request.RequestUri!.AbsolutePath.EndsWith("/FeatureServer/0/queryRelatedRecords", StringComparison.OrdinalIgnoreCase)) {
                return StubHttpMessageHandler.Json("""
                {
                  "relatedRecordGroups": [
                    {
                      "objectId": 100,
                      "count": -1
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceException>(() =>
            client.QueryRelatedRecordsAsync(
                0,
                new RelatedRecordsQuery {
                    ObjectIds = [100],
                    RelationshipId = 1
                },
                returnCountOnly: true,
                cancellationToken: cancellationToken));

        Assert.Contains("queryRelatedRecords", exception.Message, StringComparison.Ordinal);
        Assert.Contains("negative", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetEstimatesAsync_ThrowsFeatureServiceException_WhenLayerEstimateHasNegativeLayerId() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = CreateClient(request => {
            if (request.RequestUri!.AbsolutePath.EndsWith("/FeatureServer/getEstimates", StringComparison.OrdinalIgnoreCase)) {
                return StubHttpMessageHandler.Json("""
                {
                  "layerEstimates": [
                    {
                      "layerId": -1,
                      "count": 1
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceException>(() =>
            client.GetEstimatesAsync(cancellationToken));

        Assert.Contains("layer estimate", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("negative", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QueryDataElementsAsync_ThrowsFeatureServiceException_WhenReturnedLayerIdIsNegative() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "layers": [{ "id": 0, "name": "Layer 0" }],
                  "tables": [],
                  "capabilities": "Query",
                  "supportsQueryDataElements": true
                }
                """);
            }

            if (request.RequestUri!.AbsolutePath.EndsWith("/FeatureServer/queryDataElements", StringComparison.OrdinalIgnoreCase)) {
                return StubHttpMessageHandler.Json("""
                {
                  "layerDataElements": [
                    {
                      "layerId": -1,
                      "dataElement": { "name": "Broken" }
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceException>(() =>
            client.QueryDataElementsAsync([0], cancellationToken));

        Assert.Contains("layer ID", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("negative", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ApplyEditsAsync_ThrowsFeatureServiceException_WhenLayerResultIdIsMissing() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = CreateClient(request => {
            if (request.RequestUri!.AbsolutePath.EndsWith("/FeatureServer/applyEdits", StringComparison.OrdinalIgnoreCase)) {
                return StubHttpMessageHandler.Json("""
                [
                  {
                    "addResults": [
                      {
                        "success": true,
                        "objectId": 1
                      }
                    ]
                  }
                ]
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceException>(() =>
            client.ApplyEditsAsync(
                new FeatureServiceEdits {
                    Layers = [
                        new ServiceLayerEdits {
                            LayerId = 0,
                            Adds = [
                                new EditableFeature(
                                    null,
                                    new Dictionary<string, object?> {
                                        ["NAME"] = "Added"
                                    })
                            ]
                        }
                    ]
                },
                cancellationToken));

        Assert.Contains("applyEdits", exception.Message, StringComparison.Ordinal);
        Assert.Contains("ID", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtractChangesRequest_Throws_WhenServerGenerationIsNegative() {
        var request = new ExtractChangesRequest {
            Layers = [0],
            ServerGens = new ExtractChangesServerGens {
                SinceServerGen = -1
            }
        };

        var exception = Assert.Throws<InvalidOperationException>(() => request.Validate());

        Assert.Contains("SinceServerGen", exception.Message, StringComparison.Ordinal);
        Assert.Contains("greater than or equal to zero", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetMetadataAsync_ThrowsFeatureServiceException_WhenSupportedSyncDataOptionsIsNegative() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return StubHttpMessageHandler.Json("""
            {
              "layers": [{ "id": 0, "name": "Layer 0" }],
              "tables": [],
              "capabilities": "Query,Sync",
              "syncCapabilities": {
                "supportsPerLayerSync": true,
                "supportedSyncDataOptions": -1
              }
            }
            """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceException>(() =>
            client.GetMetadataAsync(cancellationToken));

        Assert.Contains("supportedSyncDataOptions", exception.Message, StringComparison.Ordinal);
        Assert.Contains("negative", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetMetadataAsync_ThrowsFeatureServiceException_WhenSupportedSyncDataOptionsIsInvalid() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return StubHttpMessageHandler.Json("""
            {
              "layers": [{ "id": 0, "name": "Layer 0" }],
              "tables": [],
              "capabilities": "Query,Sync",
              "syncCapabilities": {
                "supportsPerLayerSync": true,
                "supportedSyncDataOptions": "not a number"
              }
            }
            """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceException>(() =>
            client.GetMetadataAsync(cancellationToken));

        Assert.Contains("supportedSyncDataOptions", exception.Message, StringComparison.Ordinal);
        Assert.Contains("invalid", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static FeatureServiceClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> handler) {
        return new FeatureServiceClient(
            new HttpClient(new StubHttpMessageHandler(handler)),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });
    }

    private static bool IsServiceMetadataRequest(HttpRequestMessage request) {
        return request.RequestUri?.AbsolutePath.EndsWith(
            "/FeatureServer",
            StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool IsLayerQueryRequest(HttpRequestMessage request) {
        return request.RequestUri?.AbsolutePath.EndsWith(
            "/FeatureServer/0/query",
            StringComparison.OrdinalIgnoreCase) == true;
    }
}
