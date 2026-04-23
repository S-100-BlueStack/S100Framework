namespace S100Framework.REST.Models;

/// <summary>
/// Defines the spatial relationship used when evaluating a geometry filter.
/// </summary>
public enum SpatialRelationship
{
    /// <summary>
    /// Matches geometries that intersect.
    /// </summary>
    Intersects,

    /// <summary>
    /// Matches geometries that fully contain the filter geometry.
    /// </summary>
    Contains,

    /// <summary>
    /// Matches geometries that cross the filter geometry.
    /// </summary>
    Crosses,

    /// <summary>
    /// Matches geometries whose envelopes intersect.
    /// </summary>
    EnvelopeIntersects,

    /// <summary>
    /// Matches geometries using spatial index intersection semantics.
    /// </summary>
    IndexIntersects,

    /// <summary>
    /// Matches geometries that overlap.
    /// </summary>
    Overlaps,

    /// <summary>
    /// Matches geometries that touch.
    /// </summary>
    Touches,

    /// <summary>
    /// Matches geometries that are within the filter geometry.
    /// </summary>
    Within
}