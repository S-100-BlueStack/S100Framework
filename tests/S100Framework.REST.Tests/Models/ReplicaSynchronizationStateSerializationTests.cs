using System.Text.Json;
using S100Framework.REST.Models;
using Xunit;

namespace S100Framework.REST.Tests.Models;

public sealed class ReplicaSynchronizationStateSerializationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    [Fact]
    public void SerializeAndDeserialize_PreservesPerReplicaState() {
        var state = new ReplicaSynchronizationState {
            ReplicaId = "replica-1",
            ReplicaName = "Replica A",
            SyncModel = SynchronizeReplicaSyncModel.PerReplica,
            ReplicaServerGen = 123456789
        };

        var json = JsonSerializer.Serialize(state, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<ReplicaSynchronizationState>(json, JsonOptions);

        Assert.NotNull(deserialized);
        Assert.Equal("replica-1", deserialized!.ReplicaId);
        Assert.Equal("Replica A", deserialized.ReplicaName);
        Assert.Equal(SynchronizeReplicaSyncModel.PerReplica, deserialized.SyncModel);
        Assert.Equal(123456789, deserialized.ReplicaServerGen);
        Assert.Empty(deserialized.LayerServerGens);

        deserialized.Validate();
    }

    [Fact]
    public void SerializeAndDeserialize_PreservesPerLayerState() {
        var state = new ReplicaSynchronizationState {
            ReplicaId = "replica-1",
            ReplicaName = "Replica A",
            SyncModel = SynchronizeReplicaSyncModel.PerLayer,
            LayerServerGens = [
                new ReplicaLayerServerGeneration(0, 123),
                new ReplicaLayerServerGeneration(1, 456)
            ]
        };

        var json = JsonSerializer.Serialize(state, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<ReplicaSynchronizationState>(json, JsonOptions);

        Assert.NotNull(deserialized);
        Assert.Equal("replica-1", deserialized!.ReplicaId);
        Assert.Equal("Replica A", deserialized.ReplicaName);
        Assert.Equal(SynchronizeReplicaSyncModel.PerLayer, deserialized.SyncModel);
        Assert.Null(deserialized.ReplicaServerGen);

        Assert.Collection(
            deserialized.LayerServerGens,
            first => {
                Assert.Equal(0, first.Id);
                Assert.Equal(123, first.ServerGen);
            },
            second => {
                Assert.Equal(1, second.Id);
                Assert.Equal(456, second.ServerGen);
            });

        deserialized.Validate();
    }

    [Fact]
    public void Deserialize_AllowsPersistedStateWithoutReplicaName() {
        var state = JsonSerializer.Deserialize<ReplicaSynchronizationState>(
            """
            {
              "replicaId": "replica-1",
              "syncModel": "PerReplica",
              "replicaServerGen": 123456789
            }
            """,
            JsonOptions);

        Assert.NotNull(state);
        Assert.Equal("replica-1", state!.ReplicaId);
        Assert.Null(state.ReplicaName);
        Assert.Equal(SynchronizeReplicaSyncModel.PerReplica, state.SyncModel);
        Assert.Equal(123456789, state.ReplicaServerGen);
        Assert.Empty(state.LayerServerGens);

        state.Validate();
    }

    [Fact]
    public void Deserialize_PreservesInvalidStateForValidationToReject() {
        var state = JsonSerializer.Deserialize<ReplicaSynchronizationState>(
            """
            {
              "replicaId": "replica-1",
              "syncModel": "PerLayer",
              "layerServerGens": [
                { "id": 0, "serverGen": 123 },
                { "id": 0, "serverGen": 456 }
              ]
            }
            """,
            JsonOptions);

        Assert.NotNull(state);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            state!.Validate());

        Assert.Contains("duplicate", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Deserialize_SupportsNumericEnumValuesForDefaultSystemTextJsonCompatibility() {
        var state = JsonSerializer.Deserialize<ReplicaSynchronizationState>(
            """
            {
              "replicaId": "replica-1",
              "syncModel": 0,
              "replicaServerGen": 123456789
            }
            """,
            JsonOptions);

        Assert.NotNull(state);
        Assert.Equal(SynchronizeReplicaSyncModel.PerReplica, state!.SyncModel);
        Assert.Equal(123456789, state.ReplicaServerGen);

        state.Validate();
    }

    [Fact]
    public void Serialize_UsesStringEnumForSyncModel() {
        var state = new ReplicaSynchronizationState {
            ReplicaId = "replica-1",
            SyncModel = SynchronizeReplicaSyncModel.PerReplica,
            ReplicaServerGen = 123456789
        };

        var json = JsonSerializer.Serialize(state, JsonOptions);

        Assert.Contains("\"syncModel\":\"PerReplica\"", json);
    }
}