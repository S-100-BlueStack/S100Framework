namespace S100Framework.REST.Models;

public sealed record FeatureServiceApplyEditsResult(
    IReadOnlyList<ServiceLayerEditResults> LayerResults);

public sealed record ServiceLayerEditResults(
    int LayerId,
    IReadOnlyList<EditResult> AddResults,
    IReadOnlyList<EditResult> UpdateResults,
    IReadOnlyList<EditResult> DeleteResults);