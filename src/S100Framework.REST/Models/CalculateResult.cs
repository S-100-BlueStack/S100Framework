namespace S100Framework.REST.Models;

/// <summary>
/// Represents the result of a layer-level <c>calculate</c> operation.
/// </summary>
/// <param name="Success">
/// Indicates whether the service reported a successful calculation.
/// </param>
/// <param name="UpdatedFeatureCount">
/// The number of features updated by the operation, when returned by the service.
/// </param>
/// <param name="EditMoment">
/// The edit moment returned by the service when requested and supported.
/// </param>
public sealed record CalculateResult(
    bool Success,
    long? UpdatedFeatureCount,
    long? EditMoment);