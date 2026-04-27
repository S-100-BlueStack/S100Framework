using System.Text.Json;

namespace S100Framework.REST.Internal.Json;

internal static class JsonAttributeValueReader
{
    internal static IReadOnlyDictionary<string, object?> ReadAttributes(
        JsonElement attributesElement,
        JsonAttributeNumberHandling numberHandling) {
        if (attributesElement.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null) {
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        }

        var attributes = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var property in attributesElement.EnumerateObject()) {
            attributes[property.Name] = ConvertValue(property.Value, numberHandling);
        }

        return attributes;
    }

    internal static object? ConvertValue(
        JsonElement value,
        JsonAttributeNumberHandling numberHandling) {
        return value.ValueKind switch {
            JsonValueKind.Undefined => null,
            JsonValueKind.Null => null,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number when value.TryGetInt64(out var intValue) => intValue,
            JsonValueKind.Number when numberHandling == JsonAttributeNumberHandling.DecimalFallback &&
                                      value.TryGetDecimal(out var decimalValue) => decimalValue,
            JsonValueKind.Number => value.GetDouble(),
            JsonValueKind.Array => value
                .EnumerateArray()
                .Select(item => ConvertValue(item, numberHandling))
                .ToArray(),
            JsonValueKind.Object => value
                .EnumerateObject()
                .ToDictionary(
                    property => property.Name,
                    property => ConvertValue(property.Value, numberHandling),
                    StringComparer.OrdinalIgnoreCase),
            _ => value.Clone()
        };
    }
}