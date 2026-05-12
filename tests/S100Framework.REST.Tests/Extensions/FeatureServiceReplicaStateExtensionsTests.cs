using S100Framework.REST.Extensions;
using S100Framework.REST.Models;
using Xunit;

namespace S100Framework.REST.Tests.Extensions;

public sealed class FeatureServiceReplicaStateExtensionsTests
{
    [Fact]
    public void ToSynchronizationState_CreatesPerReplicaStateFromCreateReplicaResult() {
        var result = new CreateReplicaResult(
            ReplicaName: "Replica A",
            ReplicaId: "replica-1",
            TransportType: "esriTransportTypeURL",
            ResponseType: "esriReplicaResponseTypeData",
            SyncModel: "perReplica",
            TargetType: "client",
            ReplicaServerGen: 10,
            LayerServerGens: [],
            ResultUrl: null,
            Status: null,
            SubmissionTime: null,
            LastUpdatedTime: null);

        var state = result.ToSynchronizationState(CreateReplicaSyncModel.PerReplica);

        Assert.Equal("replica-1", state.ReplicaId);
        Assert.Equal("Replica A", state.ReplicaName);
        Assert.Equal(SynchronizeReplicaSyncModel.PerReplica, state.SyncModel);
        Assert.Equal(10, state.ReplicaServerGen);
        Assert.Empty(state.LayerServerGens);
    }

    [Fact]
    public void ToSynchronizationState_CreatesPerLayerStateFromCreateReplicaResult() {
        var result = new CreateReplicaResult(
            ReplicaName: "Replica A",
            ReplicaId: "replica-1",
            TransportType: "esriTransportTypeURL",
            ResponseType: "esriReplicaResponseTypeData",
            SyncModel: "perLayer",
            TargetType: "client",
            ReplicaServerGen: null,
            LayerServerGens: [
                new CreateReplicaLayerServerGen(0, 20),
                new CreateReplicaLayerServerGen(1, 21)
            ],
            ResultUrl: null,
            Status: null,
            SubmissionTime: null,
            LastUpdatedTime: null);

        var state = result.ToSynchronizationState(CreateReplicaSyncModel.PerLayer);

        Assert.Equal("replica-1", state.ReplicaId);
        Assert.Equal("Replica A", state.ReplicaName);
        Assert.Equal(SynchronizeReplicaSyncModel.PerLayer, state.SyncModel);
        Assert.Null(state.ReplicaServerGen);

        Assert.Collection(
            state.LayerServerGens,
            first => {
                Assert.Equal(0, first.Id);
                Assert.Equal(20, first.ServerGen);
            },
            second => {
                Assert.Equal(1, second.Id);
                Assert.Equal(21, second.ServerGen);
            });
    }

    [Fact]
    public void ToSynchronizationState_Throws_WhenSyncModelIsNone() {
        var result = CreatePerReplicaResult();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            result.ToSynchronizationState(CreateReplicaSyncModel.None));

        Assert.Contains("SyncModel None", exception.Message);
    }

    [Fact]
    public void ToSynchronizationState_Throws_WhenReplicaIdIsMissing() {
        var result = CreatePerReplicaResult() with {
            ReplicaId = " "
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            result.ToSynchronizationState(CreateReplicaSyncModel.PerReplica));

        Assert.Contains("replica ID", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ToSynchronizationState_Throws_WhenPerReplicaGenerationIsMissing() {
        var result = CreatePerReplicaResult() with {
            ReplicaServerGen = null
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            result.ToSynchronizationState(CreateReplicaSyncModel.PerReplica));

        Assert.Contains("ReplicaServerGen", exception.Message);
    }

    [Fact]
    public void UpdateFrom_UpdatesPerReplicaGeneration() {
        var state = new ReplicaSynchronizationState {
            ReplicaId = "replica-1",
            ReplicaName = "Replica A",
            SyncModel = SynchronizeReplicaSyncModel.PerReplica,
            ReplicaServerGen = 10
        };

        var result = new SynchronizeReplicaResult(
            ReplicaId: null,
            ReplicaName: null,
            TransportType: "esriTransportTypeURL",
            ResponseType: "esriReplicaResponseTypeEdits",
            ReplicaServerGen: 30,
            LayerServerGens: [],
            ResultUrl: null,
            Status: null,
            SubmissionTime: null,
            LastUpdatedTime: null);

        var updatedState = state.UpdateFrom(result);

        Assert.Equal("replica-1", updatedState.ReplicaId);
        Assert.Equal("Replica A", updatedState.ReplicaName);
        Assert.Equal(30, updatedState.ReplicaServerGen);
        Assert.Empty(updatedState.LayerServerGens);
    }

    [Fact]
    public void UpdateFrom_UpdatesPerLayerGenerations() {
        var state = new ReplicaSynchronizationState {
            ReplicaId = "replica-1",
            ReplicaName = "Replica A",
            SyncModel = SynchronizeReplicaSyncModel.PerLayer,
            LayerServerGens = [
                new ReplicaLayerServerGeneration(0, 20),
                new ReplicaLayerServerGeneration(1, 21)
            ]
        };

        var result = new SynchronizeReplicaResult(
            ReplicaId: "replica-1",
            ReplicaName: "Replica A",
            TransportType: "esriTransportTypeURL",
            ResponseType: "esriReplicaResponseTypeEdits",
            ReplicaServerGen: null,
            LayerServerGens: [
                new SynchronizeReplicaLayerServerGen(0, 30),
                new SynchronizeReplicaLayerServerGen(1, 31)
            ],
            ResultUrl: null,
            Status: null,
            SubmissionTime: null,
            LastUpdatedTime: null);

        var updatedState = state.UpdateFrom(result);

        Assert.Equal("replica-1", updatedState.ReplicaId);
        Assert.Equal("Replica A", updatedState.ReplicaName);
        Assert.Null(updatedState.ReplicaServerGen);

        Assert.Collection(
            updatedState.LayerServerGens,
            first => {
                Assert.Equal(0, first.Id);
                Assert.Equal(30, first.ServerGen);
            },
            second => {
                Assert.Equal(1, second.Id);
                Assert.Equal(31, second.ServerGen);
            });
    }

    [Fact]
    public void UpdateFrom_Throws_WhenPerLayerResultDoesNotContainLayerServerGens() {
        var state = new ReplicaSynchronizationState {
            ReplicaId = "replica-1",
            SyncModel = SynchronizeReplicaSyncModel.PerLayer,
            LayerServerGens = [
                new ReplicaLayerServerGeneration(0, 20)
            ]
        };

        var result = new SynchronizeReplicaResult(
            ReplicaId: null,
            ReplicaName: null,
            TransportType: "esriTransportTypeURL",
            ResponseType: "esriReplicaResponseTypeEdits",
            ReplicaServerGen: null,
            LayerServerGens: [],
            ResultUrl: null,
            Status: null,
            SubmissionTime: null,
            LastUpdatedTime: null);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            state.UpdateFrom(result));

        Assert.Contains("LayerServerGens", exception.Message);
    }

    private static CreateReplicaResult CreatePerReplicaResult() {
        return new CreateReplicaResult(
            ReplicaName: "Replica A",
            ReplicaId: "replica-1",
            TransportType: "esriTransportTypeURL",
            ResponseType: "esriReplicaResponseTypeData",
            SyncModel: "perReplica",
            TargetType: "client",
            ReplicaServerGen: 10,
            LayerServerGens: [],
            ResultUrl: null,
            Status: null,
            SubmissionTime: null,
            LastUpdatedTime: null);
    }
}