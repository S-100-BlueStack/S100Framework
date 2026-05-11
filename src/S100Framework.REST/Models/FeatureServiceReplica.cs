namespace S100Framework.REST.Models;

/// <summary>
/// Represents a replica registered on a feature service.
/// </summary>
/// <param name="ReplicaName">
/// The replica name returned by the service.
/// </param>
/// <param name="ReplicaId">
/// The replica ID returned by the service.
/// </param>
/// <param name="ReplicaVersion">
/// The replica version returned by the service, when requested and available.
/// </param>
/// <param name="LastSyncDate">
/// The last synchronization timestamp returned by the service, when requested and available.
/// </param>
public sealed record FeatureServiceReplica(
    string ReplicaName,
    string ReplicaId,
    string? ReplicaVersion,
    long? LastSyncDate);