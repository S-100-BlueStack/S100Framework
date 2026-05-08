namespace S100Framework.REST.Models;

/// <summary>
/// Defines a service-level <c>extractChanges</c> request.
/// </summary>
/// <remarks>
/// This model is used with <c>IFeatureServiceClient</c> to request incremental changes
/// for one or more layers in a feature service.
/// </remarks>
public sealed record ExtractChangesRequest
{
    /// <summary>
    /// Gets the layer IDs to include in the request.
    /// </summary>
    /// <remarks>
    /// At least one layer ID must be provided.
    /// </remarks>
    public IReadOnlyList<int> Layers { get; init; } = Array.Empty<int>();

    /// <summary>
    /// Gets the service-level server generation configuration.
    /// </summary>
    /// <remarks>
    /// Exactly one of <see cref="ServerGens" /> or <see cref="LayerServerGens" />
    /// must be provided.
    /// </remarks>
    public ExtractChangesServerGens? ServerGens { get; init; }

    /// <summary>
    /// Gets the layer-level server generation configuration.
    /// </summary>
    /// <remarks>
    /// Exactly one of <see cref="ServerGens" /> or <see cref="LayerServerGens" />
    /// must be provided.
    /// </remarks>
    public IReadOnlyList<ExtractChangesLayerServerGen>? LayerServerGens { get; init; }

    /// <summary>
    /// Gets optional per-layer query filters.
    /// </summary>
    /// <remarks>
    /// Each key must reference a layer that is also included in <see cref="Layers" />.
    /// </remarks>
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
    /// Gets a value indicating whether only object IDs should be returned for changes,
    /// when supported by the service.
    /// </summary>
    public bool ReturnIdsOnly { get; init; }

    /// <summary>
    /// Gets a value indicating whether update results should include whether geometry changed.
    /// </summary>
    /// <remarks>
    /// This option requires <see cref="ReturnUpdates" /> to be <see langword="true" />.
    /// </remarks>
    public bool ReturnHasGeometryUpdates { get; init; }

    /// <summary>
    /// Gets a value indicating whether deleted feature payloads should be returned instead
    /// of only delete IDs.
    /// </summary>
    /// <remarks>
    /// This option requires <see cref="ReturnDeletes" /> to be <see langword="true" />
    /// and <see cref="ReturnIdsOnly" /> to be <see langword="false" />.
    /// </remarks>
    public bool ReturnDeletedFeatures { get; init; }

    /// <summary>
    /// Gets a value indicating whether attachment changes should be returned.
    /// </summary>
    public bool ReturnAttachments { get; init; }

    /// <summary>
    /// Gets a value indicating whether attachment data should be returned by URL instead
    /// of inline content.
    /// </summary>
    /// <remarks>
    /// This option requires <see cref="ReturnAttachments" /> to be <see langword="true" />.
    /// </remarks>
    public bool ReturnAttachmentsDataByUrl { get; init; }

    /// <summary>
    /// Gets a value indicating whether only the extent of changes should be returned.
    /// </summary>
    public bool ReturnExtentOnly { get; init; }

    /// <summary>
    /// Gets the optional grid cell level used when requesting change extents.
    /// </summary>
    /// <remarks>
    /// This option requires <see cref="ReturnExtentOnly" /> to be <see langword="true" />.
    /// </remarks>
    public ExtractChangesExtentGridCell ChangesExtentGridCell { get; init; } = ExtractChangesExtentGridCell.None;

    /// <summary>
    /// Gets the fields to compare when determining update changes.
    /// </summary>
    /// <remarks>
    /// This option requires <see cref="ReturnUpdates" /> to be <see langword="true" />.
    /// </remarks>
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
    /// <exception cref="InvalidOperationException">
    /// Thrown when the request configuration is incomplete or internally inconsistent.
    /// </exception>
    /// <summary>
    /// Validates the request configuration.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the request configuration is incomplete or internally inconsistent.
    /// </exception>
    public void Validate() {
        if (Layers is not { Count: > 0 }) {
            throw new InvalidOperationException("At least one layer ID must be provided.");
        }

        if (Layers.Any(static layerId => layerId < 0)) {
            throw new InvalidOperationException("Layers must not contain negative values.");
        }

        if (Layers.Distinct().Count() != Layers.Count) {
            throw new InvalidOperationException("Layers must not contain duplicate values.");
        }

        var hasServerGens = ServerGens is not null;
        var hasLayerServerGens = LayerServerGens is { Count: > 0 };

        if (hasServerGens == hasLayerServerGens) {
            throw new InvalidOperationException(
                "Exactly one of ServerGens or LayerServerGens must be provided.");
        }

        ServerGens?.Validate();

        if (LayerServerGens is { Count: > 0 }) {
            var layerServerGenIds = new HashSet<int>();

            foreach (var layerServerGen in LayerServerGens) {
                if (layerServerGen is null) {
                    throw new InvalidOperationException("LayerServerGens must not contain null values.");
                }

                layerServerGen.Validate();

                if (!Layers.Contains(layerServerGen.Id)) {
                    throw new InvalidOperationException(
                        $"Layer server generation ID '{layerServerGen.Id}' must also be present in Layers.");
                }

                if (!layerServerGenIds.Add(layerServerGen.Id)) {
                    throw new InvalidOperationException(
                        $"Duplicate layer server generation for layer ID {layerServerGen.Id} is not allowed.");
                }
            }
        }

        if (LayerQueries is { Count: > 0 }) {
            foreach (var pair in LayerQueries) {
                if (!Layers.Contains(pair.Key)) {
                    throw new InvalidOperationException(
                        $"Layer query key '{pair.Key}' must also be present in Layers.");
                }

                if (pair.Value is null) {
                    throw new InvalidOperationException("LayerQueries must not contain null values.");
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

        if (FieldsToCompare is { Count: > 0 }) {
            if (!ReturnUpdates) {
                throw new InvalidOperationException(
                    "FieldsToCompare requires ReturnUpdates to be true.");
            }

            if (FieldsToCompare.Any(static field => string.IsNullOrWhiteSpace(field))) {
                throw new InvalidOperationException(
                    "FieldsToCompare must not contain null, empty, or whitespace-only values.");
            }
        }

        if (ReturnAttachmentsDataByUrl && !ReturnAttachments) {
            throw new InvalidOperationException(
                "ReturnAttachmentsDataByUrl requires ReturnAttachments to be true.");
        }

        if (ChangesExtentGridCell != ExtractChangesExtentGridCell.None && !ReturnExtentOnly) {
            throw new InvalidOperationException(
                "ChangesExtentGridCell requires ReturnExtentOnly to be true.");
        }

        if (!Enum.IsDefined(ChangesExtentGridCell)) {
            throw new InvalidOperationException("ChangesExtentGridCell must be a supported grid cell option.");
        }

        if (!Enum.IsDefined(DataFormat)) {
            throw new InvalidOperationException("DataFormat must be a supported extractChanges data format.");
        }

        if (OutSrid is <= 0) {
            throw new InvalidOperationException("OutSrid must be greater than zero when provided.");
        }

        if (SpatialFilter?.InSrid is <= 0) {
            throw new InvalidOperationException("SpatialFilter.InSrid must be greater than zero when provided.");
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
    /// <exception cref="InvalidOperationException">
    /// Thrown when the configuration does not define either a single starting generation
    /// or a complete min/max range.
    /// </exception>
    public void Validate() {
        var hasSince = SinceServerGen.HasValue;
        var hasRange = MinServerGen.HasValue || MaxServerGen.HasValue;

        if (hasSince == hasRange) {
            throw new InvalidOperationException(
                "Provide either SinceServerGen or both MinServerGen and MaxServerGen.");
        }

        if (SinceServerGen is < 0) {
            throw new InvalidOperationException("SinceServerGen must be greater than or equal to zero when provided.");
        }

        if (hasRange && (!MinServerGen.HasValue || !MaxServerGen.HasValue)) {
            throw new InvalidOperationException(
                "Both MinServerGen and MaxServerGen must be provided together.");
        }

        if (MinServerGen is < 0) {
            throw new InvalidOperationException("MinServerGen must be greater than or equal to zero when provided.");
        }

        if (MaxServerGen is < 0) {
            throw new InvalidOperationException("MaxServerGen must be greater than or equal to zero when provided.");
        }

        if (MinServerGen.HasValue &&
            MaxServerGen.HasValue &&
            MinServerGen.Value > MaxServerGen.Value) {
            throw new InvalidOperationException(
                "MinServerGen must be less than or equal to MaxServerGen.");
        }
    }

    /// <summary>
    /// Converts the configured server generation values to the ArcGIS REST parameter format.
    /// </summary>
    /// <returns>
    /// The serialized parameter value expected by the service.
    /// </returns>
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
/// <param name="Id">
/// The layer ID.
/// </param>
/// <param name="ServerGen">
/// The current server generation for the layer.
/// </param>
/// <param name="MinServerGen">
/// An optional minimum server generation for the layer.
/// </param>
public sealed record ExtractChangesLayerServerGen(
    int Id,
    long ServerGen,
    long? MinServerGen = null)
{
    /// <summary>
    /// Validates the layer server generation configuration.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the layer ID is negative.
    /// </exception>
    public void Validate() {
        if (Id < 0) {
            throw new InvalidOperationException(
                "Layer server generation IDs must be greater than or equal to zero.");
        }

        if (ServerGen < 0) {
            throw new InvalidOperationException(
                "Layer server generation ServerGen must be greater than or equal to zero.");
        }

        if (MinServerGen is < 0) {
            throw new InvalidOperationException(
                "Layer server generation MinServerGen must be greater than or equal to zero when provided.");
        }
    }
}