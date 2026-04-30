namespace S100Framework.REST.Models;

/// <summary>
/// Represents the result of a service-level <c>query</c> operation that returns row counts only.
/// </summary>
/// <param name="Layers">
/// The layer-grouped row counts returned by the service.
/// </param>
public sealed record FeatureServiceQueryCountResult(
    IReadOnlyList<FeatureServiceLayerCountResult> Layers);