namespace S100Framework.REST.Models;

/// <summary>
/// Represents a layer or table entry parsed from a replica JSON result file.
/// </summary>
/// <param name="Id">
/// The layer or table ID.
/// </param>
/// <param name="AddResults">
/// The add edit results returned for the layer or table.
/// </param>
/// <param name="UpdateResults">
/// The update edit results returned for the layer or table.
/// </param>
/// <param name="DeleteResults">
/// The delete edit results returned for the layer or table.
/// </param>
/// <param name="RawJson">
/// The raw JSON for the layer or table entry.
/// </param>
public sealed record ReplicaJsonResultLayer(
    int Id,
    IReadOnlyList<ReplicaEditResult> AddResults,
    IReadOnlyList<ReplicaEditResult> UpdateResults,
    IReadOnlyList<ReplicaEditResult> DeleteResults,
    string RawJson)
{
    /// <summary>
    /// Gets a value indicating whether the layer contains upload edit result arrays.
    /// </summary>
    public bool HasEditResults =>
        AddResults.Count > 0 ||
        UpdateResults.Count > 0 ||
        DeleteResults.Count > 0;
}