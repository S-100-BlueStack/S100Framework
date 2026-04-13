using NetTopologySuite.Geometries;
using S100Framework.REST.Internal.EsriGeometry;

namespace S100Framework.REST.Models;

public sealed class FeatureSpatialFilter
{
    private FeatureSpatialFilter(
        string geometryJson,
        string geometryType,
        string? spatialRelation,
        int? inSrid) {
        GeometryJson = geometryJson;
        GeometryType = geometryType;
        SpatialRelation = spatialRelation;
        InSrid = inSrid;
    }

    internal string GeometryJson { get; }

    public string GeometryType { get; }

    public string? SpatialRelation { get; }

    public int? InSrid { get; }

    public static FeatureSpatialFilter FromEnvelope(
        Envelope envelope,
        int? inSrid,
        string? spatialRelation = EsriSpatialRelationships.Intersects) {
        ArgumentNullException.ThrowIfNull(envelope);

        if (envelope.IsNull) {
            throw new InvalidOperationException("Envelope must not be null or empty.");
        }

        return new FeatureSpatialFilter(
            EsriQueryGeometryWriter.WriteEnvelope(envelope, inSrid),
            EsriGeometryTypes.Envelope,
            spatialRelation,
            inSrid);
    }

    public static FeatureSpatialFilter FromGeometry(
        Geometry geometry,
        int? inSrid = null,
        string? spatialRelation = EsriSpatialRelationships.Intersects) {
        ArgumentNullException.ThrowIfNull(geometry);

        if (geometry.IsEmpty) {
            throw new InvalidOperationException("Geometry must not be empty.");
        }

        var resolvedSrid = inSrid ?? (geometry.SRID > 0 ? geometry.SRID : null);
        var geometryType = ResolveGeometryType(geometry);

        return new FeatureSpatialFilter(
            EsriQueryGeometryWriter.WriteGeometry(geometry, resolvedSrid),
            geometryType,
            spatialRelation,
            resolvedSrid);
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