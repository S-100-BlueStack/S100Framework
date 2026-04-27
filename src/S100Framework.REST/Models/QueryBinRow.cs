namespace S100Framework.REST.Models;

/// <summary>
/// Represents one row returned by a <c>queryBins</c> operation.
/// </summary>
/// <param name="Attributes">
/// The bin attributes returned by the service, such as bin number, boundaries, frequency, and statistics.
/// </param>
/// <param name="StackedAttributes">
/// The stacked bin attributes returned by the service when stacked bins are requested.
/// </param>
public sealed record QueryBinRow(
    IReadOnlyDictionary<string, object?> Attributes,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> StackedAttributes) : IAttributeRecord;