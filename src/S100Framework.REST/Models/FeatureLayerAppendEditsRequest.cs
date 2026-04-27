using System.Text.Json;

namespace S100Framework.REST.Models;

/// <summary>
/// Represents a layer-level append request that uses the literal <c>edits</c> source.
/// </summary>
public sealed record FeatureLayerAppendEditsRequest : FeatureLayerAppendRequestBase
{
    /// <summary>
    /// Gets the literal feature collection JSON payload sent through the <c>edits</c> parameter.
    /// </summary>
    public string EditsJson { get; init; } = string.Empty;

    /// <summary>
    /// Validates the append request.
    /// </summary>
    public void Validate() {
        ValidateCommon();

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
    }
}