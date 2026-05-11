namespace S100Framework.REST.Models;

/// <summary>
/// Defines a service-level <c>createReplica</c> request.
/// </summary>
/// <remarks>
/// This model intentionally starts with client-side, download-oriented replica creation. Server-to-server,
/// bidirectional sync, upload workflows, and advanced replica options can be added without changing this core shape.
/// </remarks>
public sealed record CreateReplicaRequest
{
    /// <summary>
    /// Gets the optional replica name to request from the service.
    /// </summary>
    public string? ReplicaName { get; init; }

    /// <summary>
    /// Gets the layer and table IDs to include in the replica.
    /// </summary>
    /// <remarks>
    /// At least one layer or table ID must be provided.
    /// </remarks>
    public IReadOnlyList<int> Layers { get; init; } = Array.Empty<int>();

    /// <summary>
    /// Gets optional per-layer query filters.
    /// </summary>
    /// <remarks>
    /// Each key must reference a layer or table that is also included in <see cref="Layers" />.
    /// </remarks>
    public IReadOnlyDictionary<int, CreateReplicaLayerQuery>? LayerQueries { get; init; }

    /// <summary>
    /// Gets the optional spatial filter defining the replica area.
    /// </summary>
    /// <remarks>
    /// A spatial filter is required for <see cref="CreateReplicaSyncModel.PerReplica" /> and
    /// <see cref="CreateReplicaSyncModel.PerLayer" /> to stay compatible with older ArcGIS Enterprise services.
    /// </remarks>
    public FeatureSpatialFilter? SpatialFilter { get; init; }

    /// <summary>
    /// Gets the output spatial reference ID for the replica data, when specified.
    /// </summary>
    public int? ReplicaSrid { get; init; }

    /// <summary>
    /// Gets the response transport type requested from the service.
    /// </summary>
    public CreateReplicaTransportType TransportType { get; init; } = CreateReplicaTransportType.Url;

    /// <summary>
    /// Gets a value indicating whether attachment content should be included in the replica.
    /// </summary>
    public bool ReturnAttachments { get; init; }

    /// <summary>
    /// Gets a value indicating whether attachment content should be referenced by URL instead of embedded.
    /// </summary>
    /// <remarks>
    /// This option requires <see cref="ReturnAttachments" /> to be <see langword="true" />.
    /// </remarks>
    public bool ReturnAttachmentsDataByUrl { get; init; }

    /// <summary>
    /// Gets a value indicating whether the request should be submitted as an asynchronous job.
    /// </summary>
    public bool IsAsync { get; init; }

    /// <summary>
    /// Gets the sync model requested for the replica.
    /// </summary>
    public CreateReplicaSyncModel SyncModel { get; init; } = CreateReplicaSyncModel.PerReplica;

    /// <summary>
    /// Gets the requested replica data format.
    /// </summary>
    public CreateReplicaDataFormat DataFormat { get; init; } = CreateReplicaDataFormat.Json;

    /// <summary>
    /// Gets the replica target type.
    /// </summary>
    public CreateReplicaTargetType TargetType { get; init; } = CreateReplicaTargetType.Client;

    /// <summary>
    /// Validates the request configuration.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the request configuration is incomplete or internally inconsistent.
    /// </exception>
    public void Validate() {
        if (ReplicaName is not null && string.IsNullOrWhiteSpace(ReplicaName)) {
            throw new InvalidOperationException("ReplicaName must not be empty or whitespace when provided.");
        }

        if (Layers is not { Count: > 0 }) {
            throw new InvalidOperationException("At least one layer ID must be provided.");
        }

        if (Layers.Any(static layerId => layerId < 0)) {
            throw new InvalidOperationException("Layers must not contain negative values.");
        }

        if (Layers.Distinct().Count() != Layers.Count) {
            throw new InvalidOperationException("Layers must not contain duplicate values.");
        }

        if (!Enum.IsDefined(TransportType)) {
            throw new InvalidOperationException("TransportType must be a supported createReplica transport type.");
        }

        if (!Enum.IsDefined(SyncModel)) {
            throw new InvalidOperationException("SyncModel must be a supported createReplica sync model.");
        }

        if (!Enum.IsDefined(DataFormat)) {
            throw new InvalidOperationException("DataFormat must be a supported createReplica data format.");
        }

        if (!Enum.IsDefined(TargetType)) {
            throw new InvalidOperationException("TargetType must be a supported createReplica target type.");
        }

        if (TargetType != CreateReplicaTargetType.Client) {
            throw new InvalidOperationException("Only client target replicas are supported by this API version.");
        }

        if (ReturnAttachmentsDataByUrl && !ReturnAttachments) {
            throw new InvalidOperationException(
                "ReturnAttachmentsDataByUrl requires ReturnAttachments to be true.");
        }

        if (ReplicaSrid is <= 0) {
            throw new InvalidOperationException("ReplicaSrid must be greater than zero when provided.");
        }

        if (SpatialFilter?.InSrid is <= 0) {
            throw new InvalidOperationException("SpatialFilter.InSrid must be greater than zero when provided.");
        }

        if (SyncModel != CreateReplicaSyncModel.None && SpatialFilter is null) {
            throw new InvalidOperationException(
                "SpatialFilter is required when SyncModel is PerReplica or PerLayer.");
        }

        if (DataFormat == CreateReplicaDataFormat.Sqlite && TransportType == CreateReplicaTransportType.Embedded) {
            throw new InvalidOperationException(
                "DataFormat Sqlite requires Url transport because SQLite replicas are returned as files.");
        }

        if (IsAsync && TransportType == CreateReplicaTransportType.Embedded) {
            throw new InvalidOperationException(
                "Async createReplica requests require Url transport because async results are returned by URL.");
        }

        if (LayerQueries is { Count: > 0 }) {
            foreach (var pair in LayerQueries) {
                if (pair.Key < 0) {
                    throw new InvalidOperationException("LayerQueries must not contain negative layer IDs.");
                }

                if (!Layers.Contains(pair.Key)) {
                    throw new InvalidOperationException(
                        $"Layer query key '{pair.Key}' must also be present in Layers.");
                }

                if (pair.Value is null) {
                    throw new InvalidOperationException("LayerQueries must not contain null values.");
                }

                pair.Value.Validate();
            }
        }
    }
}