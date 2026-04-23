using NetTopologySuite.Geometries;

namespace S100Framework.REST.Models;

/// <summary>
/// Represents a spatial extent returned by a feature service.
/// </summary>
/// <param name="Envelope">
/// The extent envelope.
/// </param>
/// <param name="Srid">
/// The spatial reference ID associated with the extent, when available.
/// </param>
public sealed record FeatureExtent(
    Envelope Envelope,
    int? Srid);