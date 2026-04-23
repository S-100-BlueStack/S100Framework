using System.Globalization;
using System.Text.Json;
using S100Framework.REST.Internal.EsriGeometry;
using S100Framework.REST.Models;

namespace S100Framework.REST.Serialization;

/// <summary>
/// Serializes feature records and feature sets to Esri JSON.
/// </summary>
public static class EsriJsonFeatureConverter
{
    /// <summary>
    /// Serializes a single <see cref="FeatureRecord"/> to an Esri JSON string.
    /// </summary>
    /// <param name="feature">
    /// The feature to serialize.
    /// </param>
    /// <param name="objectIdFieldName">
    /// The object ID field name to inject when the feature has an <see cref="FeatureRecord.ObjectId"/>
    /// value but the attribute bag does not already contain the field.
    /// </param>
    /// <param name="includeGeometry">
    /// <see langword="true"/> to include geometry in the serialized payload.
    /// </param>
    /// <param name="includeSpatialReference">
    /// <see langword="true"/> to include spatial reference information when geometry is serialized.
    /// </param>
    /// <returns>
    /// The serialized Esri JSON feature payload.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="feature"/> is <see langword="null"/>.
    /// </exception>
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

    /// <summary>
    /// Serializes a single <see cref="FeatureRecord"/> to an Esri JSON element.
    /// </summary>
    /// <param name="feature">
    /// The feature to serialize.
    /// </param>
    /// <param name="objectIdFieldName">
    /// The object ID field name to inject when the feature has an <see cref="FeatureRecord.ObjectId"/>
    /// value but the attribute bag does not already contain the field.
    /// </param>
    /// <param name="includeGeometry">
    /// <see langword="true"/> to include geometry in the serialized payload.
    /// </param>
    /// <param name="includeSpatialReference">
    /// <see langword="true"/> to include spatial reference information when geometry is serialized.
    /// </param>
    /// <returns>
    /// The serialized Esri JSON feature payload as a <see cref="JsonElement"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="feature"/> is <see langword="null"/>.
    /// </exception>
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

    /// <summary>
    /// Serializes a sequence of <see cref="FeatureRecord"/> values to an Esri JSON feature set string.
    /// </summary>
    /// <param name="features">
    /// The features to serialize.
    /// </param>
    /// <param name="schema">
    /// The schema that describes the feature set.
    /// </param>
    /// <param name="includeGeometry">
    /// <see langword="true"/> to include geometry in the serialized payload.
    /// </param>
    /// <param name="includeSpatialReference">
    /// <see langword="true"/> to include spatial reference information when geometry is serialized.
    /// </param>
    /// <returns>
    /// The serialized Esri JSON feature set payload.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="features"/> or <paramref name="schema"/> is <see langword="null"/>.
    /// </exception>
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

        var payload = new Dictionary<string, object?>(StringComparer.Ordinal) {
            ["geometryType"] = schema.GeometryType,
            ["objectIdFieldName"] = schema.ObjectIdFieldName,
            ["features"] = featureList
        };

        if (includeGeometry && includeSpatialReference && schema.Srid.HasValue && schema.Srid.Value > 0) {
            payload["spatialReference"] = new Dictionary<string, object?>(StringComparer.Ordinal) {
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

        var payload = new Dictionary<string, object?>(StringComparer.Ordinal) {
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