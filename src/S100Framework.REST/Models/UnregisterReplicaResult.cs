namespace S100Framework.REST.Models;

/// <summary>
/// Represents the result returned by a feature service <c>unRegisterReplica</c> operation.
/// </summary>
/// <param name="ReplicaId">
/// The replica ID submitted to the service.
/// </param>
/// <param name="Success">
/// Indicates whether the service reported a successful unregister operation.
/// </param>
public sealed record UnregisterReplicaResult(
    string ReplicaId,
    bool Success);