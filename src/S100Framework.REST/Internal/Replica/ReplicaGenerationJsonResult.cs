namespace S100Framework.REST.Internal.Replica;

internal sealed record ReplicaGenerationJsonResult(
    string? ReplicaId,
    string? ReplicaName,
    string? TransportType,
    string? ResponseType,
    string? SyncModel,
    string? TargetType,
    long? ReplicaServerGen,
    IReadOnlyList<ReplicaLayerGenerationJsonResult> LayerServerGens);