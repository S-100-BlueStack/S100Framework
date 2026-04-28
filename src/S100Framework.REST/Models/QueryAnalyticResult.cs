namespace S100Framework.REST.Models;

/// <summary>
/// Represents the result of a <c>queryAnalytic</c> operation.
/// </summary>
/// <param name="Rows">
/// The returned analytic rows as feature records.
/// </param>
/// <param name="ExceededTransferLimit">
/// Indicates whether the service exceeded its transfer limit.
/// </param>
public sealed record QueryAnalyticResult(
    IReadOnlyList<FeatureRecord> Rows,
    bool? ExceededTransferLimit);