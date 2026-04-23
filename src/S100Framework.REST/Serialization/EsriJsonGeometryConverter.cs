using System.Text.Json;
using NetTopologySuite.Geometries;
using S100Framework.REST.Configuration;
using S100Framework.REST.Internal.EsriGeometry;

namespace S100Framework.REST.Serialization;

/// <summary>
/// Serializes and deserializes NetTopologySuite geometries to and from Esri JSON.
/// </summary>
public static class EsriJsonGeometryConverter
{
    /// <summary>
    /// Serializes a geometry to an Esri JSON string.
    /// </summary>
    /// <param name="geometry">
    /// The geometry to serialize.
    /// </param>
    /// <param name="includeSpatialReference">
    /// <see langword="true"/> to include spatial reference information in the serialized payload.
    /// </param>
    /// <returns>
    /// The serialized Esri JSON geometry payload.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="geometry"/> is <see langword="null"/>.
    /// </exception>
    public static string Serialize(
        Geometry geometry,
        bool includeSpatialReference = true) {
        ArgumentNullException.ThrowIfNull(geometry);

        var payload = EsriEditGeometryWriter.WriteGeometry(geometry, includeSpatialReference);
        return JsonSerializer.Serialize(payload);
    }

    /// <summary>
    /// Serializes a geometry to an Esri JSON element.
    /// </summary>
    /// <param name="geometry">
    /// The geometry to serialize.
    /// </param>
    /// <param name="includeSpatialReference">
    /// <see langword="true"/> to include spatial reference information in the serialized payload.
    /// </param>
    /// <returns>
    /// The serialized Esri JSON geometry payload as a <see cref="JsonElement"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="geometry"/> is <see langword="null"/>.
    /// </exception>
    public static JsonElement SerializeToElement(
        Geometry geometry,
        bool includeSpatialReference = true) {
        ArgumentNullException.ThrowIfNull(geometry);

        var payload = EsriEditGeometryWriter.WriteGeometry(geometry, includeSpatialReference);
        return JsonSerializer.SerializeToElement(payload);
    }

    /// <summary>
    /// Deserializes Esri JSON text to a NetTopologySuite geometry.
    /// </summary>
    /// <param name="esriJson">
    /// The Esri JSON geometry payload.
    /// </param>
    /// <param name="geometryType">
    /// The ArcGIS geometry type identifier, when it is known externally.
    /// </param>
    /// <param name="defaultSrid">
    /// The fallback SRID to apply when the payload does not contain one.
    /// </param>
    /// <param name="preferLatestWkid">
    /// <see langword="true"/> to prefer <c>latestWkid</c> over legacy <c>wkid</c> when both are present.
    /// </param>
    /// <param name="fixInvalidGeometries">
    /// <see langword="true"/> to attempt geometry repair when the payload produces invalid geometry.
    /// </param>
    /// <param name="trueCurveHandling">
    /// Controls how true curves should be handled.
    /// </param>
    /// <param name="circularArcSegmentCount">
    /// The segment count to use when circular arcs are linearized.
    /// </param>
    /// <returns>
    /// The deserialized geometry, or <see langword="null"/> when the payload represents no geometry.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="esriJson"/> is empty or whitespace.
    /// </exception>
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

    /// <summary>
    /// Deserializes an Esri JSON element to a NetTopologySuite geometry.
    /// </summary>
    /// <param name="esriJson">
    /// The Esri JSON geometry payload.
    /// </param>
    /// <param name="geometryType">
    /// The ArcGIS geometry type identifier, when it is known externally.
    /// </param>
    /// <param name="defaultSrid">
    /// The fallback SRID to apply when the payload does not contain one.
    /// </param>
    /// <param name="preferLatestWkid">
    /// <see langword="true"/> to prefer <c>latestWkid</c> over legacy <c>wkid</c> when both are present.
    /// </param>
    /// <param name="fixInvalidGeometries">
    /// <see langword="true"/> to attempt geometry repair when the payload produces invalid geometry.
    /// </param>
    /// <param name="trueCurveHandling">
    /// Controls how true curves should be handled.
    /// </param>
    /// <param name="circularArcSegmentCount">
    /// The segment count to use when circular arcs are linearized.
    /// </param>
    /// <returns>
    /// The deserialized geometry, or <see langword="null"/> when the payload represents no geometry.
    /// </returns>
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