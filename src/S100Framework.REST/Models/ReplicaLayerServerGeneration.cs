namespace S100Framework.REST.Models;

/// <summary>
/// Represents a persisted layer-level server generation for a replica.
/// </summary>
/// <param name="Id">
/// The layer or table ID.
/// </param>
/// <param name="ServerGen">
/// The server generation for the layer or table.
/// </param>
public sealed record ReplicaLayerServerGeneration(
    int Id,
    long ServerGen);