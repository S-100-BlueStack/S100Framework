namespace S100Framework.REST.Models;

/// <summary>
/// Represents the result returned by the service-level <c>replicas</c> resource.
/// </summary>
/// <param name="Replicas">
/// The replicas returned by the service.
/// </param>
public sealed record FeatureServiceReplicasResult(
    IReadOnlyList<FeatureServiceReplica> Replicas);