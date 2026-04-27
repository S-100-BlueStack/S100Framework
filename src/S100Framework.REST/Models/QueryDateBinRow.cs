namespace S100Framework.REST.Models;

/// <summary>
/// Represents one row returned by a <c>queryDateBins</c> operation.
/// </summary>
/// <param name="Attributes">
/// The date-bin attributes returned by the service, such as boundary values and requested statistics.
/// </param>
/// <param name="Centroid">
/// The centroid attributes returned by the service when <c>returnCentroid</c> is enabled.
/// </param>
public sealed record QueryDateBinRow(
    IReadOnlyDictionary<string, object?> Attributes,
    IReadOnlyDictionary<string, object?>? Centroid) : IAttributeRecord;