namespace S100Framework.REST.Models;

/// <summary>
/// Defines the linear unit used when applying a spatial query distance.
/// </summary>
public enum FeatureSpatialDistanceUnit
{
    /// <summary>
    /// Uses meters.
    /// </summary>
    Meter,

    /// <summary>
    /// Uses statute miles.
    /// </summary>
    StatuteMile,

    /// <summary>
    /// Uses feet.
    /// </summary>
    Foot,

    /// <summary>
    /// Uses kilometers.
    /// </summary>
    Kilometer,

    /// <summary>
    /// Uses nautical miles.
    /// </summary>
    NauticalMile,

    /// <summary>
    /// Uses US nautical miles.
    /// </summary>
    UsNauticalMile
}