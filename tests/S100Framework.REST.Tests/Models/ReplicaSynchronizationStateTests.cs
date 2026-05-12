using S100Framework.REST.Models;
using Xunit;

namespace S100Framework.REST.Tests.Models;

public sealed class ReplicaSynchronizationStateTests
{
    [Fact]
    public void Validate_DoesNotThrow_ForValidPerReplicaState() {
        var state = CreatePerReplicaState();

        state.Validate();
    }

    [Fact]
    public void Validate_DoesNotThrow_ForValidPerLayerState() {
        var state = CreatePerLayerState();

        state.Validate();
    }

    [Fact]
    public void Validate_Throws_WhenReplicaIdIsMissing() {
        var state = CreatePerReplicaState() with {
            ReplicaId = " "
        };

        var exception = Assert.Throws<InvalidOperationException>(() => state.Validate());

        Assert.Contains("ReplicaId", exception.Message);
    }

    [Fact]
    public void Validate_Throws_WhenPerReplicaStateHasNoReplicaServerGen() {
        var state = CreatePerReplicaState() with {
            ReplicaServerGen = null
        };

        var exception = Assert.Throws<InvalidOperationException>(() => state.Validate());

        Assert.Contains("ReplicaServerGen", exception.Message);
    }

    [Fact]
    public void Validate_Throws_WhenPerReplicaStateHasLayerServerGens() {
        var state = CreatePerReplicaState() with {
            LayerServerGens = [
                new ReplicaLayerServerGeneration(0, 20)
            ]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => state.Validate());

        Assert.Contains("LayerServerGens", exception.Message);
    }

    [Fact]
    public void Validate_Throws_WhenPerLayerStateHasNoLayerServerGens() {
        var state = CreatePerLayerState() with {
            LayerServerGens = []
        };

        var exception = Assert.Throws<InvalidOperationException>(() => state.Validate());

        Assert.Contains("LayerServerGens", exception.Message);
    }

    [Fact]
    public void Validate_Throws_WhenPerLayerStateHasReplicaServerGen() {
        var state = CreatePerLayerState() with {
            ReplicaServerGen = 10
        };

        var exception = Assert.Throws<InvalidOperationException>(() => state.Validate());

        Assert.Contains("ReplicaServerGen", exception.Message);
    }

    [Fact]
    public void Validate_Throws_WhenLayerServerGensContainDuplicateLayerIds() {
        var state = CreatePerLayerState() with {
            LayerServerGens = [
                new ReplicaLayerServerGeneration(0, 20),
                new ReplicaLayerServerGeneration(0, 21)
            ]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => state.Validate());

        Assert.Contains("duplicate", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ToDownloadRequest_CreatesPerReplicaDownloadRequest() {
        var state = CreatePerReplicaState();

        var request = state.ToDownloadRequest(
            dataFormat: SynchronizeReplicaDataFormat.Sqlite,
            transportType: SynchronizeReplicaTransportType.Url,
            isAsync: true,
            returnAttachmentsDataByUrl: true,
            closeReplica: true);

        Assert.Equal("replica-1", request.ReplicaId);
        Assert.Equal(SynchronizeReplicaSyncModel.PerReplica, request.SyncModel);
        Assert.Equal(10, request.ReplicaServerGen);
        Assert.Empty(request.SyncLayers);
        Assert.Equal(SynchronizeReplicaDataFormat.Sqlite, request.DataFormat);
        Assert.Equal(SynchronizeReplicaTransportType.Url, request.TransportType);
        Assert.True(request.IsAsync);
        Assert.True(request.ReturnAttachmentsDataByUrl);
        Assert.True(request.CloseReplica);
    }

    [Fact]
    public void ToDownloadRequest_CreatesPerLayerDownloadRequest() {
        var state = CreatePerLayerState();

        var request = state.ToDownloadRequest();

        Assert.Equal("replica-1", request.ReplicaId);
        Assert.Equal(SynchronizeReplicaSyncModel.PerLayer, request.SyncModel);
        Assert.Null(request.ReplicaServerGen);

        Assert.Collection(
            request.SyncLayers,
            first => {
                Assert.Equal(0, first.Id);
                Assert.Equal(20, first.ServerGen);
                Assert.Equal(SynchronizeReplicaSyncDirection.Download, first.SyncDirection);
            },
            second => {
                Assert.Equal(1, second.Id);
                Assert.Equal(21, second.ServerGen);
                Assert.Equal(SynchronizeReplicaSyncDirection.Download, second.SyncDirection);
            });
    }

    private static ReplicaSynchronizationState CreatePerReplicaState() {
        return new ReplicaSynchronizationState {
            ReplicaId = "replica-1",
            ReplicaName = "Replica A",
            SyncModel = SynchronizeReplicaSyncModel.PerReplica,
            ReplicaServerGen = 10
        };
    }

    private static ReplicaSynchronizationState CreatePerLayerState() {
        return new ReplicaSynchronizationState {
            ReplicaId = "replica-1",
            ReplicaName = "Replica A",
            SyncModel = SynchronizeReplicaSyncModel.PerLayer,
            LayerServerGens = [
                new ReplicaLayerServerGeneration(0, 20),
                new ReplicaLayerServerGeneration(1, 21)
            ]
        };
    }
}