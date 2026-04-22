namespace S100Framework.REST.Configuration;

/// <summary>
/// Defines how true-curve geometries returned by the service should be handled.
/// </summary>
public enum TrueCurveHandling
{
    /// <summary>
    /// Throws when the service returns true-curve geometry that cannot be represented directly.
    /// </summary>
    Throw = 0,

    /// <summary>
    /// Converts circular arcs to linearized segments.
    /// </summary>
    LinearizeCircularArcs = 1
}