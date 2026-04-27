using System.Globalization;
using System.Text.Json;
using S100Framework.REST.Internal.Dto;
using S100Framework.REST.Internal.EsriGeometry;
using S100Framework.REST.Models;

namespace S100Framework.REST.Clients;

/// <summary>
/// Provides shared DTO-to-model mapping helpers for <see cref="FeatureLayerClient" />.
/// </summary>
public sealed partial class FeatureLayerClient
{
    private FeatureRecord MapFeature(FeatureLayerSchema schema, EsriFeatureDto feature) {
        var attributes = ReadAttributes(feature.Attributes);

        var geometry = feature.Geometry.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
            ? null
            : EsriGeometryReader.Read(
                feature.Geometry,
                schema.GeometryType,
                schema.Srid,
                _serviceClient.Options.PreferLatestWkid,
                _serviceClient.Options.FixInvalidGeometries,
                _serviceClient.Options.TrueCurveHandling,
                _serviceClient.Options.CircularArcSegmentCount);

        long? objectId = null;

        if (!string.IsNullOrWhiteSpace(schema.ObjectIdFieldName) &&
            attributes.TryGetValue(schema.ObjectIdFieldName, out var rawObjectId)) {
            objectId = ConvertToInt64(rawObjectId);
        }

        return new FeatureRecord(geometry, attributes, objectId);
    }

    private FeatureRecord MapRelatedRecord(
        EsriFeatureDto feature,
        string? geometryType,
        int? defaultSrid,
        string? objectIdFieldName) {
        var attributes = ReadAttributes(feature.Attributes);

        var geometry = feature.Geometry.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
            ? null
            : EsriGeometryReader.Read(
                feature.Geometry,
                geometryType,
                defaultSrid,
                _serviceClient.Options.PreferLatestWkid,
                _serviceClient.Options.FixInvalidGeometries,
                _serviceClient.Options.TrueCurveHandling,
                _serviceClient.Options.CircularArcSegmentCount);

        long? objectId = null;

        if (!string.IsNullOrWhiteSpace(objectIdFieldName) &&
            attributes.TryGetValue(objectIdFieldName, out var rawObjectId)) {
            objectId = ConvertToInt64(rawObjectId);
        }

        return new FeatureRecord(geometry, attributes, objectId);
    }

    private AttachmentInfo MapAttachmentInfo(
        JsonElement attachmentInfoElement,
        long? parentObjectId,
        string? parentGlobalId) {
        var attributes = ReadObjectAttributes(attachmentInfoElement);

        long attachmentId = 0;

        if (TryGetInt64(attributes, "id", out var id)) {
            attachmentId = id;
        }
        else if (TryGetInt64(attributes, "attachmentid", out var attachmentIdAlias)) {
            attachmentId = attachmentIdAlias;
        }

        string? globalId = TryGetString(attributes, "globalId") ?? TryGetString(attributes, "globalid");
        string? name = TryGetString(attributes, "name") ?? TryGetString(attributes, "att_name");
        string? contentType = TryGetString(attributes, "contentType") ?? TryGetString(attributes, "content_type");
        long? size = TryGetNullableInt64(attributes, "size") ?? TryGetNullableInt64(attributes, "data_size");
        string? url = TryGetString(attributes, "url");

        return new AttachmentInfo(
            attachmentId,
            parentObjectId,
            parentGlobalId,
            name,
            contentType,
            size,
            globalId,
            url,
            attributes);
    }

    private int? ResolveSrid(EsriSpatialReferenceDto? spatialReference) {
        if (spatialReference is null) {
            return null;
        }

        return _serviceClient.Options.PreferLatestWkid
            ? spatialReference.LatestWkid ?? spatialReference.Wkid
            : spatialReference.Wkid ?? spatialReference.LatestWkid;
    }

    private static IReadOnlyDictionary<string, object?> ReadObjectAttributes(JsonElement objectElement) {
        if (objectElement.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null) {
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        }

        var attributes = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var property in objectElement.EnumerateObject()) {
            attributes[property.Name] = ConvertJsonValue(property.Value);
        }

        return attributes;
    }

    private static IReadOnlyDictionary<string, object?> ReadAttributes(JsonElement attributesElement) {
        if (attributesElement.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null) {
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        }

        var attributes = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var property in attributesElement.EnumerateObject()) {
            attributes[property.Name] = ConvertJsonValue(property.Value);
        }

        return attributes;
    }

    private static object? ConvertJsonValue(JsonElement value) {
        return value.ValueKind switch {
            JsonValueKind.Undefined => null,
            JsonValueKind.Null => null,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number when value.TryGetInt64(out var intValue) => intValue,
            JsonValueKind.Number when value.TryGetDecimal(out var decimalValue) => decimalValue,
            JsonValueKind.Number => value.GetDouble(),
            _ => value.Clone()
        };
    }

    private static bool TryGetInt64(
        IReadOnlyDictionary<string, object?> attributes,
        string name,
        out long value) {
        value = default;

        if (!attributes.TryGetValue(name, out var raw)) {
            return false;
        }

        var converted = ConvertToInt64(raw);

        if (!converted.HasValue) {
            return false;
        }

        value = converted.Value;
        return true;
    }

    private static long? TryGetNullableInt64(
        IReadOnlyDictionary<string, object?> attributes,
        string name) {
        return attributes.TryGetValue(name, out var raw)
            ? ConvertToInt64(raw)
            : null;
    }

    private static string? TryGetString(
        IReadOnlyDictionary<string, object?> attributes,
        string name) {
        if (!attributes.TryGetValue(name, out var raw)) {
            return null;
        }

        return raw switch {
            null => null,
            string stringValue => stringValue,
            _ => Convert.ToString(raw, CultureInfo.InvariantCulture)
        };
    }

    private static long? ConvertToInt64(object? value) {
        return value switch {
            null => null,
            long longValue => longValue,
            int intValue => intValue,
            decimal decimalValue => (long)decimalValue,
            double doubleValue => (long)doubleValue,
            string stringValue when long.TryParse(stringValue, out var parsed) => parsed,
            _ => null
        };
    }
}