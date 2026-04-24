namespace S100Framework.REST.Models;

/// <summary>
/// Maps a source field to a destination field for append operations.
/// </summary>
public sealed record FeatureServiceAppendFieldMapping
{
    /// <summary>
    /// Gets the destination field name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the source field name.
    /// </summary>
    public string Source { get; init; } = string.Empty;
}