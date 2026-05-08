using System.Text.Json;
using System.Text.Json.Serialization;

namespace S100Framework.REST.Internal.Json;

internal sealed class EsriStringListJsonConverter : JsonConverter<List<string>?>
{
    public override List<string>? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) {
        if (reader.TokenType == JsonTokenType.Null) {
            return null;
        }

        if (reader.TokenType == JsonTokenType.String) {
            return ParseDelimitedValue(reader.GetString());
        }

        if (reader.TokenType == JsonTokenType.StartArray) {
            return ReadStringArray(ref reader);
        }

        throw new JsonException($"Expected string, array, or null, but got {reader.TokenType}.");
    }

    public override void Write(
        Utf8JsonWriter writer,
        List<string>? value,
        JsonSerializerOptions options) {
        if (value is null) {
            writer.WriteNullValue();
            return;
        }

        // ArcGIS commonly exposes these metadata values as comma-separated strings.
        writer.WriteStringValue(string.Join(",", value));
    }

    private static List<string> ParseDelimitedValue(string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return [];
        }

        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .ToList();
    }

    private static List<string> ReadStringArray(ref Utf8JsonReader reader) {
        var values = new List<string>();

        while (reader.Read()) {
            if (reader.TokenType == JsonTokenType.EndArray) {
                return values;
            }

            if (reader.TokenType == JsonTokenType.Null) {
                continue;
            }

            if (reader.TokenType != JsonTokenType.String) {
                throw new JsonException($"Expected string or null value in array, but got {reader.TokenType}.");
            }

            var value = reader.GetString();

            if (!string.IsNullOrWhiteSpace(value)) {
                values.Add(value.Trim());
            }
        }

        throw new JsonException("Unexpected end of JSON while reading string array.");
    }
}
