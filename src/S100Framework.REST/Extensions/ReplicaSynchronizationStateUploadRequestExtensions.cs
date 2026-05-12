using S100Framework.REST.Models;

namespace S100Framework.REST.Extensions;

/// <summary>
/// Provides helpers for building upload and bidirectional synchronize replica requests from persisted state.
/// </summary>
public static class ReplicaSynchronizationStateUploadRequestExtensions
{
    /// <summary>
    /// Builds an upload-only <c>synchronizeReplica</c> request from persisted replica state and upload options.
    /// </summary>
    /// <param name="state">
    /// The persisted replica synchronization state.
    /// </param>
    /// <param name="request">
    /// The upload-only workflow options.
    /// </param>
    /// <returns>
    /// A fully populated upload-only synchronize replica request.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="state" /> or <paramref name="request" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when state or request options are invalid.
    /// </exception>
    public static SynchronizeReplicaRequest ToUploadRequest(
        this ReplicaSynchronizationState state,
        SynchronizeReplicaStateUploadRequest request) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(request);

        state.Validate();
        request.Validate();

        var editsJson = request.Edits is null
            ? request.EditsJson
            : request.Edits.ToJson(request.EditsJsonOptions);

        return state.SyncModel switch {
            SynchronizeReplicaSyncModel.PerReplica => new SynchronizeReplicaRequest {
                ReplicaId = state.ReplicaId,
                SyncModel = SynchronizeReplicaSyncModel.PerReplica,
                SyncDirection = SynchronizeReplicaSyncDirection.Upload,
                TransportType = SynchronizeReplicaTransportType.Url,
                DataFormat = SynchronizeReplicaDataFormat.Json,
                IsAsync = request.IsAsync,
                ReturnAttachmentsDataByUrl = request.ReturnAttachmentsDataByUrl,
                RollbackOnFailure = request.RollbackOnFailure,
                ReturnIdsForAdds = request.ReturnIdsForAdds,
                EditsJson = editsJson,
                EditsUploadId = request.EditsUploadId
            },
            SynchronizeReplicaSyncModel.PerLayer => new SynchronizeReplicaRequest {
                ReplicaId = state.ReplicaId,
                SyncModel = SynchronizeReplicaSyncModel.PerLayer,
                SyncLayers = state.LayerServerGens
                    .Select(static value => new SynchronizeReplicaSyncLayer {
                        Id = value.Id,
                        SyncDirection = SynchronizeReplicaSyncDirection.Upload
                    })
                    .ToArray(),
                SyncDirection = SynchronizeReplicaSyncDirection.Upload,
                TransportType = SynchronizeReplicaTransportType.Url,
                DataFormat = SynchronizeReplicaDataFormat.Json,
                IsAsync = request.IsAsync,
                ReturnAttachmentsDataByUrl = request.ReturnAttachmentsDataByUrl,
                RollbackOnFailure = request.RollbackOnFailure,
                ReturnIdsForAdds = request.ReturnIdsForAdds,
                EditsJson = editsJson,
                EditsUploadId = request.EditsUploadId
            },
            _ => throw new ArgumentOutOfRangeException(nameof(state), state.SyncModel, null)
        };
    }

    /// <summary>
    /// Builds a bidirectional <c>synchronizeReplica</c> request from persisted replica state and bidirectional options.
    /// </summary>
    /// <param name="state">
    /// The persisted replica synchronization state.
    /// </param>
    /// <param name="request">
    /// The bidirectional workflow options.
    /// </param>
    /// <returns>
    /// A fully populated bidirectional synchronize replica request.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="state" /> or <paramref name="request" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when state or request options are invalid.
    /// </exception>
    public static SynchronizeReplicaRequest ToBidirectionalRequest(
        this ReplicaSynchronizationState state,
        SynchronizeReplicaStateBidirectionalRequest request) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(request);

        state.Validate();
        request.Validate();

        var editsJson = request.Edits is null
            ? request.EditsJson
            : request.Edits.ToJson(request.EditsJsonOptions);

        return state.SyncModel switch {
            SynchronizeReplicaSyncModel.PerReplica => new SynchronizeReplicaRequest {
                ReplicaId = state.ReplicaId,
                SyncModel = SynchronizeReplicaSyncModel.PerReplica,
                ReplicaServerGen = state.ReplicaServerGen,
                SyncDirection = SynchronizeReplicaSyncDirection.Bidirectional,
                TransportType = SynchronizeReplicaTransportType.Url,
                DataFormat = SynchronizeReplicaDataFormat.Json,
                IsAsync = request.IsAsync,
                ReturnAttachmentsDataByUrl = request.ReturnAttachmentsDataByUrl,
                RollbackOnFailure = request.RollbackOnFailure,
                ReturnIdsForAdds = request.ReturnIdsForAdds,
                EditsJson = editsJson,
                EditsUploadId = request.EditsUploadId
            },
            SynchronizeReplicaSyncModel.PerLayer => new SynchronizeReplicaRequest {
                ReplicaId = state.ReplicaId,
                SyncModel = SynchronizeReplicaSyncModel.PerLayer,
                SyncLayers = state.LayerServerGens
                    .Select(static value => new SynchronizeReplicaSyncLayer {
                        Id = value.Id,
                        ServerGen = value.ServerGen,
                        SyncDirection = SynchronizeReplicaSyncDirection.Bidirectional
                    })
                    .ToArray(),
                SyncDirection = SynchronizeReplicaSyncDirection.Bidirectional,
                TransportType = SynchronizeReplicaTransportType.Url,
                DataFormat = SynchronizeReplicaDataFormat.Json,
                IsAsync = request.IsAsync,
                ReturnAttachmentsDataByUrl = request.ReturnAttachmentsDataByUrl,
                RollbackOnFailure = request.RollbackOnFailure,
                ReturnIdsForAdds = request.ReturnIdsForAdds,
                EditsJson = editsJson,
                EditsUploadId = request.EditsUploadId
            },
            _ => throw new ArgumentOutOfRangeException(nameof(state), state.SyncModel, null)
        };
    }
}