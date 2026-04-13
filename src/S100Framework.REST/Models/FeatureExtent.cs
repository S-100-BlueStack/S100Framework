using NetTopologySuite.Geometries;

namespace S100Framework.REST.Models;

public sealed record FeatureExtent(
    Envelope Envelope,
    int? Srid);