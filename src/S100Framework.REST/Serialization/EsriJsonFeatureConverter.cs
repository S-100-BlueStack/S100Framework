using System.Globalization;
using System.Text.Json;
using S100Framework.REST.Internal.EsriGeometry;
using S100Framework.REST.Models;

namespace S100Framework.REST.Serialization;

public static class EsriJsonFeatureConverter
{
    public static string SerializeFeature(
        FeatureRecord feature,
        string? objectIdFieldName = null,
        bool includeGeometry = true,
        bool includeSpatialReference = true) {
        ArgumentNullException.ThrowIfNull(feature);

        var payload = CreateFeatureObject(
            feature,
            objectIdFieldName,
            includeGeometry,
            includeSpatialReference);

        return JsonSerializer.Serialize(payload);
    }

    public static JsonElement SerializeFeatureToElement(
        FeatureRecord feature,
        string? objectIdFieldName = null,
        bool includeGeometry = true,
        bool includeSpatialReference = true) {
        ArgumentNullException.ThrowIfNull(feature);

        var payload = CreateFeatureObject(
            feature,
            objectIdFieldName,
            includeGeometry,
            includeSpatialReference);

        return JsonSerializer.SerializeToElement(payload);
    }

    public static string SerializeFeatureSet(
        IEnumerable<FeatureRecord> features,
        FeatureLayerSchema schema,
        bool includeGeometry = true,
        bool includeSpatialReference = true) {
        ArgumentNullException.ThrowIfNull(features);
        ArgumentNullException.ThrowIfNull(schema);

        var featureList = features
            .Select(feature => CreateFeatureObject(
                feature,
                schema.ObjectIdFieldName,
                includeGeometry,
                includeSpatialReference))
            .ToArray();

        var payload = new Dictionary<string, object?> {
            ["geometryType"] = schema.GeometryType,
            ["objectIdFieldName"] = schema.ObjectIdFieldName,
            ["features"] = featureList
        };

        if (includeGeometry && includeSpatialReference && schema.Srid.HasValue && schema.Srid.Value > 0) {
            payload["spatialReference"] = new Dictionary<string, object?> {
                ["wkid"] = schema.Srid.Value
            };
        }

        return JsonSerializer.Serialize(payload);
    }

    private static Dictionary<string, object?> CreateFeatureObject(
        FeatureRecord feature,
        string? objectIdFieldName,
        bool includeGeometry,
        bool includeSpatialReference) {
        var attributes = new Dictionary<string, object?>(
            feature.Attributes,
            StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(objectIdFieldName) &&
            feature.ObjectId.HasValue &&
            !attributes.ContainsKey(objectIdFieldName)) {
            attributes[objectIdFieldName] = feature.ObjectId.Value;
        }

        var payload = new Dictionary<string, object?> {
            ["attributes"] = attributes
        };

        if (includeGeometry) {
            payload["geometry"] = EsriEditGeometryWriter.WriteGeometry(
                feature.Geometry,
                includeSpatialReference);
        }

        return payload;
    }
}