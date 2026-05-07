namespace S100Framework.REST.Models;

/// <summary>
/// Specifies how a layer-level query filter should be applied in <c>extractChanges</c>.
/// </summary>
public enum ExtractChangesLayerQueryOption
{
    /// <summary>
    /// No special query option is requested.
    /// </summary>
    None = 0,

    /// <summary>
    /// Applies the supplied filter settings for the layer.
    /// </summary>
    UseFilter = 1,

    /// <summary>
    /// Requests all changes for the layer without applying a filter.
    /// </summary>
    All = 2
}

/// <summary>
/// Defines an optional per-layer query filter for <c>extractChanges</c>.
/// </summary>
public sealed record ExtractChangesLayerQuery
{
    /// <summary>
    /// Gets the query behavior for the layer.
    /// </summary>
    public ExtractChangesLayerQueryOption QueryOption { get; init; } = ExtractChangesLayerQueryOption.UseFilter;

    /// <summary>
    /// Gets the optional SQL where-clause to apply for the layer.
    /// </summary>
    public string? Where { get; init; }

    /// <summary>
    /// Gets a value indicating whether the spatial filter should be applied to the layer.
    /// </summary>
    public bool UseGeometry { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether related records should be included when supported.
    /// </summary>
    public bool IncludeRelated { get; init; } = true;

    /// <summary>
    /// Validates the layer query configuration.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the layer query configuration contains unsupported values.
    /// </exception>
    public void Validate() {
        if (!Enum.IsDefined(QueryOption)) {
            throw new InvalidOperationException(
                "QueryOption must be a supported extractChanges layer query option.");
        }

        // ArcGIS accepts combinations here and ignores some values depending on queryOption.
        // The client keeps validation intentionally light to avoid rejecting valid server-side cases.
    }
}