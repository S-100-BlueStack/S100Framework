using System.Text.Json;

namespace S100Framework.REST.Models;

/// <summary>
/// Represents common options for layer-level append requests.
/// </summary>
public abstract record FeatureLayerAppendRequestBase
{
    /// <summary>
    /// Gets the source table name when the append source contains named tables.
    /// </summary>
    public string? SourceTableName { get; init; }

    /// <summary>
    /// Gets optional field mappings from source fields to destination fields.
    /// </summary>
    public IReadOnlyList<FeatureServiceAppendFieldMapping>? FieldMappings { get; init; }

    /// <summary>
    /// Gets raw JSON describing the append source information.
    /// </summary>
    public string? AppendSourceInfoJson { get; init; }

    /// <summary>
    /// Gets raw JSON describing source filters for append.
    /// </summary>
    public string? AppendSourceFilterJson { get; init; }

    /// <summary>
    /// Gets a value indicating whether existing rows should be updated when matching identifiers are found.
    /// </summary>
    public bool Upsert { get; init; }

    /// <summary>
    /// Gets a value indicating whether insert operations should be skipped during upsert append.
    /// </summary>
    public bool SkipInserts { get; init; }

    /// <summary>
    /// Gets a value indicating whether update operations should be skipped during upsert append.
    /// </summary>
    public bool SkipUpdates { get; init; }

    /// <summary>
    /// Gets a value indicating whether global IDs should be used for append matching.
    /// </summary>
    public bool UseGlobalIds { get; init; }

    /// <summary>
    /// Gets a value indicating whether existing rows should be truncated before append.
    /// </summary>
    public bool TruncateExisting { get; init; }

    /// <summary>
    /// Gets a value indicating whether geometry should be updated for matching features.
    /// </summary>
    public bool? UpdateGeometry { get; init; }

    /// <summary>
    /// Gets the destination fields included in the append operation.
    /// </summary>
    public IReadOnlyList<string>? AppendFields { get; init; }

    /// <summary>
    /// Gets the destination field used to match source rows during upsert.
    /// </summary>
    public string? UpsertMatchingField { get; init; }

    /// <summary>
    /// Gets the target geodatabase version for reference feature services.
    /// </summary>
    public string? GdbVersion { get; init; }

    /// <summary>
    /// Gets a value indicating whether the response should include the edit moment when supported.
    /// </summary>
    public bool ReturnEditMoment { get; init; }

    /// <summary>
    /// Gets a value indicating whether the client is true-curve capable.
    /// </summary>
    public bool TrueCurveClient { get; init; }

    /// <summary>
    /// Gets a value indicating whether the client can work with unknown time references.
    /// </summary>
    public bool TimeReferenceUnknownClient { get; init; }

    /// <summary>
    /// Gets raw JSON describing append attachment behavior.
    /// </summary>
    public string? AppendAttachmentsInfoJson { get; init; }

    /// <summary>
    /// Gets optional source-to-destination layer mappings.
    /// </summary>
    public IReadOnlyList<FeatureServiceAppendLayerMapping>? LayerMappings { get; init; }

    /// <summary>
    /// Gets a value indicating whether the server should roll back the append when a failure occurs.
    /// </summary>
    public bool RollbackOnFailure { get; init; }

    /// <summary>
    /// Validates the common layer-level append options.
    /// </summary>
    protected void ValidateCommon() {
        if (SourceTableName is not null && string.IsNullOrWhiteSpace(SourceTableName)) {
            throw new InvalidOperationException("SourceTableName must not be empty when provided.");
        }

        ValidateJsonObject(AppendSourceInfoJson, nameof(AppendSourceInfoJson));
        ValidateJson(AppendSourceFilterJson, nameof(AppendSourceFilterJson));
        ValidateJsonObject(AppendAttachmentsInfoJson, nameof(AppendAttachmentsInfoJson));

        if (SkipInserts && !Upsert) {
            throw new InvalidOperationException("SkipInserts can only be used when Upsert is true.");
        }

        if (SkipUpdates && !Upsert) {
            throw new InvalidOperationException("SkipUpdates can only be used when Upsert is true.");
        }

        if (UpdateGeometry.HasValue && !Upsert) {
            throw new InvalidOperationException("UpdateGeometry can only be used when Upsert is true.");
        }

        if (AppendFields is { Count: 0 }) {
            throw new InvalidOperationException("AppendFields must not be empty when provided.");
        }

        if (AppendFields is not null && AppendFields.Any(string.IsNullOrWhiteSpace)) {
            throw new InvalidOperationException("AppendFields must not contain empty field names.");
        }

        if (UpsertMatchingField is not null && string.IsNullOrWhiteSpace(UpsertMatchingField)) {
            throw new InvalidOperationException("UpsertMatchingField must not be empty when provided.");
        }

        if (GdbVersion is not null && string.IsNullOrWhiteSpace(GdbVersion)) {
            throw new InvalidOperationException("GdbVersion must not be empty when provided.");
        }

        ValidateFieldMappings(FieldMappings);
        ValidateLayerMappings(LayerMappings);
    }

    private static void ValidateFieldMappings(IReadOnlyList<FeatureServiceAppendFieldMapping>? fieldMappings) {
        if (fieldMappings is null) {
            return;
        }

        if (fieldMappings.Count == 0) {
            throw new InvalidOperationException("FieldMappings must not be empty when provided.");
        }

        foreach (var fieldMapping in fieldMappings) {
            ArgumentNullException.ThrowIfNull(fieldMapping);

            if (string.IsNullOrWhiteSpace(fieldMapping.Name)) {
                throw new InvalidOperationException("FieldMappings.Name must not be empty.");
            }

            if (string.IsNullOrWhiteSpace(fieldMapping.Source)) {
                throw new InvalidOperationException("FieldMappings.Source must not be empty.");
            }
        }
    }

    private static void ValidateLayerMappings(IReadOnlyList<FeatureServiceAppendLayerMapping>? layerMappings) {
        if (layerMappings is null) {
            return;
        }

        if (layerMappings.Count == 0) {
            throw new InvalidOperationException("LayerMappings must not be empty when provided.");
        }

        foreach (var mapping in layerMappings) {
            ArgumentNullException.ThrowIfNull(mapping);

            if (mapping.Id < 0) {
                throw new InvalidOperationException("LayerMappings.Id must not be negative.");
            }

            if (mapping.SourceId is null && string.IsNullOrWhiteSpace(mapping.SourceTableName)) {
                throw new InvalidOperationException(
                    "Each LayerMappings entry must specify either SourceId or SourceTableName.");
            }

            if (mapping.SourceId is < 0) {
                throw new InvalidOperationException("LayerMappings.SourceId must not be negative when provided.");
            }

            if (mapping.SourceTableName is not null && string.IsNullOrWhiteSpace(mapping.SourceTableName)) {
                throw new InvalidOperationException("LayerMappings.SourceTableName must not be empty when provided.");
            }

            ValidateFieldMappings(mapping.FieldMappings);
        }
    }

    private static void ValidateJsonObject(string? json, string propertyName) {
        if (json is null) {
            return;
        }

        using var document = ParseJson(json, propertyName);

        if (document.RootElement.ValueKind != JsonValueKind.Object) {
            throw new InvalidOperationException($"{propertyName} must be a JSON object.");
        }
    }

    private static void ValidateJson(string? json, string propertyName) {
        if (json is null) {
            return;
        }

        using var document = ParseJson(json, propertyName);
    }

    private static JsonDocument ParseJson(string json, string propertyName) {
        if (string.IsNullOrWhiteSpace(json)) {
            throw new InvalidOperationException($"{propertyName} must not be empty when provided.");
        }

        try {
            return JsonDocument.Parse(json);
        }
        catch (JsonException exception) {
            throw new InvalidOperationException($"{propertyName} must contain valid JSON.", exception);
        }
    }
}