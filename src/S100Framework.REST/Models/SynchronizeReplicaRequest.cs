namespace S100Framework.REST.Models;

/// <summary>
/// Defines a service-level <c>synchronizeReplica</c> request.
/// </summary>
/// <remarks>
/// This initial model intentionally supports download and snapshot scenarios only. Upload, bidirectional sync,
/// edit payloads, edit upload IDs, and rollback behavior should be added as separate API slices.
/// </remarks>
public sealed record SynchronizeReplicaRequest
{
    /// <summary>
    /// Gets the replica ID to synchronize.
    /// </summary>
    public required string ReplicaId { get; init; }

    /// <summary>
    /// Gets the sync model used by the replica.
    /// </summary>
    public SynchronizeReplicaSyncModel SyncModel { get; init; } = SynchronizeReplicaSyncModel.PerReplica;

    /// <summary>
    /// Gets the replica-level server generation.
    /// </summary>
    /// <remarks>
    /// This value is used for <see cref="SynchronizeReplicaSyncModel.PerReplica" /> download requests.
    /// </remarks>
    public long? ReplicaServerGen { get; init; }

    /// <summary>
    /// Gets the per-layer synchronization generations.
    /// </summary>
    /// <remarks>
    /// This value is used for <see cref="SynchronizeReplicaSyncModel.PerLayer" /> requests.
    /// </remarks>
    public IReadOnlyList<SynchronizeReplicaSyncLayer> SyncLayers { get; init; } = Array.Empty<SynchronizeReplicaSyncLayer>();

    /// <summary>
    /// Gets the response transport type requested from the service.
    /// </summary>
    public SynchronizeReplicaTransportType TransportType { get; init; } = SynchronizeReplicaTransportType.Url;

    /// <summary>
    /// Gets a value indicating whether the replica should be unregistered after synchronization completes.
    /// </summary>
    public bool CloseReplica { get; init; }

    /// <summary>
    /// Gets a value indicating whether attachment content should be referenced by URL instead of embedded.
    /// </summary>
    public bool ReturnAttachmentsDataByUrl { get; init; }

    /// <summary>
    /// Gets a value indicating whether the request should be submitted as an asynchronous job.
    /// </summary>
    public bool IsAsync { get; init; }

    /// <summary>
    /// Gets the replica-level synchronization direction.
    /// </summary>
    public SynchronizeReplicaSyncDirection SyncDirection { get; init; } = SynchronizeReplicaSyncDirection.Download;

    /// <summary>
    /// Gets the requested synchronization data format.
    /// </summary>
    public SynchronizeReplicaDataFormat DataFormat { get; init; } = SynchronizeReplicaDataFormat.Json;

    /// <summary>
    /// Validates the request configuration.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the request configuration is incomplete or internally inconsistent.
    /// </exception>
    public void Validate() {
        if (string.IsNullOrWhiteSpace(ReplicaId)) {
            throw new InvalidOperationException("ReplicaId must be provided.");
        }

        if (!Enum.IsDefined(SyncModel)) {
            throw new InvalidOperationException("SyncModel must be a supported synchronizeReplica sync model.");
        }

        if (!Enum.IsDefined(TransportType)) {
            throw new InvalidOperationException("TransportType must be a supported synchronizeReplica transport type.");
        }

        if (!Enum.IsDefined(SyncDirection)) {
            throw new InvalidOperationException("SyncDirection must be a supported synchronizeReplica sync direction.");
        }

        if (!Enum.IsDefined(DataFormat)) {
            throw new InvalidOperationException("DataFormat must be a supported synchronizeReplica data format.");
        }

        if (ReplicaServerGen is < 0) {
            throw new InvalidOperationException("ReplicaServerGen must not be negative when provided.");
        }

        if (SyncModel == SynchronizeReplicaSyncModel.PerReplica &&
            SyncDirection == SynchronizeReplicaSyncDirection.Download &&
            !ReplicaServerGen.HasValue) {
            throw new InvalidOperationException(
                "ReplicaServerGen is required when SyncModel is PerReplica and SyncDirection is Download.");
        }

        if (SyncModel == SynchronizeReplicaSyncModel.PerLayer && SyncLayers is not { Count: > 0 }) {
            throw new InvalidOperationException(
                "SyncLayers must contain at least one layer when SyncModel is PerLayer.");
        }

        if (SyncModel == SynchronizeReplicaSyncModel.PerReplica && SyncLayers.Count > 0) {
            throw new InvalidOperationException(
                "SyncLayers must not be provided when SyncModel is PerReplica.");
        }

        if (SyncModel == SynchronizeReplicaSyncModel.PerLayer && ReplicaServerGen.HasValue) {
            throw new InvalidOperationException(
                "ReplicaServerGen must not be provided when SyncModel is PerLayer.");
        }

        if (DataFormat == SynchronizeReplicaDataFormat.Sqlite &&
            TransportType == SynchronizeReplicaTransportType.Embedded) {
            throw new InvalidOperationException(
                "DataFormat Sqlite requires Url transport because SQLite synchronization results are returned as files.");
        }

        if (IsAsync && TransportType == SynchronizeReplicaTransportType.Embedded) {
            throw new InvalidOperationException(
                "Async synchronizeReplica requests require Url transport because async results are returned by URL.");
        }

        if (SyncLayers.Count > 0) {
            var layerIds = new HashSet<int>();

            foreach (var layer in SyncLayers) {
                if (layer is null) {
                    throw new InvalidOperationException("SyncLayers must not contain null values.");
                }

                layer.Validate();

                if (!layerIds.Add(layer.Id)) {
                    throw new InvalidOperationException("SyncLayers must not contain duplicate layer IDs.");
                }
            }
        }
    }
}