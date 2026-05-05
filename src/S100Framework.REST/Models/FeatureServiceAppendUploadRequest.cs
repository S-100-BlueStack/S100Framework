namespace S100Framework.REST.Models;

/// <summary>
/// Represents a service-level append request that uses a server upload item as the source.
/// </summary>
public sealed record FeatureServiceAppendUploadRequest
{
    /// <summary>
    /// Gets the destination layer IDs to append into.
    /// </summary>
    public IReadOnlyList<int> Layers { get; init; } = Array.Empty<int>();

    /// <summary>
    /// Gets the upload item ID returned from the uploads operation.
    /// </summary>
    public string AppendUploadId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the uploaded source format.
    /// </summary>
    public FeatureServiceAppendSourceFormat? AppendUploadFormat { get; init; }

    /// <summary>
    /// Gets optional source-to-destination layer mappings.
    /// </summary>
    public IReadOnlyList<FeatureServiceAppendLayerMapping>? LayerMappings { get; init; }

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

        if (string.IsNullOrWhiteSpace(AppendUploadId)) {
            throw new InvalidOperationException("AppendUploadId must be provided.");
        }

        if (!AppendUploadFormat.HasValue) {
            throw new InvalidOperationException("AppendUploadFormat must be provided.");
        }

        if (!Enum.IsDefined(AppendUploadFormat.Value)) {
            throw new InvalidOperationException("AppendUploadFormat must be a supported append source format.");
        }

        if (GdbVersion is not null && string.IsNullOrWhiteSpace(GdbVersion)) {
            throw new InvalidOperationException("GdbVersion must not be empty when provided.");
        }

        if (LayerMappings is null) {
            return;
        }

        if (LayerMappings.Count == 0) {
            throw new InvalidOperationException("LayerMappings must not be empty when provided.");
        }

        var destinationIds = new HashSet<int>(Layers);

        foreach (var mapping in LayerMappings) {
            ArgumentNullException.ThrowIfNull(mapping);

            if (!destinationIds.Contains(mapping.Id)) {
                throw new InvalidOperationException(
                    $"LayerMappings contains destination layer ID {mapping.Id}, which is not present in Layers.");
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

            if (mapping.FieldMappings is null) {
                continue;
            }

            foreach (var fieldMapping in mapping.FieldMappings) {
                ArgumentNullException.ThrowIfNull(fieldMapping);

                if (string.IsNullOrWhiteSpace(fieldMapping.Name)) {
                    throw new InvalidOperationException("LayerMappings.FieldMappings.Name must not be empty.");
                }

                if (string.IsNullOrWhiteSpace(fieldMapping.Source)) {
                    throw new InvalidOperationException("LayerMappings.FieldMappings.Source must not be empty.");
                }
            }
        }

        if (LayerMappings
            .GroupBy(static mapping => mapping.Id)
            .Any(static group => group.Count() > 1)) {
            throw new InvalidOperationException("LayerMappings must not contain duplicate destination layer IDs.");
        }
    }
}