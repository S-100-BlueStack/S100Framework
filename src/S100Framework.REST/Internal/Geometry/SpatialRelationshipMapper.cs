using S100Framework.REST.Models;

namespace S100Framework.REST.Internal.EsriGeometry;

internal static class SpatialRelationshipMapper
{
    public static string ToEsriValue(SpatialRelationship relationship) {
        return relationship switch {
            SpatialRelationship.Intersects => EsriSpatialRelationships.Intersects,
            SpatialRelationship.Contains => EsriSpatialRelationships.Contains,
            SpatialRelationship.Crosses => EsriSpatialRelationships.Crosses,
            SpatialRelationship.EnvelopeIntersects => EsriSpatialRelationships.EnvelopeIntersects,
            SpatialRelationship.IndexIntersects => EsriSpatialRelationships.IndexIntersects,
            SpatialRelationship.Overlaps => EsriSpatialRelationships.Overlaps,
            SpatialRelationship.Touches => EsriSpatialRelationships.Touches,
            SpatialRelationship.Within => EsriSpatialRelationships.Within,
            _ => throw new ArgumentOutOfRangeException(nameof(relationship), relationship, null)
        };
    }
}