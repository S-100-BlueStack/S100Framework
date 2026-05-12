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

    /// <summary>
    /// Gets a value indicating whether any edit result on the layer contains an error.
    /// </summary>
    public bool HasEditErrors =>
        GetEditResults().Any(static result => result.HasError || result.Success == false);

    /// <summary>
    /// Gets the total number of parsed edit results on the layer.
    /// </summary>
    public int EditResultCount =>
        AddResults.Count + UpdateResults.Count + DeleteResults.Count;

    /// <summary>
    /// Gets the number of parsed successful edit results on the layer.
    /// </summary>
    public int SuccessfulEditResultCount =>
        GetEditResults().Count(static result => result.IsSuccessful);

    /// <summary>
    /// Gets the number of parsed failed edit results on the layer.
    /// </summary>
    public int FailedEditResultCount =>
        GetEditResults().Count(static result => result.IsFailed);

    /// <summary>
    /// Gets all parsed edit results on the layer.
    /// </summary>
    /// <returns>
    /// All parsed add, update, and delete results.
    /// </returns>
    public IEnumerable<ReplicaEditResult> GetEditResults() {
        return AddResults
            .Concat(UpdateResults)
            .Concat(DeleteResults);
    }
}