namespace S100Framework.REST.Models;

/// <summary>
/// Defines a service-level <c>extractChanges</c> request.
/// </summary>
public sealed record ExtractChangesRequest
{
    /// <summary>
    /// Gets the layer IDs to include in the request.
    /// </summary>
    public IReadOnlyList<int> Layers { get; init; } = Array.Empty<int>();

    /// <summary>
    /// Gets the service-level server generation configuration.
    /// Exactly one of <see cref="ServerGens"/> or <see cref="LayerServerGens"/> must be provided.
    /// </summary>
    public ExtractChangesServerGens? ServerGens { get; init; }

    /// <summary>
    /// Gets the layer-level server generation configuration.
    /// Exactly one of <see cref="LayerServerGens"/> or <see cref="ServerGens"/> must be provided.
    /// </summary>
    public IReadOnlyList<ExtractChangesLayerServerGen>? LayerServerGens { get; init; }

    /// <summary>
    /// Gets optional per-layer query filters.
    /// </summary>
    public IReadOnlyDictionary<int, ExtractChangesLayerQuery>? LayerQueries { get; init; }

    /// <summary>
    /// Gets an optional spatial filter.
    /// </summary>
    public ExtractChangesSpatialFilter? SpatialFilter { get; init; }

    /// <summary>
    /// Gets a value indicating whether inserted features should be returned.
    /// </summary>
    public bool ReturnInserts { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether updated features should be returned.
    /// </summary>
    public bool ReturnUpdates { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether deleted features should be returned.
    /// </summary>
    public bool ReturnDeletes { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether only IDs should be returned for changes, when supported.
    /// </summary>
    public bool ReturnIdsOnly { get; init; }

    /// <summary>
    /// Gets a value indicating whether update results should include whether geometry changed.
    /// </summary>
    public bool ReturnHasGeometryUpdates { get; init; }

    /// <summary>
    /// Gets a value indicating whether deleted feature payloads should be returned instead of only delete IDs.
    /// </summary>
    public bool ReturnDeletedFeatures { get; init; }

    /// <summary>
    /// Gets a value indicating whether attachment changes should be returned.
    /// </summary>
    public bool ReturnAttachments { get; init; }

    /// <summary>
    /// Gets a value indicating whether attachment data should be returned by URL instead of inline content.
    /// </summary>
    public bool ReturnAttachmentsDataByUrl { get; init; }

    /// <summary>
    /// Gets a value indicating whether only the extent of changes should be returned.
    /// </summary>
    public bool ReturnExtentOnly { get; init; }

    /// <summary>
    /// Gets the optional grid-cell level used when requesting change extents.
    /// </summary>
    public ExtractChangesExtentGridCell ChangesExtentGridCell { get; init; } = ExtractChangesExtentGridCell.None;

    /// <summary>
    /// Gets the fields to compare when determining update changes.
    /// </summary>
    public IReadOnlyList<string>? FieldsToCompare { get; init; }

    /// <summary>
    /// Gets the output spatial reference ID for returned geometries, when specified.
    /// </summary>
    public int? OutSrid { get; init; }

    /// <summary>
    /// Gets the requested response format.
    /// </summary>
    public ExtractChangesDataFormat DataFormat { get; init; } = ExtractChangesDataFormat.Json;

    /// <summary>
    /// Validates the request configuration.
    /// </summary>
    public void Validate() {
        if (Layers.Count == 0) {
            throw new InvalidOperationException("At least one layer ID must be provided.");
        }

        var hasServerGens = ServerGens is not null;
        var hasLayerServerGens = LayerServerGens is { Count: > 0 };

        if (hasServerGens == hasLayerServerGens) {
            throw new InvalidOperationException(
                "Exactly one of ServerGens or LayerServerGens must be provided.");
        }

        ServerGens?.Validate();

        if (LayerServerGens is { Count: > 0 }) {
            foreach (var layerServerGen in LayerServerGens) {
                layerServerGen.Validate();
            }
        }

        if (LayerQueries is { Count: > 0 }) {
            foreach (var pair in LayerQueries) {
                if (!Layers.Contains(pair.Key)) {
                    throw new InvalidOperationException(
                        $"Layer query key '{pair.Key}' must also be present in Layers.");
                }

                pair.Value.Validate();
            }
        }

        if (ReturnHasGeometryUpdates && !ReturnUpdates) {
            throw new InvalidOperationException(
                "ReturnHasGeometryUpdates requires ReturnUpdates to be true.");
        }

        if (ReturnDeletedFeatures) {
            if (!ReturnDeletes) {
                throw new InvalidOperationException(
                    "ReturnDeletedFeatures requires ReturnDeletes to be true.");
            }

            if (ReturnIdsOnly) {
                throw new InvalidOperationException(
                    "ReturnDeletedFeatures requires ReturnIdsOnly to be false.");
            }
        }

        if (FieldsToCompare is { Count: > 0 } && !ReturnUpdates) {
            throw new InvalidOperationException(
                "FieldsToCompare requires ReturnUpdates to be true.");
        }

        if (ReturnAttachmentsDataByUrl && !ReturnAttachments) {
            throw new InvalidOperationException(
                "ReturnAttachmentsDataByUrl requires ReturnAttachments to be true.");
        }

        if (ChangesExtentGridCell != ExtractChangesExtentGridCell.None && !ReturnExtentOnly) {
            throw new InvalidOperationException(
                "ChangesExtentGridCell requires ReturnExtentOnly to be true.");
        }
    }
}

/// <summary>
/// Defines service-level server generation values for <c>extractChanges</c>.
/// </summary>
public sealed record ExtractChangesServerGens
{
    /// <summary>
    /// Gets the starting server generation for incremental extraction.
    /// </summary>
    public long? SinceServerGen { get; init; }

    /// <summary>
    /// Gets the minimum server generation for range-based extraction.
    /// </summary>
    public long? MinServerGen { get; init; }

    /// <summary>
    /// Gets the maximum server generation for range-based extraction.
    /// </summary>
    public long? MaxServerGen { get; init; }

    /// <summary>
    /// Validates the server generation configuration.
    /// </summary>
    public void Validate() {
        var hasSince = SinceServerGen.HasValue;
        var hasRange = MinServerGen.HasValue || MaxServerGen.HasValue;

        if (hasSince == hasRange) {
            throw new InvalidOperationException(
                "Provide either SinceServerGen or both MinServerGen and MaxServerGen.");
        }

        if (hasRange && (!MinServerGen.HasValue || !MaxServerGen.HasValue)) {
            throw new InvalidOperationException(
                "Both MinServerGen and MaxServerGen must be provided together.");
        }

        if (MinServerGen.HasValue &&
            MaxServerGen.HasValue &&
            MinServerGen.Value > MaxServerGen.Value) {
            throw new InvalidOperationException(
                "MinServerGen must be less than or equal to MaxServerGen.");
        }
    }

    internal string ToParameterValue() {
        if (SinceServerGen.HasValue) {
            return SinceServerGen.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return $"[{MinServerGen!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)},{MaxServerGen!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}]";
    }
}

/// <summary>
/// Defines a server generation value for a specific layer in <c>extractChanges</c>.
/// </summary>
/// <param name="Id">The layer ID.</param>
/// <param name="ServerGen">The current server generation for the layer.</param>
/// <param name="MinServerGen">An optional minimum server generation for the layer.</param>
public sealed record ExtractChangesLayerServerGen(
    int Id,
    long ServerGen,
    long? MinServerGen = null)
{
    /// <summary>
    /// Validates the layer server generation configuration.
    /// </summary>
    public void Validate() {
        if (Id < 0) {
            throw new InvalidOperationException(
                "Layer server generation IDs must be greater than or equal to zero.");
        }
    }
}