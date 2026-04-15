using System.Text.Json;
using NetTopologySuite.Geometries;
using S100Framework.REST.Configuration;
using S100Framework.REST.Internal.EsriGeometry;

namespace S100Framework.REST.Serialization;

public static class EsriJsonGeometryConverter
{
    public static string Serialize(
        Geometry geometry,
        bool includeSpatialReference = true) {
        ArgumentNullException.ThrowIfNull(geometry);

        var payload = EsriEditGeometryWriter.WriteGeometry(geometry, includeSpatialReference);
        return JsonSerializer.Serialize(payload);
    }

    public static JsonElement SerializeToElement(
        Geometry geometry,
        bool includeSpatialReference = true) {
        ArgumentNullException.ThrowIfNull(geometry);

        var payload = EsriEditGeometryWriter.WriteGeometry(geometry, includeSpatialReference);
        return JsonSerializer.SerializeToElement(payload);
    }

    public static Geometry? Deserialize(
        string esriJson,
        string? geometryType = null,
        int? defaultSrid = null,
        bool preferLatestWkid = true,
        bool fixInvalidGeometries = false,
        TrueCurveHandling trueCurveHandling = TrueCurveHandling.Throw,
        int circularArcSegmentCount = 16) {
        if (string.IsNullOrWhiteSpace(esriJson)) {
            throw new ArgumentException("Esri JSON must be provided.", nameof(esriJson));
        }

        using var document = JsonDocument.Parse(esriJson);

        return Deserialize(
            document.RootElement,
            geometryType,
            defaultSrid,
            preferLatestWkid,
            fixInvalidGeometries,
            trueCurveHandling,
            circularArcSegmentCount);
    }

    public static Geometry? Deserialize(
        JsonElement esriJson,
        string? geometryType = null,
        int? defaultSrid = null,
        bool preferLatestWkid = true,
        bool fixInvalidGeometries = false,
        TrueCurveHandling trueCurveHandling = TrueCurveHandling.Throw,
        int circularArcSegmentCount = 16) {
        return EsriGeometryReader.Read(
            esriJson,
            geometryType,
            defaultSrid,
            preferLatestWkid,
            fixInvalidGeometries,
            trueCurveHandling,
            circularArcSegmentCount);
    }
}