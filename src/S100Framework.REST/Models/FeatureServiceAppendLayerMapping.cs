namespace S100Framework.REST.Models;

/// <summary>
/// Maps a source table or layer to a destination layer for append operations.
/// </summary>
public sealed record FeatureServiceAppendLayerMapping
{
    /// <summary>
    /// Gets the destination layer ID.
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// Gets the source layer ID when the source is another feature service.
    /// </summary>
    public int? SourceId { get; init; }

    /// <summary>
    /// Gets the source table name when the source format contains named tables.
    /// </summary>
    public string? SourceTableName { get; init; }

    /// <summary>
    /// Gets optional field mappings from source to destination fields.
    /// </summary>
    public IReadOnlyList<FeatureServiceAppendFieldMapping>? FieldMappings { get; init; }
}