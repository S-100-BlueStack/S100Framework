using NetTopologySuite.Geometries;
using S100Framework.REST.Internal.EsriGeometry;

namespace S100Framework.REST.Models;

public sealed record ExtractChangesSpatialFilter
{
    internal string GeometryJson { get; }

    internal string GeometryType { get; }

    public int? InSrid { get; }

    private ExtractChangesSpatialFilter(
        string geometryJson,
        string geometryType,
        int? inSrid) {
        GeometryJson = geometryJson;
        GeometryType = geometryType;
        InSrid = inSrid;
    }

    public static ExtractChangesSpatialFilter FromEnvelope(
        Envelope envelope,
        int? inSrid = null) {
        ArgumentNullException.ThrowIfNull(envelope);

        return new ExtractChangesSpatialFilter(
            EsriExtractChangesGeometryWriter.WriteEnvelopeJson(envelope),
            "esriGeometryEnvelope",
            inSrid);
    }

    public static ExtractChangesSpatialFilter FromGeometry(
        Geometry geometry,
        int? inSrid = null) {
        ArgumentNullException.ThrowIfNull(geometry);

        return new ExtractChangesSpatialFilter(
            EsriExtractChangesGeometryWriter.WriteGeometryJson(geometry),
            EsriExtractChangesGeometryWriter.GetGeometryType(geometry),
            inSrid ?? (geometry.SRID > 0 ? geometry.SRID : null));
    }
}