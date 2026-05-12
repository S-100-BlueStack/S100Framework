using System.Globalization;
using System.Text.Json;

namespace S100Framework.REST.Internal.Replica;

internal static class ReplicaGenerationJsonReader
{
    public static ReplicaGenerationJsonResult Read(
        byte[] content,
        Uri resultUrl,
        string operationName) {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(resultUrl);

        if (string.IsNullOrWhiteSpace(operationName)) {
            throw new ArgumentException("Operation name must be provided.", nameof(operationName));
        }

        if (content.Length == 0) {
            throw new InvalidOperationException(
                $"The downloaded {operationName} JSON file is empty.");
        }

        using var document = ParseJsonFile(content, operationName);
        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Object) {
            throw new InvalidOperationException(
                $"The downloaded {operationName} JSON file must contain a JSON object.");
        }

        return new ReplicaGenerationJsonResult(
            ReplicaId: ReadOptionalString(root, "replicaID"),
            ReplicaName: ReadOptionalString(root, "replicaName"),
            TransportType: ReadOptionalString(root, "transportType"),
            ResponseType: ReadOptionalString(root, "responseType"),
            SyncModel: ReadOptionalString(root, "syncModel"),
            TargetType: ReadOptionalString(root, "targetType"),
            ReplicaServerGen: ReadOptionalInt64(root, "replicaServerGen", resultUrl, operationName),
            LayerServerGens: ReadLayerServerGens(root, resultUrl, operationName));
    }

    private static JsonDocument ParseJsonFile(
        byte[] content,
        string operationName) {
        try {
            return JsonDocument.Parse(content);
        }
        catch (JsonException exception) {
            throw new InvalidOperationException(
                $"The downloaded {operationName} JSON file could not be parsed.",
                exception);
        }
    }

    private static string? ReadOptionalString(
        JsonElement root,
        string propertyName) {
        if (!root.TryGetProperty(propertyName, out var property) ||
            property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) {
            return null;
        }

        if (property.ValueKind != JsonValueKind.String) {
            return null;
        }

        var value = property.GetString();

        return string.IsNullOrWhiteSpace(value)
            ? null
            : value;
    }

    private static long? ReadOptionalInt64(
        JsonElement root,
        string propertyName,
        Uri resultUrl,
        string operationName) {
        if (!root.TryGetProperty(propertyName, out var property) ||
            property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) {
            return null;
        }

        if (!TryReadInt64(property, out var value)) {
            throw new InvalidOperationException(
                $"The downloaded {operationName} JSON file contains an invalid {propertyName} value. Result URL: {resultUrl}");
        }

        if (value < 0) {
            throw new InvalidOperationException(
                $"The downloaded {operationName} JSON file contains a negative {propertyName} value. Result URL: {resultUrl}");
        }

        return value;
    }

    private static IReadOnlyList<ReplicaLayerGenerationJsonResult> ReadLayerServerGens(
        JsonElement root,
        Uri resultUrl,
        string operationName) {
        if (!root.TryGetProperty("layerServerGens", out var property) ||
            property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) {
            return Array.Empty<ReplicaLayerGenerationJsonResult>();
        }

        if (property.ValueKind != JsonValueKind.Array) {
            throw new InvalidOperationException(
                $"The downloaded {operationName} JSON file contains an invalid layerServerGens value. Result URL: {resultUrl}");
        }

        var values = new List<ReplicaLayerGenerationJsonResult>();

        foreach (var item in property.EnumerateArray()) {
            if (item.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) {
                continue;
            }

            if (item.ValueKind != JsonValueKind.Object) {
                throw new InvalidOperationException(
                    $"The downloaded {operationName} JSON file contains an invalid layerServerGens item. Result URL: {resultUrl}");
            }

            var id = ReadRequiredInt32(item, "id", "layerServerGens", resultUrl, operationName);
            var serverGen = ReadRequiredInt64(item, "serverGen", "layerServerGens", resultUrl, operationName);

            if (id < 0) {
                throw new InvalidOperationException(
                    $"The downloaded {operationName} JSON file contains a negative layer ID. Result URL: {resultUrl}");
            }

            if (serverGen < 0) {
                throw new InvalidOperationException(
                    $"The downloaded {operationName} JSON file contains a negative serverGen value. Result URL: {resultUrl}");
            }

            values.Add(new ReplicaLayerGenerationJsonResult(id, serverGen));
        }

        return values;
    }

    private static int ReadRequiredInt32(
        JsonElement root,
        string propertyName,
        string containerName,
        Uri resultUrl,
        string operationName) {
        var value = ReadRequiredInt64(root, propertyName, containerName, resultUrl, operationName);

        if (value < int.MinValue || value > int.MaxValue) {
            throw new InvalidOperationException(
                $"The downloaded {operationName} JSON file contains an out-of-range {propertyName} value in {containerName}. Result URL: {resultUrl}");
        }

        return (int)value;
    }

    private static long ReadRequiredInt64(
        JsonElement root,
        string propertyName,
        string containerName,
        Uri resultUrl,
        string operationName) {
        if (!root.TryGetProperty(propertyName, out var property) ||
            property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ||
            !TryReadInt64(property, out var value)) {
            throw new InvalidOperationException(
                $"The downloaded {operationName} JSON file contains a {containerName} item without a valid {propertyName} value. Result URL: {resultUrl}");
        }

        return value;
    }

    private static bool TryReadInt64(
        JsonElement element,
        out long value) {
        value = 0;

        return element.ValueKind switch {
            JsonValueKind.Number => element.TryGetInt64(out value),
            JsonValueKind.String => long.TryParse(
                element.GetString(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out value),
            _ => false
        };
    }
}