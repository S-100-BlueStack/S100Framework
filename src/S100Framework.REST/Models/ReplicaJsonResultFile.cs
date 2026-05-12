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

    /// <summary>
    /// Gets a value indicating whether any parsed edit result contains an error.
    /// </summary>
    public bool HasEditErrors =>
        Layers.Any(static layer => layer.HasEditErrors);

    /// <summary>
    /// Gets the total number of parsed edit results across all layers.
    /// </summary>
    public int EditResultCount =>
        Layers.Sum(static layer => layer.EditResultCount);

    /// <summary>
    /// Gets the number of parsed successful edit results across all layers.
    /// </summary>
    public int SuccessfulEditResultCount =>
        Layers.Sum(static layer => layer.SuccessfulEditResultCount);

    /// <summary>
    /// Gets the number of parsed failed edit results across all layers.
    /// </summary>
    public int FailedEditResultCount =>
        Layers.Sum(static layer => layer.FailedEditResultCount);

    /// <summary>
    /// Gets all parsed edit results across all layers.
    /// </summary>
    /// <returns>
    /// All parsed add, update, and delete results.
    /// </returns>
    public IEnumerable<ReplicaEditResult> GetEditResults() {
        return Layers.SelectMany(static layer => layer.GetEditResults());
    }

    /// <summary>
    /// Gets parsed edit results that contain an error.
    /// </summary>
    /// <returns>
    /// Failed edit results across all layers.
    /// </returns>
    public IEnumerable<ReplicaEditResult> GetEditErrors() {
        return GetEditResults().Where(static result => result.HasError || result.Success == false);
    }
}