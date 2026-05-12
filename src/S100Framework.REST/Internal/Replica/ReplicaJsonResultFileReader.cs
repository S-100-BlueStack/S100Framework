using System.Globalization;
using System.Text;
using System.Text.Json;
using S100Framework.REST.Models;

namespace S100Framework.REST.Internal.Replica;

internal static class ReplicaJsonResultFileReader
{
    public static ReplicaJsonResultFile Read(
        byte[] content,
        Uri resultUrl,
        string operationName) {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(resultUrl);

        var generationResult = ReplicaGenerationJsonReader.Read(
            content,
            resultUrl,
            operationName);

        using var document = ParseJson(content, operationName);
        var root = document.RootElement;

        return new ReplicaJsonResultFile(
            ReplicaId: generationResult.ReplicaId,
            ReplicaName: generationResult.ReplicaName,
            TransportType: generationResult.TransportType,
            ResponseType: generationResult.ResponseType,
            SyncModel: generationResult.SyncModel,
            TargetType: generationResult.TargetType,
            ReplicaServerGen: generationResult.ReplicaServerGen,
            LayerServerGens: generationResult.LayerServerGens
                .Select(static value => new ReplicaLayerServerGeneration(value.Id, value.ServerGen))
                .ToArray(),
            Layers: ReadLayers(root, resultUrl, operationName),
            RawJson: Encoding.UTF8.GetString(content),
            ResultUrl: resultUrl);
    }

    private static JsonDocument ParseJson(
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

    private static IReadOnlyList<ReplicaJsonResultLayer> ReadLayers(
        JsonElement root,
        Uri resultUrl,
        string operationName) {
        if (root.ValueKind != JsonValueKind.Object) {
            return Array.Empty<ReplicaJsonResultLayer>();
        }

        if (root.TryGetProperty("edits", out var editsElement)) {
            return ReadLayerCollection(editsElement, "edits", resultUrl, operationName);
        }

        if (root.TryGetProperty("layers", out var layersElement)) {
            return ReadLayerCollection(layersElement, "layers", resultUrl, operationName);
        }

        return Array.Empty<ReplicaJsonResultLayer>();
    }

    private static IReadOnlyList<ReplicaJsonResultLayer> ReadLayerCollection(
        JsonElement collectionElement,
        string collectionName,
        Uri resultUrl,
        string operationName) {
        if (collectionElement.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) {
            return Array.Empty<ReplicaJsonResultLayer>();
        }

        if (collectionElement.ValueKind != JsonValueKind.Array) {
            throw new InvalidOperationException(
                $"The downloaded {operationName} JSON file contains an invalid {collectionName} value. Result URL: {resultUrl}");
        }

        var layers = new List<ReplicaJsonResultLayer>();

        foreach (var layerElement in collectionElement.EnumerateArray()) {
            if (layerElement.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) {
                continue;
            }

            if (layerElement.ValueKind != JsonValueKind.Object) {
                throw new InvalidOperationException(
                    $"The downloaded {operationName} JSON file contains an invalid {collectionName} item. Result URL: {resultUrl}");
            }

            layers.Add(ReadLayer(layerElement, collectionName, resultUrl, operationName));
        }

        return layers;
    }

    private static ReplicaJsonResultLayer ReadLayer(
        JsonElement layerElement,
        string collectionName,
        Uri resultUrl,
        string operationName) {
        var id = ReadRequiredInt32(
            layerElement,
            "id",
            collectionName,
            resultUrl,
            operationName);

        if (id < 0) {
            throw new InvalidOperationException(
                $"The downloaded {operationName} JSON file contains a negative layer ID. Result URL: {resultUrl}");
        }

        return new ReplicaJsonResultLayer(
            Id: id,
            AddResults: ReadEditResults(layerElement, "addResults", resultUrl, operationName),
            UpdateResults: ReadEditResults(layerElement, "updateResults", resultUrl, operationName),
            DeleteResults: ReadEditResults(layerElement, "deleteResults", resultUrl, operationName),
            RawJson: layerElement.GetRawText());
    }

    private static IReadOnlyList<ReplicaEditResult> ReadEditResults(
        JsonElement layerElement,
        string propertyName,
        Uri resultUrl,
        string operationName) {
        if (!layerElement.TryGetProperty(propertyName, out var resultsElement) ||
            resultsElement.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) {
            return Array.Empty<ReplicaEditResult>();
        }

        if (resultsElement.ValueKind != JsonValueKind.Array) {
            throw new InvalidOperationException(
                $"The downloaded {operationName} JSON file contains an invalid {propertyName} value. Result URL: {resultUrl}");
        }

        var results = new List<ReplicaEditResult>();

        foreach (var item in resultsElement.EnumerateArray()) {
            if (item.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) {
                continue;
            }

            if (item.ValueKind != JsonValueKind.Object) {
                throw new InvalidOperationException(
                    $"The downloaded {operationName} JSON file contains an invalid {propertyName} item. Result URL: {resultUrl}");
            }

            results.Add(ReadEditResult(item, propertyName, resultUrl, operationName));
        }

        return results;
    }

    private static ReplicaEditResult ReadEditResult(
        JsonElement item,
        string collectionName,
        Uri resultUrl,
        string operationName) {
        var objectId = ReadOptionalInt64(item, "objectId", resultUrl, operationName) ??
                       ReadOptionalInt64(item, "objectID", resultUrl, operationName);

        if (objectId is < 0) {
            throw new InvalidOperationException(
                $"The downloaded {operationName} JSON file contains a negative objectId value in {collectionName}. Result URL: {resultUrl}");
        }

        var success = ReadOptionalBoolean(item, "success", resultUrl, operationName);
        var error = ReadOptionalError(item, resultUrl, operationName);

        return new ReplicaEditResult(
            ObjectId: objectId,
            GlobalId: ReadOptionalString(item, "globalId") ?? ReadOptionalString(item, "globalID"),
            Success: success,
            ErrorCode: error.Code,
            ErrorDescription: error.Description,
            ErrorDetails: error.Details,
            RawJson: item.GetRawText());
    }

    private static ReplicaEditResultError ReadOptionalError(
        JsonElement item,
        Uri resultUrl,
        string operationName) {
        if (!item.TryGetProperty("error", out var errorElement) ||
            errorElement.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) {
            return ReplicaEditResultError.Empty;
        }

        if (errorElement.ValueKind != JsonValueKind.Object) {
            throw new InvalidOperationException(
                $"The downloaded {operationName} JSON file contains an invalid edit result error value. Result URL: {resultUrl}");
        }

        return new ReplicaEditResultError(
            Code: ReadOptionalInt32(errorElement, "code", resultUrl, operationName),
            Description: ReadOptionalString(errorElement, "description") ??
                         ReadOptionalString(errorElement, "message"),
            Details: ReadOptionalStringArray(errorElement, "details", resultUrl, operationName));
    }

    private static IReadOnlyList<string> ReadOptionalStringArray(
        JsonElement root,
        string propertyName,
        Uri resultUrl,
        string operationName) {
        if (!root.TryGetProperty(propertyName, out var element) ||
            element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) {
            return Array.Empty<string>();
        }

        if (element.ValueKind != JsonValueKind.Array) {
            throw new InvalidOperationException(
                $"The downloaded {operationName} JSON file contains an invalid {propertyName} value. Result URL: {resultUrl}");
        }

        return element
            .EnumerateArray()
            .Where(static item => item.ValueKind == JsonValueKind.String)
            .Select(static item => item.GetString())
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value!)
            .ToArray();
    }

    private static string? ReadOptionalString(
        JsonElement root,
        string propertyName) {
        if (!root.TryGetProperty(propertyName, out var element) ||
            element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) {
            return null;
        }

        if (element.ValueKind != JsonValueKind.String) {
            return null;
        }

        var value = element.GetString();

        return string.IsNullOrWhiteSpace(value)
            ? null
            : value;
    }

    private static bool? ReadOptionalBoolean(
        JsonElement root,
        string propertyName,
        Uri resultUrl,
        string operationName) {
        if (!root.TryGetProperty(propertyName, out var element) ||
            element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) {
            return null;
        }

        if (element.ValueKind is JsonValueKind.True or JsonValueKind.False) {
            return element.GetBoolean();
        }

        throw new InvalidOperationException(
            $"The downloaded {operationName} JSON file contains an invalid {propertyName} value. Result URL: {resultUrl}");
    }

    private static int? ReadOptionalInt32(
        JsonElement root,
        string propertyName,
        Uri resultUrl,
        string operationName) {
        var value = ReadOptionalInt64(root, propertyName, resultUrl, operationName);

        if (!value.HasValue) {
            return null;
        }

        if (value.Value < int.MinValue || value.Value > int.MaxValue) {
            throw new InvalidOperationException(
                $"The downloaded {operationName} JSON file contains an out-of-range {propertyName} value. Result URL: {resultUrl}");
        }

        return (int)value.Value;
    }

    private static long? ReadOptionalInt64(
        JsonElement root,
        string propertyName,
        Uri resultUrl,
        string operationName) {
        if (!root.TryGetProperty(propertyName, out var element) ||
            element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) {
            return null;
        }

        if (!TryReadInt64(element, out var value)) {
            throw new InvalidOperationException(
                $"The downloaded {operationName} JSON file contains an invalid {propertyName} value. Result URL: {resultUrl}");
        }

        return value;
    }

    private static int ReadRequiredInt32(
        JsonElement root,
        string propertyName,
        string containerName,
        Uri resultUrl,
        string operationName) {
        var value = ReadOptionalInt64(root, propertyName, resultUrl, operationName);

        if (!value.HasValue || value.Value < int.MinValue || value.Value > int.MaxValue) {
            throw new InvalidOperationException(
                $"The downloaded {operationName} JSON file contains a {containerName} item without a valid {propertyName} value. Result URL: {resultUrl}");
        }

        return (int)value.Value;
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

    private sealed record ReplicaEditResultError(
        int? Code,
        string? Description,
        IReadOnlyList<string> Details)
    {
        public static ReplicaEditResultError Empty { get; } = new(null, null, Array.Empty<string>());
    }
}