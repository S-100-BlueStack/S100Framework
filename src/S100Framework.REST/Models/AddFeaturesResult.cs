namespace S100Framework.REST.Models;

/// <summary>
/// Represents the result of a direct layer-level <c>addFeatures</c> operation.
/// </summary>
/// <param name="Success">
/// Indicates whether all returned add results succeeded.
/// </param>
/// <param name="AddResults">
/// The per-feature add results returned by the service.
/// </param>
/// <param name="EditMoment">
/// The edit moment returned by the service when requested and supported.
/// </param>
public sealed record AddFeaturesResult(
    bool Success,
    IReadOnlyList<EditResult> AddResults,
    long? EditMoment);