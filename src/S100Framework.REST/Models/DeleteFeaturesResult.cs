namespace S100Framework.REST.Models;

/// <summary>
/// Represents the result of a direct layer-level <c>deleteFeatures</c> operation.
/// </summary>
/// <param name="Success">
/// Indicates whether the service reported a successful delete operation.
/// </param>
/// <param name="DeleteResults">
/// The per-feature delete results returned by the service, when available.
/// </param>
/// <param name="EditMoment">
/// The edit moment returned by the service when requested and supported.
/// </param>
public sealed record DeleteFeaturesResult(
    bool Success,
    IReadOnlyList<EditResult> DeleteResults,
    long? EditMoment);