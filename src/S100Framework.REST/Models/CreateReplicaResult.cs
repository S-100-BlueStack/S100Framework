namespace S100Framework.REST.Models;

/// <summary>
/// Represents the immediate or completed result of a <c>createReplica</c> operation.
/// </summary>
/// <param name="ReplicaName">
/// The replica name returned by the service, when available.
/// </param>
/// <param name="ReplicaId">
/// The replica ID returned by the service, when available.
/// </param>
/// <param name="TransportType">
/// The response transport type returned by the service, when available.
/// </param>
/// <param name="ResponseType">
/// The replica response type returned by the service, when available.
/// </param>
/// <param name="SyncModel">
/// The sync model returned by the service, when available.
/// </param>
/// <param name="TargetType">
/// The target type returned by the service, when available.
/// </param>
/// <param name="ReplicaServerGen">
/// The replica-level server generation, when the service returns one.
/// </param>
/// <param name="LayerServerGens">
/// The layer-level server generations returned by the service.
/// </param>
/// <param name="ResultUrl">
/// The generated replica file URL, when the service returns one.
/// </param>
/// <param name="Status">
/// The job status returned by the service, when available.
/// </param>
/// <param name="SubmissionTime">
/// The server submission timestamp, when available.
/// </param>
/// <param name="LastUpdatedTime">
/// The server last-updated timestamp, when available.
/// </param>
public sealed record CreateReplicaResult(
    string? ReplicaName,
    string? ReplicaId,
    string? TransportType,
    string? ResponseType,
    string? SyncModel,
    string? TargetType,
    long? ReplicaServerGen,
    IReadOnlyList<CreateReplicaLayerServerGen> LayerServerGens,
    Uri? ResultUrl,
    string? Status,
    long? SubmissionTime,
    long? LastUpdatedTime);