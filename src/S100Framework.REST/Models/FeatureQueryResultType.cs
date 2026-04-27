namespace S100Framework.REST.Models;

/// <summary>
/// Defines the feature query result type used by ArcGIS max-record-count behavior.
/// </summary>
public enum FeatureQueryResultType
{
    /// <summary>
    /// Uses the service's default query behavior.
    /// </summary>
    None = 0,

    /// <summary>
    /// Uses standard, non-tiled query behavior.
    /// </summary>
    Standard = 1,

    /// <summary>
    /// Uses tile-oriented query behavior.
    /// </summary>
    Tile = 2
}