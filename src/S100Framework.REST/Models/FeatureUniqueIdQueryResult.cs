namespace S100Framework.REST.Models;

/// <summary>
/// Represents the result of a query that returns unique IDs only.
/// </summary>
public sealed record FeatureUniqueIdQueryResult
{
    /// <summary>
    /// Initializes a unique ID query result.
    /// </summary>
    /// <param name="uniqueIdFieldNames">
    /// The field names that define the unique ID.
    /// </param>
    /// <param name="uniqueIds">
    /// The returned unique ID values.
    /// </param>
    /// <param name="exceededTransferLimit">
    /// Indicates whether the server signaled that the transfer limit was exceeded.
    /// </param>
    public FeatureUniqueIdQueryResult(
        IReadOnlyList<string> uniqueIdFieldNames,
        IReadOnlyList<FeatureUniqueId> uniqueIds,
        bool exceededTransferLimit) {
        ArgumentNullException.ThrowIfNull(uniqueIdFieldNames);
        ArgumentNullException.ThrowIfNull(uniqueIds);

        UniqueIdFieldNames = uniqueIdFieldNames;
        UniqueIds = uniqueIds;
        ExceededTransferLimit = exceededTransferLimit;
    }

    /// <summary>
    /// Gets the field names that define the unique ID.
    /// </summary>
    public IReadOnlyList<string> UniqueIdFieldNames { get; init; }

    /// <summary>
    /// Gets the returned unique ID values.
    /// </summary>
    public IReadOnlyList<FeatureUniqueId> UniqueIds { get; init; }

    /// <summary>
    /// Gets a value indicating whether the server signaled that the transfer limit was exceeded.
    /// </summary>
    public bool ExceededTransferLimit { get; init; }

    /// <summary>
    /// Gets a value indicating whether the returned identifiers are composite.
    /// </summary>
    public bool IsComposite => UniqueIdFieldNames.Count > 1;
}