namespace S100Framework.REST.Models;

/// <summary>
/// Represents the immediate or completed result of a <c>synchronizeReplica</c> operation.
/// </summary>
/// <param name="ReplicaId">
/// The replica ID returned by the service, when available.
/// </param>
/// <param name="ReplicaName">
/// The replica name returned by the service, when available.
/// </param>
/// <param name="TransportType">
/// The response transport type returned by the service, when available.
/// </param>
/// <param name="ResponseType">
/// The replica response type returned by the service, when available.
/// </param>
/// <param name="ReplicaServerGen">
/// The updated replica-level server generation, when the service returns one.
/// </param>
/// <param name="LayerServerGens">
/// The updated layer-level server generations returned by the service.
/// </param>
/// <param name="ResultUrl">
/// The generated synchronization file URL, when the service returns one.
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
public sealed record SynchronizeReplicaResult(
    string? ReplicaId,
    string? ReplicaName,
    string? TransportType,
    string? ResponseType,
    long? ReplicaServerGen,
    IReadOnlyList<SynchronizeReplicaLayerServerGen> LayerServerGens,
    Uri? ResultUrl,
    string? Status,
    long? SubmissionTime,
    long? LastUpdatedTime);