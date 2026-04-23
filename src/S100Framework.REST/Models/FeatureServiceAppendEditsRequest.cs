using System.Text.Json;

namespace S100Framework.REST.Models;

/// <summary>
/// Represents a service-level append request that uses the literal <c>edits</c> source.
/// </summary>
public sealed record FeatureServiceAppendEditsRequest
{
    /// <summary>
    /// Gets the destination layer IDs to append into.
    /// </summary>
    public IReadOnlyList<int> Layers { get; init; } = Array.Empty<int>();

    /// <summary>
    /// Gets the literal feature collection JSON payload sent through the <c>edits</c> parameter.
    /// </summary>
    public string EditsJson { get; init; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether existing rows should be updated when matching identifiers are found.
    /// </summary>
    public bool Upsert { get; init; }

    /// <summary>
    /// Gets a value indicating whether global IDs should be used for upsert matching.
    /// </summary>
    public bool UseGlobalIds { get; init; }

    /// <summary>
    /// Gets a value indicating whether the server should roll back the append when a failure occurs.
    /// </summary>
    public bool RollbackOnFailure { get; init; }

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
    /// Validates the append request.
    /// </summary>
    public void Validate() {
        if (Layers is null || Layers.Count == 0) {
            throw new InvalidOperationException("Layers must contain at least one destination layer ID.");
        }

        if (Layers.Any(layerId => layerId < 0)) {
            throw new InvalidOperationException("Layers must not contain negative layer IDs.");
        }

        if (Layers.Distinct().Count() != Layers.Count) {
            throw new InvalidOperationException("Layers must not contain duplicate layer IDs.");
        }

        if (string.IsNullOrWhiteSpace(EditsJson)) {
            throw new InvalidOperationException("EditsJson must be provided.");
        }

        try {
            using var document = JsonDocument.Parse(EditsJson);

            if (document.RootElement.ValueKind != JsonValueKind.Object) {
                throw new InvalidOperationException("EditsJson must be a JSON object.");
            }
        }
        catch (JsonException exception) {
            throw new InvalidOperationException("EditsJson must contain valid JSON.", exception);
        }

        if (GdbVersion is not null && string.IsNullOrWhiteSpace(GdbVersion)) {
            throw new InvalidOperationException("GdbVersion must not be empty when provided.");
        }
    }
}