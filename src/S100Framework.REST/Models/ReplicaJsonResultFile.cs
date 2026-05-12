namespace S100Framework.REST.Models;

/// <summary>
/// Represents a parsed JSON result file produced by <c>createReplica</c> or <c>synchronizeReplica</c>.
/// </summary>
/// <param name="ReplicaId">
/// The replica ID returned in the JSON result file, when available.
/// </param>
/// <param name="ReplicaName">
/// The replica name returned in the JSON result file, when available.
/// </param>
/// <param name="TransportType">
/// The transport type returned in the JSON result file, when available.
/// </param>
/// <param name="ResponseType">
/// The response type returned in the JSON result file, when available.
/// </param>
/// <param name="SyncModel">
/// The sync model returned in the JSON result file, when available.
/// </param>
/// <param name="TargetType">
/// The target type returned in the JSON result file, when available.
/// </param>
/// <param name="ReplicaServerGen">
/// The replica-level server generation returned in the JSON result file, when available.
/// </param>
/// <param name="LayerServerGens">
/// The layer-level server generations returned in the JSON result file.
/// </param>
/// <param name="Layers">
/// The parsed layer or table entries returned in <c>edits</c> or <c>layers</c>.
/// </param>
/// <param name="RawJson">
/// The raw JSON file content.
/// </param>
/// <param name="ResultUrl">
/// The URL the result file was downloaded from.
/// </param>
public sealed record ReplicaJsonResultFile(
    string? ReplicaId,
    string? ReplicaName,
    string? TransportType,
    string? ResponseType,
    string? SyncModel,
    string? TargetType,
    long? ReplicaServerGen,
    IReadOnlyList<ReplicaLayerServerGeneration> LayerServerGens,
    IReadOnlyList<ReplicaJsonResultLayer> Layers,
    string RawJson,
    Uri ResultUrl)
{
    /// <summary>
    /// Gets a value indicating whether any layer entry contains upload edit result arrays.
    /// </summary>
    public bool HasEditResults =>
        Layers.Any(static layer => layer.HasEditResults);
}