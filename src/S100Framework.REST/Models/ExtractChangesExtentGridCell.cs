namespace S100Framework.REST.Models;

/// <summary>
/// Specifies the grid-cell size used when requesting extent-only change output.
/// </summary>
public enum ExtractChangesExtentGridCell
{
    /// <summary>
    /// No grid-cell aggregation is requested.
    /// </summary>
    None = 0,

    /// <summary>
    /// Requests large grid cells for returned change extents.
    /// </summary>
    Large = 1,

    /// <summary>
    /// Requests medium grid cells for returned change extents.
    /// </summary>
    Medium = 2,

    /// <summary>
    /// Requests small grid cells for returned change extents.
    /// </summary>
    Small = 3
}