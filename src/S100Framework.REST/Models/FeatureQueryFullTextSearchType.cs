namespace S100Framework.REST.Models;

/// <summary>
/// Defines the type of full text search to perform.
/// </summary>
public enum FeatureQueryFullTextSearchType
{
    /// <summary>
    /// Matches keywords or quoted phrases.
    /// </summary>
    Simple = 0,

    /// <summary>
    /// Matches values using prefix search semantics.
    /// </summary>
    Prefix = 1
}