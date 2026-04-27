namespace S100Framework.REST.Models;

/// <summary>
/// Represents approximate count and extent information returned by the <c>getEstimates</c> operation.
/// </summary>
/// <param name="LayerId">The layer or table ID associated with the estimate.</param>
/// <param name="Count">The approximate row count, when the service returns count information.</param>
/// <param name="Extent">The approximate spatial extent, when the service returns extent information.</param>
public sealed record FeatureLayerEstimate(
    int LayerId,
    long? Count,
    FeatureExtent? Extent);