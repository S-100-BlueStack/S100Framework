using System.Text.Json.Serialization;

namespace S100Framework.REST.Models;

/// <summary>
/// Represents persisted state required to continue synchronizing a feature service replica.
/// </summary>
/// <remarks>
/// Consumers should persist this model after <c>createReplica</c> and update it after each successful
/// <c>synchronizeReplica</c> call. The model intentionally stores only generic replica identifiers and
/// generation values, so it remains independent of any storage, authentication, or application workflow.
/// </remarks>
public sealed record ReplicaSynchronizationState
{
    /// <summary>
    /// Gets the replica ID returned by the feature service.
    /// </summary>
    public required string ReplicaId { get; init; }

    /// <summary>
    /// Gets the optional replica name returned by the feature service.
    /// </summary>
    public string? ReplicaName { get; init; }

    /// <summary>
    /// Gets the sync model used by the replica.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<SynchronizeReplicaSyncModel>))]
    public SynchronizeReplicaSyncModel SyncModel { get; init; } = SynchronizeReplicaSyncModel.PerReplica;

    /// <summary>
    /// Gets the replica-level server generation used for per-replica synchronization.
    /// </summary>
    public long? ReplicaServerGen { get; init; }

    /// <summary>
    /// Gets layer-level server generations used for per-layer synchronization.
    /// </summary>
    public IReadOnlyList<ReplicaLayerServerGeneration> LayerServerGens { get; init; } =
        Array.Empty<ReplicaLayerServerGeneration>();

    /// <summary>
    /// Builds a download-only <c>synchronizeReplica</c> request from the current persisted state.
    /// </summary>
    /// <param name="dataFormat">
    /// The requested synchronization data format.
    /// </param>
    /// <param name="transportType">
    /// The requested transport type.
    /// </param>
    /// <param name="isAsync">
    /// A value indicating whether the request should be submitted asynchronously.
    /// </param>
    /// <param name="returnAttachmentsDataByUrl">
    /// A value indicating whether attachment content should be referenced by URL instead of embedded.
    /// </param>
    /// <param name="closeReplica">
    /// A value indicating whether the replica should be closed after synchronization.
    /// </param>
    /// <returns>
    /// A download-only synchronize replica request.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the state is incomplete or internally inconsistent.
    /// </exception>
    public SynchronizeReplicaRequest ToDownloadRequest(
        SynchronizeReplicaDataFormat dataFormat = SynchronizeReplicaDataFormat.Json,
        SynchronizeReplicaTransportType transportType = SynchronizeReplicaTransportType.Url,
        bool isAsync = false,
        bool returnAttachmentsDataByUrl = false,
        bool closeReplica = false) {
        Validate();

        if (!Enum.IsDefined(dataFormat)) {
            throw new InvalidOperationException("DataFormat must be a supported synchronizeReplica data format.");
        }

        if (!Enum.IsDefined(transportType)) {
            throw new InvalidOperationException("TransportType must be a supported synchronizeReplica transport type.");
        }

        return SyncModel switch {
            SynchronizeReplicaSyncModel.PerReplica => new SynchronizeReplicaRequest {
                ReplicaId = ReplicaId,
                SyncModel = SynchronizeReplicaSyncModel.PerReplica,
                ReplicaServerGen = ReplicaServerGen,
                SyncDirection = SynchronizeReplicaSyncDirection.Download,
                DataFormat = dataFormat,
                TransportType = transportType,
                IsAsync = isAsync,
                ReturnAttachmentsDataByUrl = returnAttachmentsDataByUrl,
                CloseReplica = closeReplica
            },
            SynchronizeReplicaSyncModel.PerLayer => new SynchronizeReplicaRequest {
                ReplicaId = ReplicaId,
                SyncModel = SynchronizeReplicaSyncModel.PerLayer,
                SyncLayers = LayerServerGens
                    .Select(static value => new SynchronizeReplicaSyncLayer {
                        Id = value.Id,
                        ServerGen = value.ServerGen,
                        SyncDirection = SynchronizeReplicaSyncDirection.Download
                    })
                    .ToArray(),
                SyncDirection = SynchronizeReplicaSyncDirection.Download,
                DataFormat = dataFormat,
                TransportType = transportType,
                IsAsync = isAsync,
                ReturnAttachmentsDataByUrl = returnAttachmentsDataByUrl,
                CloseReplica = closeReplica
            },
            _ => throw new ArgumentOutOfRangeException(nameof(SyncModel), SyncModel, null)
        };
    }

    /// <summary>
    /// Validates the synchronization state.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the state is incomplete or internally inconsistent.
    /// </exception>
    public void Validate() {
        if (string.IsNullOrWhiteSpace(ReplicaId)) {
            throw new InvalidOperationException("ReplicaId must be provided.");
        }

        if (ReplicaName is not null && string.IsNullOrWhiteSpace(ReplicaName)) {
            throw new InvalidOperationException("ReplicaName must not be empty or whitespace when provided.");
        }

        if (!Enum.IsDefined(SyncModel)) {
            throw new InvalidOperationException("SyncModel must be a supported synchronizeReplica sync model.");
        }

        if (ReplicaServerGen is < 0) {
            throw new InvalidOperationException("ReplicaServerGen must not be negative when provided.");
        }

        if (LayerServerGens.Any(static value => value is null)) {
            throw new InvalidOperationException("LayerServerGens must not contain null values.");
        }

        if (LayerServerGens.Any(static value => value.Id < 0)) {
            throw new InvalidOperationException("LayerServerGens must not contain negative layer IDs.");
        }

        if (LayerServerGens.Any(static value => value.ServerGen < 0)) {
            throw new InvalidOperationException("LayerServerGens must not contain negative server generation values.");
        }

        if (LayerServerGens.Select(static value => value.Id).Distinct().Count() != LayerServerGens.Count) {
            throw new InvalidOperationException("LayerServerGens must not contain duplicate layer IDs.");
        }

        switch (SyncModel) {
            case SynchronizeReplicaSyncModel.PerReplica:
                if (!ReplicaServerGen.HasValue) {
                    throw new InvalidOperationException(
                        "ReplicaServerGen is required when SyncModel is PerReplica.");
                }

                if (LayerServerGens.Count > 0) {
                    throw new InvalidOperationException(
                        "LayerServerGens must not be provided when SyncModel is PerReplica.");
                }

                break;

            case SynchronizeReplicaSyncModel.PerLayer:
                if (ReplicaServerGen.HasValue) {
                    throw new InvalidOperationException(
                        "ReplicaServerGen must not be provided when SyncModel is PerLayer.");
                }

                if (LayerServerGens.Count == 0) {
                    throw new InvalidOperationException(
                        "LayerServerGens must contain at least one item when SyncModel is PerLayer.");
                }

                break;
        }
    }
}