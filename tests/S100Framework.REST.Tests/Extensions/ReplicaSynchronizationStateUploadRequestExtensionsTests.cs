using S100Framework.REST.Extensions;
using S100Framework.REST.Models;
using Xunit;

namespace S100Framework.REST.Tests.Extensions;

public sealed class ReplicaSynchronizationStateUploadRequestExtensionsTests
{
    [Fact]
    public void ToUploadRequest_BuildsPerReplicaUploadRequestWithStructuredEdits() {
        var state = new ReplicaSynchronizationState {
            ReplicaId = "replica-1",
            ReplicaName = "Replica A",
            SyncModel = SynchronizeReplicaSyncModel.PerReplica,
            ReplicaServerGen = 10
        };

        var request = state.ToUploadRequest(
            new SynchronizeReplicaStateUploadRequest {
                Edits = CreateEdits(),
                IsAsync = true,
                ReturnAttachmentsDataByUrl = true,
                RollbackOnFailure = true,
                ReturnIdsForAdds = true
            });

        Assert.Equal("replica-1", request.ReplicaId);
        Assert.Equal(SynchronizeReplicaSyncModel.PerReplica, request.SyncModel);
        Assert.Equal(SynchronizeReplicaSyncDirection.Upload, request.SyncDirection);
        Assert.Null(request.ReplicaServerGen);
        Assert.Empty(request.SyncLayers);
        Assert.Equal(SynchronizeReplicaTransportType.Url, request.TransportType);
        Assert.Equal(SynchronizeReplicaDataFormat.Json, request.DataFormat);
        Assert.True(request.IsAsync);
        Assert.True(request.ReturnAttachmentsDataByUrl);
        Assert.True(request.RollbackOnFailure);
        Assert.True(request.ReturnIdsForAdds);
        Assert.NotNull(request.EditsJson);
        Assert.Null(request.EditsUploadId);

        request.Validate();
    }

    [Fact]
    public void ToUploadRequest_BuildsPerLayerUploadRequestWithoutServerGens() {
        var state = new ReplicaSynchronizationState {
            ReplicaId = "replica-1",
            SyncModel = SynchronizeReplicaSyncModel.PerLayer,
            LayerServerGens = [
                new ReplicaLayerServerGeneration(0, 20),
                new ReplicaLayerServerGeneration(1, 21)
            ]
        };

        var request = state.ToUploadRequest(
            new SynchronizeReplicaStateUploadRequest {
                EditsUploadId = "upload-1"
            });

        Assert.Equal(SynchronizeReplicaSyncModel.PerLayer, request.SyncModel);
        Assert.Equal(SynchronizeReplicaSyncDirection.Upload, request.SyncDirection);
        Assert.Null(request.ReplicaServerGen);
        Assert.Equal("upload-1", request.EditsUploadId);
        Assert.Null(request.EditsJson);

        Assert.Collection(
            request.SyncLayers,
            first => {
                Assert.Equal(0, first.Id);
                Assert.Null(first.ServerGen);
                Assert.Equal(SynchronizeReplicaSyncDirection.Upload, first.SyncDirection);
            },
            second => {
                Assert.Equal(1, second.Id);
                Assert.Null(second.ServerGen);
                Assert.Equal(SynchronizeReplicaSyncDirection.Upload, second.SyncDirection);
            });

        request.Validate();
    }

    [Fact]
    public void ToBidirectionalRequest_BuildsPerReplicaBidirectionalRequestWithGeneration() {
        var state = new ReplicaSynchronizationState {
            ReplicaId = "replica-1",
            ReplicaName = "Replica A",
            SyncModel = SynchronizeReplicaSyncModel.PerReplica,
            ReplicaServerGen = 10
        };

        var request = state.ToBidirectionalRequest(
            new SynchronizeReplicaStateBidirectionalRequest {
                EditsJson = """{"layers":[]}""",
                IsAsync = true,
                ReturnAttachmentsDataByUrl = true,
                RollbackOnFailure = true,
                ReturnIdsForAdds = true
            });

        Assert.Equal("replica-1", request.ReplicaId);
        Assert.Equal(SynchronizeReplicaSyncModel.PerReplica, request.SyncModel);
        Assert.Equal(SynchronizeReplicaSyncDirection.Bidirectional, request.SyncDirection);
        Assert.Equal(10, request.ReplicaServerGen);
        Assert.Empty(request.SyncLayers);
        Assert.Equal(SynchronizeReplicaTransportType.Url, request.TransportType);
        Assert.Equal(SynchronizeReplicaDataFormat.Json, request.DataFormat);
        Assert.True(request.IsAsync);
        Assert.True(request.ReturnAttachmentsDataByUrl);
        Assert.True(request.RollbackOnFailure);
        Assert.True(request.ReturnIdsForAdds);
        Assert.Equal("""{"layers":[]}""", request.EditsJson);
        Assert.Null(request.EditsUploadId);

        request.Validate();
    }

    [Fact]
    public void ToBidirectionalRequest_BuildsPerLayerBidirectionalRequestWithServerGens() {
        var state = new ReplicaSynchronizationState {
            ReplicaId = "replica-1",
            SyncModel = SynchronizeReplicaSyncModel.PerLayer,
            LayerServerGens = [
                new ReplicaLayerServerGeneration(0, 20),
                new ReplicaLayerServerGeneration(1, 21)
            ]
        };

        var request = state.ToBidirectionalRequest(
            new SynchronizeReplicaStateBidirectionalRequest {
                EditsUploadId = "upload-1"
            });

        Assert.Equal(SynchronizeReplicaSyncModel.PerLayer, request.SyncModel);
        Assert.Equal(SynchronizeReplicaSyncDirection.Bidirectional, request.SyncDirection);
        Assert.Null(request.ReplicaServerGen);
        Assert.Equal("upload-1", request.EditsUploadId);
        Assert.Null(request.EditsJson);

        Assert.Collection(
            request.SyncLayers,
            first => {
                Assert.Equal(0, first.Id);
                Assert.Equal(20, first.ServerGen);
                Assert.Equal(SynchronizeReplicaSyncDirection.Bidirectional, first.SyncDirection);
            },
            second => {
                Assert.Equal(1, second.Id);
                Assert.Equal(21, second.ServerGen);
                Assert.Equal(SynchronizeReplicaSyncDirection.Bidirectional, second.SyncDirection);
            });

        request.Validate();
    }

    [Fact]
    public void ToUploadRequest_ValidatesStateBeforeBuildingRequest() {
        var state = new ReplicaSynchronizationState {
            ReplicaId = " ",
            SyncModel = SynchronizeReplicaSyncModel.PerReplica,
            ReplicaServerGen = 10
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            state.ToUploadRequest(
                new SynchronizeReplicaStateUploadRequest {
                    EditsJson = """{"layers":[]}"""
                }));

        Assert.Contains("ReplicaId", exception.Message);
    }

    [Fact]
    public void ToBidirectionalRequest_ValidatesRequestBeforeBuildingRequest() {
        var state = new ReplicaSynchronizationState {
            ReplicaId = "replica-1",
            SyncModel = SynchronizeReplicaSyncModel.PerReplica,
            ReplicaServerGen = 10
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            state.ToBidirectionalRequest(
                new SynchronizeReplicaStateBidirectionalRequest()));

        Assert.Contains("requires Edits, EditsJson, or EditsUploadId", exception.Message);
    }

    private static ReplicaEdits CreateEdits() {
        return new ReplicaEdits {
            Layers = [
                new ReplicaLayerEdits {
                    Id = 0,
                    AddsJson = "[]"
                }
            ]
        };
    }
}