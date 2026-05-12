namespace S100Framework.REST.Models;

/// <summary>
/// Represents a layer-level server generation returned from <c>synchronizeReplica</c>.
/// </summary>
/// <param name="Id">
/// The layer or table ID.
/// </param>
/// <param name="ServerGen">
/// The server generation value for the layer or table.
/// </param>
public sealed record SynchronizeReplicaLayerServerGen(
    int Id,
    long ServerGen);