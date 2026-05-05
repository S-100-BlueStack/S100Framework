using NetTopologySuite.Geometries;
using S100Framework.REST.Internal.EsriGeometry;

namespace S100Framework.REST.Models;

/// <summary>
/// Represents a spatial filter that can be applied to feature queries.
/// </summary>
/// <remarks>
/// Instances are created through the provided factory methods to ensure the geometry
/// is serialized consistently for ArcGIS REST query parameters.
/// </remarks>
public sealed class FeatureSpatialFilter
{
    private FeatureSpatialFilter(
        string geometryJson,
        string geometryType,
        SpatialRelationship spatialRelationship,
        int? inSrid,
        double? distance,
        FeatureSpatialDistanceUnit? distanceUnit) {
        GeometryJson = geometryJson;
        GeometryType = geometryType;
        SpatialRelationship = spatialRelationship;
        InSrid = inSrid;
        Distance = distance;
        DistanceUnit = distanceUnit;
    }

    /// <summary>
    /// Gets the serialized Esri JSON geometry payload.
    /// </summary>
    internal string GeometryJson { get; }

    /// <summary>
    /// Gets the ArcGIS geometry type identifier for the serialized geometry.
    /// </summary>
    internal string GeometryType { get; }

    /// <summary>
    /// Gets the ArcGIS distance unit used for the spatial query buffer.
    /// </summary>
    internal FeatureSpatialDistanceUnit? DistanceUnit { get; }

    /// <summary>
    /// Gets the spatial relationship used when evaluating the filter.
    /// </summary>
    public SpatialRelationship SpatialRelationship { get; }

    /// <summary>
    /// Gets the spatial reference ID of the input geometry, when available.
    /// </summary>
    public int? InSrid { get; }

    /// <summary>
    /// Gets the buffer distance used when evaluating the spatial filter.
    /// </summary>
    public double? Distance { get; }

    /// <summary>
    /// Creates a spatial filter from an envelope.
    /// </summary>
    /// <param name="envelope">
    /// The envelope to serialize.
    /// </param>
    /// <param name="inSrid">
    /// The spatial reference ID of the envelope.
    /// </param>
    /// <param name="spatialRelationship">
    /// The spatial relationship to apply.
    /// </param>
    /// <param name="distance">
    /// The optional buffer distance to apply around the input geometry.
    /// </param>
    /// <param name="distanceUnit">
    /// The optional linear unit used for <paramref name="distance" />.
    /// </param>
    /// <returns>
    /// A spatial filter based on the provided envelope.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="envelope" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the envelope is null or empty, or when spatial options are invalid.
    /// </exception>
    public static FeatureSpatialFilter FromEnvelope(
        Envelope envelope,
        int? inSrid,
        SpatialRelationship spatialRelationship = SpatialRelationship.Intersects,
        double? distance = null,
        FeatureSpatialDistanceUnit? distanceUnit = null) {
        ArgumentNullException.ThrowIfNull(envelope);

        if (envelope.IsNull) {
            throw new InvalidOperationException("Envelope must not be null or empty.");
        }

        ValidateOptions(
            inSrid,
            spatialRelationship,
            distance,
            distanceUnit);

        return new FeatureSpatialFilter(
            EsriQueryGeometryWriter.WriteEnvelope(envelope, inSrid),
            EsriGeometryTypes.Envelope,
            spatialRelationship,
            inSrid,
            distance,
            distanceUnit);
    }

    /// <summary>
    /// Creates a spatial filter from a NetTopologySuite geometry.
    /// </summary>
    /// <param name="geometry">
    /// The geometry to serialize.
    /// </param>
    /// <param name="inSrid">
    /// The input spatial reference ID. When omitted, the geometry SRID is used if greater than zero.
    /// </param>
    /// <param name="spatialRelationship">
    /// The spatial relationship to apply.
    /// </param>
    /// <param name="distance">
    /// The optional buffer distance to apply around the input geometry.
    /// </param>
    /// <param name="distanceUnit">
    /// The optional linear unit used for <paramref name="distance" />.
    /// </param>
    /// <returns>
    /// A spatial filter based on the provided geometry.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="geometry" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the geometry is empty, or when spatial options are invalid.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// Thrown when the geometry type cannot be serialized to an ArcGIS query geometry.
    /// </exception>
    public static FeatureSpatialFilter FromGeometry(
        Geometry geometry,
        int? inSrid = null,
        SpatialRelationship spatialRelationship = SpatialRelationship.Intersects,
        double? distance = null,
        FeatureSpatialDistanceUnit? distanceUnit = null) {
        ArgumentNullException.ThrowIfNull(geometry);

        if (geometry.IsEmpty) {
            throw new InvalidOperationException("Geometry must not be empty.");
        }

        var resolvedSrid = inSrid ?? (geometry.SRID > 0 ? geometry.SRID : null);

        ValidateOptions(
            resolvedSrid,
            spatialRelationship,
            distance,
            distanceUnit);

        var geometryType = ResolveGeometryType(geometry);

        return new FeatureSpatialFilter(
            EsriQueryGeometryWriter.WriteGeometry(geometry, resolvedSrid),
            geometryType,
            spatialRelationship,
            resolvedSrid,
            distance,
            distanceUnit);
    }

    private static void ValidateOptions(
        int? inSrid,
        SpatialRelationship spatialRelationship,
        double? distance,
        FeatureSpatialDistanceUnit? distanceUnit) {
        if (inSrid is <= 0) {
            throw new InvalidOperationException("InSrid must be greater than zero when provided.");
        }

        if (!Enum.IsDefined(spatialRelationship)) {
            throw new InvalidOperationException("SpatialRelationship must be a supported spatial relationship.");
        }

        if (distance.HasValue &&
            (double.IsNaN(distance.Value) || double.IsInfinity(distance.Value))) {
            throw new InvalidOperationException("Distance must be a finite value when provided.");
        }

        if (distance is < 0) {
            throw new InvalidOperationException("Distance must be greater than or equal to zero when provided.");
        }

        if (distanceUnit.HasValue && !Enum.IsDefined(distanceUnit.Value)) {
            throw new InvalidOperationException("DistanceUnit must be a supported spatial distance unit.");
        }

        if (distanceUnit.HasValue && !distance.HasValue) {
            throw new InvalidOperationException("DistanceUnit requires Distance to be specified.");
        }
    }

    private static string ResolveGeometryType(Geometry geometry) {
        return geometry switch {
            Point => EsriGeometryTypes.Point,
            MultiPoint => EsriGeometryTypes.Multipoint,
            LineString => EsriGeometryTypes.Polyline,
            MultiLineString => EsriGeometryTypes.Polyline,
            Polygon => EsriGeometryTypes.Polygon,
            MultiPolygon => EsriGeometryTypes.Polygon,
            _ => throw new NotSupportedException(
                $"Geometry type '{geometry.GetType().Name}' is not supported for Esri query serialization.")
        };
    }
}