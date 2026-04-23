using NetTopologySuite.Geometries;

namespace S100Framework.REST.Models;

/// <summary>
/// Represents a feature returned by a query or change operation.
/// </summary>
/// <param name="Geometry">
/// The feature geometry, when returned.
/// </param>
/// <param name="Attributes">
/// The feature attributes.
/// </param>
/// <param name="ObjectId">
/// The resolved object ID, when available.
/// </param>
public sealed record FeatureRecord(
    Geometry? Geometry,
    IReadOnlyDictionary<string, object?> Attributes,
    long? ObjectId) : IAttributeRecord;