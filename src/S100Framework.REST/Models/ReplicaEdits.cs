using System.Text;
using System.Text.Json;

namespace S100Framework.REST.Models;

/// <summary>
/// Represents a schema-agnostic ArcGIS replica edit payload.
/// </summary>
/// <remarks>
/// The generated JSON is intended for <see cref="SynchronizeReplicaRequest.EditsJson" /> and keeps individual
/// feature edit payloads raw so consumers can preserve their own geometry, attributes, global IDs, and attachment shape.
/// </remarks>
public sealed record ReplicaEdits
{
    /// <summary>
    /// Gets the layer and table edit entries.
    /// </summary>
    public IReadOnlyList<ReplicaLayerEdits> Layers { get; init; } = Array.Empty<ReplicaLayerEdits>();

    /// <summary>
    /// Validates the edit payload.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the edit payload is incomplete or invalid.
    /// </exception>
    public void Validate() {
        if (Layers is not { Count: > 0 }) {
            throw new InvalidOperationException("Replica edits must contain at least one layer edit entry.");
        }

        var layerIds = new HashSet<int>();

        foreach (var layer in Layers) {
            if (layer is null) {
                throw new InvalidOperationException("Replica edits must not contain null layer entries.");
            }

            layer.Validate();

            if (!layerIds.Add(layer.Id)) {
                throw new InvalidOperationException("Replica edits must not contain duplicate layer IDs.");
            }
        }
    }

    /// <summary>
    /// Converts the edit payload to JSON for <see cref="SynchronizeReplicaRequest.EditsJson" />.
    /// </summary>
    /// <param name="options">
    /// Optional JSON output options.
    /// </param>
    /// <returns>
    /// The serialized ArcGIS edits payload.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the edit payload is incomplete or invalid.
    /// </exception>
    public string ToJson(ReplicaEditsJsonOptions? options = null) {
        Validate();

        using var stream = new MemoryStream();

        using (var writer = new Utf8JsonWriter(
                   stream,
                   new JsonWriterOptions {
                       Indented = options?.WriteIndented ?? false
                   })) {
            writer.WriteStartArray();

            foreach (var layer in Layers) {
                layer.WriteTo(writer);
            }

            writer.WriteEndArray();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}