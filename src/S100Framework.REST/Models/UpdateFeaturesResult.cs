namespace S100Framework.REST.Models;

/// <summary>
/// Represents the result of a direct layer-level <c>updateFeatures</c> operation.
/// </summary>
/// <param name="Success">
/// Indicates whether all returned update results succeeded.
/// </param>
/// <param name="UpdateResults">
/// The per-feature update results returned by the service.
/// </param>
/// <param name="EditMoment">
/// The edit moment returned by the service when requested and supported.
/// </param>
public sealed record UpdateFeaturesResult(
    bool Success,
    IReadOnlyList<EditResult> UpdateResults,
    long? EditMoment);