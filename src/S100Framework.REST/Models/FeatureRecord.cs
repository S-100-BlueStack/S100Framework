using NetTopologySuite.Geometries;

namespace S100Framework.REST.Models;

public sealed record FeatureRecord(
    Geometry? Geometry,
    IReadOnlyDictionary<string, object?> Attributes,
    long? ObjectId) : IAttributeRecord;