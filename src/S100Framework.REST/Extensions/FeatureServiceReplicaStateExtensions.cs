using S100Framework.REST.Models;

namespace S100Framework.REST.Extensions;

/// <summary>
/// Provides helpers for creating and updating persisted replica synchronization state.
/// </summary>
public static class FeatureServiceReplicaStateExtensions
{
    /// <summary>
    /// Creates a persisted synchronization state from a completed <c>createReplica</c> result.
    /// </summary>
    /// <param name="result">
    /// The completed create replica result.
    /// </param>
    /// <param name="syncModel">
    /// The sync model used when the replica was created.
    /// </param>
    /// <returns>
    /// A synchronization state that can be persisted by the consumer and used to build future synchronize requests.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="result" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the result does not contain the required generation information for the selected sync model.
    /// </exception>
    public static ReplicaSynchronizationState ToSynchronizationState(
        this CreateReplicaResult result,
        CreateReplicaSyncModel syncModel) {
        ArgumentNullException.ThrowIfNull(result);

        if (!Enum.IsDefined(syncModel)) {
            throw new InvalidOperationException("SyncModel must be a supported createReplica sync model.");
        }

        if (syncModel == CreateReplicaSyncModel.None) {
            throw new InvalidOperationException(
                "A createReplica result with SyncModel None cannot be converted to synchronization state.");
        }

        if (string.IsNullOrWhiteSpace(result.ReplicaId)) {
            throw new InvalidOperationException(
                "The createReplica result does not contain a replica ID.");
        }

        var state = syncModel switch {
            CreateReplicaSyncModel.PerReplica => new ReplicaSynchronizationState {
                ReplicaId = result.ReplicaId,
                ReplicaName = string.IsNullOrWhiteSpace(result.ReplicaName) ? null : result.ReplicaName,
                SyncModel = SynchronizeReplicaSyncModel.PerReplica,
                ReplicaServerGen = result.ReplicaServerGen
            },
            CreateReplicaSyncModel.PerLayer => new ReplicaSynchronizationState {
                ReplicaId = result.ReplicaId,
                ReplicaName = string.IsNullOrWhiteSpace(result.ReplicaName) ? null : result.ReplicaName,
                SyncModel = SynchronizeReplicaSyncModel.PerLayer,
                LayerServerGens = result.LayerServerGens
                    .Select(static value => new ReplicaLayerServerGeneration(value.Id, value.ServerGen))
                    .ToArray()
            },
            _ => throw new ArgumentOutOfRangeException(nameof(syncModel), syncModel, null)
        };

        state.Validate();

        return state;
    }

    /// <summary>
    /// Creates a new synchronization state by applying a completed <c>synchronizeReplica</c> result to an existing state.
    /// </summary>
    /// <param name="state">
    /// The existing persisted synchronization state.
    /// </param>
    /// <param name="result">
    /// The completed synchronize replica result.
    /// </param>
    /// <returns>
    /// A new synchronization state containing the updated generation values returned by the service.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="state" /> or <paramref name="result" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the result does not contain the required generation information for the existing sync model.
    /// </exception>
    public static ReplicaSynchronizationState UpdateFrom(
        this ReplicaSynchronizationState state,
        SynchronizeReplicaResult result) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(result);

        state.Validate();

        var replicaId = string.IsNullOrWhiteSpace(result.ReplicaId)
            ? state.ReplicaId
            : result.ReplicaId;

        var replicaName = string.IsNullOrWhiteSpace(result.ReplicaName)
            ? state.ReplicaName
            : result.ReplicaName;

        var updatedState = state.SyncModel switch {
            SynchronizeReplicaSyncModel.PerReplica => state with {
                ReplicaId = replicaId,
                ReplicaName = replicaName,
                ReplicaServerGen = result.ReplicaServerGen
            },
            SynchronizeReplicaSyncModel.PerLayer => state with {
                ReplicaId = replicaId,
                ReplicaName = replicaName,
                LayerServerGens = result.LayerServerGens
                    .Select(static value => new ReplicaLayerServerGeneration(value.Id, value.ServerGen))
                    .ToArray()
            },
            _ => throw new ArgumentOutOfRangeException(nameof(state), state.SyncModel, null)
        };

        updatedState.Validate();

        return updatedState;
    }
}