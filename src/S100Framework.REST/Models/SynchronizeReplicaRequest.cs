using System.Text.Json;

namespace S100Framework.REST.Models;

/// <summary>
/// Defines a service-level <c>synchronizeReplica</c> request.
/// </summary>
/// <remarks>
/// This model supports download, snapshot, raw upload, and raw bidirectional synchronization.
/// Typed edit payload builders can be layered on top without changing the underlying REST request shape.
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
    /// This value is required for per-replica directions that download server changes.
    /// </remarks>
    public long? ReplicaServerGen { get; init; }

    /// <summary>
    /// Gets the per-layer synchronization generations.
    /// </summary>
    /// <remarks>
    /// This value is used for <see cref="SynchronizeReplicaSyncModel.PerLayer" /> requests.
    /// </remarks>
    public IReadOnlyList<SynchronizeReplicaSyncLayer> SyncLayers { get; init; } =
        Array.Empty<SynchronizeReplicaSyncLayer>();

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
    public SynchronizeReplicaSyncDirection SyncDirection { get; init; } =
        SynchronizeReplicaSyncDirection.Download;

    /// <summary>
    /// Gets the requested synchronization data format.
    /// </summary>
    public SynchronizeReplicaDataFormat DataFormat { get; init; } = SynchronizeReplicaDataFormat.Json;

    /// <summary>
    /// Gets the raw Esri <c>edits</c> JSON payload used for upload and bidirectional synchronization.
    /// </summary>
    /// <remarks>
    /// The payload is accepted as raw JSON to keep the core package schema-agnostic. Typed builders can be added later.
    /// </remarks>
    public string? EditsJson { get; init; }

    /// <summary>
    /// Gets the uploaded edit payload ID used for upload and bidirectional synchronization.
    /// </summary>
    /// <remarks>
    /// Use this when a large edits payload has already been uploaded through the service uploads workflow.
    /// </remarks>
    public string? EditsUploadId { get; init; }

    /// <summary>
    /// Gets a value indicating whether uploaded edits should be rolled back if any edit fails.
    /// </summary>
    /// <remarks>
    /// This option is only valid for upload and bidirectional synchronization.
    /// </remarks>
    public bool RollbackOnFailure { get; init; }

    /// <summary>
    /// Gets a value indicating whether the service should return object IDs for added features.
    /// </summary>
    /// <remarks>
    /// This option is only valid for upload and bidirectional synchronization.
    /// </remarks>
    public bool ReturnIdsForAdds { get; init; }

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

        ValidateUploadOptions();

        if (SyncModel == SynchronizeReplicaSyncModel.PerReplica &&
            RequiresReplicaServerGeneration(SyncDirection) &&
            !ReplicaServerGen.HasValue) {
            throw new InvalidOperationException(
                "ReplicaServerGen is required when SyncModel is PerReplica and SyncDirection downloads server changes.");
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

    private void ValidateUploadOptions() {
        var hasEditsJson = EditsJson is not null;
        var hasEditsUploadId = EditsUploadId is not null;
        var isUploadDirection = IsUploadDirection(SyncDirection);

        if (hasEditsJson && string.IsNullOrWhiteSpace(EditsJson)) {
            throw new InvalidOperationException("EditsJson must not be empty or whitespace when provided.");
        }

        if (hasEditsUploadId && string.IsNullOrWhiteSpace(EditsUploadId)) {
            throw new InvalidOperationException("EditsUploadId must not be empty or whitespace when provided.");
        }

        if (hasEditsJson && hasEditsUploadId) {
            throw new InvalidOperationException(
                "EditsJson and EditsUploadId cannot both be provided.");
        }

        if (isUploadDirection && !hasEditsJson && !hasEditsUploadId) {
            throw new InvalidOperationException(
                "Upload and bidirectional synchronizeReplica requests require either EditsJson or EditsUploadId.");
        }

        if (!isUploadDirection && (hasEditsJson || hasEditsUploadId)) {
            throw new InvalidOperationException(
                "EditsJson and EditsUploadId can only be used with Upload or Bidirectional sync directions.");
        }

        if (!isUploadDirection && RollbackOnFailure) {
            throw new InvalidOperationException(
                "RollbackOnFailure can only be used with Upload or Bidirectional sync directions.");
        }

        if (!isUploadDirection && ReturnIdsForAdds) {
            throw new InvalidOperationException(
                "ReturnIdsForAdds can only be used with Upload or Bidirectional sync directions.");
        }

        if (hasEditsJson) {
            ValidateEditsJson();
        }
    }

    private void ValidateEditsJson() {
        try {
            using var document = JsonDocument.Parse(EditsJson!);

            if (document.RootElement.ValueKind is not JsonValueKind.Object and not JsonValueKind.Array) {
                throw new InvalidOperationException(
                    "EditsJson must contain a JSON object or array.");
            }
        }
        catch (JsonException exception) {
            throw new InvalidOperationException(
                "EditsJson must contain valid JSON.",
                exception);
        }
    }

    private static bool RequiresReplicaServerGeneration(SynchronizeReplicaSyncDirection syncDirection) {
        return syncDirection is
            SynchronizeReplicaSyncDirection.Download or
            SynchronizeReplicaSyncDirection.Bidirectional;
    }

    private static bool IsUploadDirection(SynchronizeReplicaSyncDirection syncDirection) {
        return syncDirection is
            SynchronizeReplicaSyncDirection.Upload or
            SynchronizeReplicaSyncDirection.Bidirectional;
    }
}