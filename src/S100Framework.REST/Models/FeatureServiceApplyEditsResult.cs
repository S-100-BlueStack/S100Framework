namespace S100Framework.REST.Models;

/// <summary>
/// Represents the final result of a service-level <c>applyEdits</c> operation across
/// multiple layers.
/// </summary>
/// <param name="LayerResults">
/// The edit results grouped by layer.
/// </param>
public sealed record FeatureServiceApplyEditsResult(
    IReadOnlyList<ServiceLayerEditResults> LayerResults);

/// <summary>
/// Represents the final <c>applyEdits</c> result for a single layer within a
/// service-level request.
/// </summary>
/// <param name="LayerId">
/// The layer ID that produced these results.
/// </param>
/// <param name="AddResults">
/// The per-feature results for add operations.
/// </param>
/// <param name="UpdateResults">
/// The per-feature results for update operations.
/// </param>
/// <param name="DeleteResults">
/// The per-feature results for delete operations.
/// </param>
public sealed record ServiceLayerEditResults(
    int LayerId,
    IReadOnlyList<EditResult> AddResults,
    IReadOnlyList<EditResult> UpdateResults,
    IReadOnlyList<EditResult> DeleteResults);