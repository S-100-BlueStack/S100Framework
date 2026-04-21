using NetTopologySuite.Geometries;
using S100Framework.REST.Internal.EsriGeometry;

namespace S100Framework.REST.Models;

/// <summary>
/// Represents a spatial filter used by <c>extractChanges</c>.
/// </summary>
public sealed record ExtractChangesSpatialFilter
{
    internal string GeometryJson { get; }

    internal string GeometryType { get; }

    /// <summary>
    /// Gets the input spatial reference ID, when specified.
    /// </summary>
    public int? InSrid { get; }

    private ExtractChangesSpatialFilter(
        string geometryJson,
        string geometryType,
        int? inSrid) {
        GeometryJson = geometryJson;
        GeometryType = geometryType;
        InSrid = inSrid;
    }

    /// <summary>
    /// Creates a spatial filter from an envelope.
    /// </summary>
    /// <param name="envelope">The envelope to use as the spatial filter.</param>
    /// <param name="inSrid">The input spatial reference ID, when known.</param>
    /// <returns>A spatial filter that serializes as an Esri envelope geometry.</returns>
    public static ExtractChangesSpatialFilter FromEnvelope(Envelope envelope, int? inSrid = null) {
        ArgumentNullException.ThrowIfNull(envelope);

        return new ExtractChangesSpatialFilter(
            EsriExtractChangesGeometryWriter.WriteEnvelopeJson(envelope),
            "esriGeometryEnvelope",
            inSrid);
    }

    /// <summary>
    /// Creates a spatial filter from a NetTopologySuite geometry.
    /// </summary>
    /// <param name="geometry">The geometry to use as the spatial filter.</param>
    /// <param name="inSrid">
    /// The input spatial reference ID. When omitted, the geometry SRID is used if greater than zero.
    /// </param>
    /// <returns>A spatial filter that serializes as the corresponding Esri geometry type.</returns>
    public static ExtractChangesSpatialFilter FromGeometry(Geometry geometry, int? inSrid = null) {
        ArgumentNullException.ThrowIfNull(geometry);

        return new ExtractChangesSpatialFilter(
            EsriExtractChangesGeometryWriter.WriteGeometryJson(geometry),
            EsriExtractChangesGeometryWriter.GetGeometryType(geometry),
            inSrid ?? (geometry.SRID > 0 ? geometry.SRID : null));
    }
}