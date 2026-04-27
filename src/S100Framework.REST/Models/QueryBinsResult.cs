namespace S100Framework.REST.Models;

/// <summary>
/// Represents the result of a <c>queryBins</c> operation.
/// </summary>
/// <param name="Rows">The returned bin rows.</param>
/// <param name="ExceededTransferLimit">Indicates whether the service exceeded its transfer limit.</param>
/// <param name="StackFieldNames">The stacked field names returned by the service for stacked bins.</param>
public sealed record QueryBinsResult(
    IReadOnlyList<QueryBinRow> Rows,
    bool? ExceededTransferLimit,
    IReadOnlyList<string> StackFieldNames);