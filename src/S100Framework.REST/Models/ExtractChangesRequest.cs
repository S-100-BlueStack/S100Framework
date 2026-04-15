namespace S100Framework.REST.Models;

public sealed record ExtractChangesRequest
{
    public IReadOnlyList<int> Layers { get; init; } = Array.Empty<int>();

    public ExtractChangesServerGens? ServerGens { get; init; }

    public IReadOnlyList<ExtractChangesLayerServerGen>? LayerServerGens { get; init; }

    public IReadOnlyDictionary<int, ExtractChangesLayerQuery>? LayerQueries { get; init; }

    public ExtractChangesSpatialFilter? SpatialFilter { get; init; }

    public bool ReturnInserts { get; init; } = true;

    public bool ReturnUpdates { get; init; } = true;

    public bool ReturnDeletes { get; init; } = true;

    public bool ReturnIdsOnly { get; init; }

    public bool ReturnHasGeometryUpdates { get; init; }

    public bool ReturnDeletedFeatures { get; init; }

    public bool ReturnAttachments { get; init; }

    public bool ReturnAttachmentsDataByUrl { get; init; }

    public bool ReturnExtentOnly { get; init; }

    public ExtractChangesExtentGridCell ChangesExtentGridCell { get; init; } =
        ExtractChangesExtentGridCell.None;

    public IReadOnlyList<string>? FieldsToCompare { get; init; }

    public int? OutSrid { get; init; }

    public ExtractChangesDataFormat DataFormat { get; init; } =
        ExtractChangesDataFormat.Json;

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

public sealed record ExtractChangesServerGens
{
    public long? SinceServerGen { get; init; }

    public long? MinServerGen { get; init; }

    public long? MaxServerGen { get; init; }

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

        if (MinServerGen.HasValue && MaxServerGen.HasValue && MinServerGen.Value > MaxServerGen.Value) {
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

public sealed record ExtractChangesLayerServerGen(
    int Id,
    long ServerGen,
    long? MinServerGen = null)
{
    public void Validate() {
        if (Id < 0) {
            throw new InvalidOperationException("Layer server generation IDs must be greater than or equal to zero.");
        }
    }
}