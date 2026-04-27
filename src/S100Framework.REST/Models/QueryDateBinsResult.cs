namespace S100Framework.REST.Models;

/// <summary>
/// Represents the result of a <c>queryDateBins</c> operation.
/// </summary>
/// <param name="Rows">The returned date-bin rows.</param>
/// <param name="ExceededTransferLimit">Indicates whether the service exceeded its transfer limit.</param>
/// <param name="GeometryType">The geometry type returned by the service when centroid output is included.</param>
public sealed record QueryDateBinsResult(
    IReadOnlyList<QueryDateBinRow> Rows,
    bool? ExceededTransferLimit,
    string? GeometryType);