using System.Text.Json;

namespace S100Framework.REST.Models;

/// <summary>
/// Represents schema-agnostic edits for a single feature service layer or table in a replica sync payload.
/// </summary>
/// <remarks>
/// The edit collections are accepted as raw JSON arrays to keep the package independent of any specific
/// feature schema, geometry type, or attachment workflow.
/// </remarks>
public sealed record ReplicaLayerEdits
{
    /// <summary>
    /// Gets the layer or table ID.
    /// </summary>
    public required int Id { get; init; }

    /// <summary>
    /// Gets raw JSON for feature additions, when provided.
    /// </summary>
    /// <remarks>
    /// The value must contain a JSON array that matches the ArcGIS edit feature format expected by the service.
    /// </remarks>
    public string? AddsJson { get; init; }

    /// <summary>
    /// Gets raw JSON for feature updates, when provided.
    /// </summary>
    /// <remarks>
    /// The value must contain a JSON array that matches the ArcGIS edit feature format expected by the service.
    /// </remarks>
    public string? UpdatesJson { get; init; }

    /// <summary>
    /// Gets raw JSON for feature deletions, when provided.
    /// </summary>
    /// <remarks>
    /// The value must contain a JSON array that matches the ArcGIS delete format expected by the service.
    /// </remarks>
    public string? DeletesJson { get; init; }

    /// <summary>
    /// Gets raw JSON for attachment edits, when provided.
    /// </summary>
    /// <remarks>
    /// The value must contain a JSON object or array that matches the ArcGIS attachment edit format expected by the service.
    /// </remarks>
    public string? AttachmentsJson { get; init; }

    /// <summary>
    /// Validates the layer edit payload.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the edit payload is incomplete or invalid.
    /// </exception>
    public void Validate() {
        if (Id < 0) {
            throw new InvalidOperationException("Replica layer edit Id must not be negative.");
        }

        var hasAdds = !string.IsNullOrWhiteSpace(AddsJson);
        var hasUpdates = !string.IsNullOrWhiteSpace(UpdatesJson);
        var hasDeletes = !string.IsNullOrWhiteSpace(DeletesJson);
        var hasAttachments = !string.IsNullOrWhiteSpace(AttachmentsJson);

        if (!hasAdds && !hasUpdates && !hasDeletes && !hasAttachments) {
            throw new InvalidOperationException(
                "Replica layer edits must contain at least one edit collection.");
        }

        if (AddsJson is not null) {
            ValidateJsonArray(AddsJson, nameof(AddsJson));
        }

        if (UpdatesJson is not null) {
            ValidateJsonArray(UpdatesJson, nameof(UpdatesJson));
        }

        if (DeletesJson is not null) {
            ValidateJsonArray(DeletesJson, nameof(DeletesJson));
        }

        if (AttachmentsJson is not null) {
            ValidateJsonObjectOrArray(AttachmentsJson, nameof(AttachmentsJson));
        }
    }

    internal void WriteTo(Utf8JsonWriter writer) {
        Validate();

        writer.WriteStartObject();
        writer.WriteNumber("id", Id);

        WriteRawProperty(writer, "adds", AddsJson);
        WriteRawProperty(writer, "updates", UpdatesJson);
        WriteRawProperty(writer, "deletes", DeletesJson);
        WriteRawProperty(writer, "attachments", AttachmentsJson);

        writer.WriteEndObject();
    }

    private static void WriteRawProperty(
        Utf8JsonWriter writer,
        string propertyName,
        string? rawJson) {
        if (string.IsNullOrWhiteSpace(rawJson)) {
            return;
        }

        writer.WritePropertyName(propertyName);
        using var document = JsonDocument.Parse(rawJson);
        document.RootElement.WriteTo(writer);
    }

    private static void ValidateJsonArray(
        string value,
        string propertyName) {
        ValidateJson(
            value,
            propertyName,
            static kind => kind == JsonValueKind.Array,
            "a JSON array");
    }

    private static void ValidateJsonObjectOrArray(
        string value,
        string propertyName) {
        ValidateJson(
            value,
            propertyName,
            static kind => kind is JsonValueKind.Object or JsonValueKind.Array,
            "a JSON object or array");
    }

    private static void ValidateJson(
        string value,
        string propertyName,
        Func<JsonValueKind, bool> isValidKind,
        string expectedKindDescription) {
        if (string.IsNullOrWhiteSpace(value)) {
            throw new InvalidOperationException($"{propertyName} must not be empty or whitespace when provided.");
        }

        try {
            using var document = JsonDocument.Parse(value);

            if (!isValidKind(document.RootElement.ValueKind)) {
                throw new InvalidOperationException($"{propertyName} must contain {expectedKindDescription}.");
            }
        }
        catch (JsonException exception) {
            throw new InvalidOperationException($"{propertyName} must contain valid JSON.", exception);
        }
    }
}