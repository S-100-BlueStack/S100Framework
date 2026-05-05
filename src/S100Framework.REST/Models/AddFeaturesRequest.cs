namespace S100Framework.REST.Models;

/// <summary>
/// Represents a direct layer-level <c>addFeatures</c> request.
/// </summary>
public sealed record AddFeaturesRequest
{
    /// <summary>
    /// Gets the features to add.
    /// </summary>
    public IReadOnlyList<EditableFeature> Features { get; init; } = Array.Empty<EditableFeature>();

    /// <summary>
    /// Gets the geodatabase version to edit when the target data supports versioned editing.
    /// </summary>
    public string? GdbVersion { get; init; }

    /// <summary>
    /// Gets a value indicating whether the server should roll back the full request if one add fails.
    /// </summary>
    public bool RollbackOnFailure { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether the request should use global IDs where supported.
    /// </summary>
    public bool UseGlobalIds { get; init; }

    /// <summary>
    /// Gets a value indicating whether the service should return the edit moment.
    /// </summary>
    public bool ReturnEditMoment { get; init; }

    /// <summary>
    /// Creates an add request for a feature collection.
    /// </summary>
    /// <param name="features">
    /// The features to add.
    /// </param>
    /// <returns>
    /// The created add request.
    /// </returns>
    public static AddFeaturesRequest ForFeatures(IReadOnlyList<EditableFeature> features) {
        return new AddFeaturesRequest {
            Features = features
        };
    }

    /// <summary>
    /// Validates the add request before it is sent.
    /// </summary>
    public void Validate() {
        ValidateFeatures(Features, nameof(Features));

        if (GdbVersion is not null && string.IsNullOrWhiteSpace(GdbVersion)) {
            throw new InvalidOperationException("GdbVersion must not be empty when provided.");
        }
    }

    private static void ValidateFeatures(
     IReadOnlyList<EditableFeature>? features,
     string propertyName) {
        if (features is null) {
            throw new InvalidOperationException($"{propertyName} must be provided.");
        }

        if (features.Count == 0) {
            throw new InvalidOperationException($"{propertyName} must contain at least one feature.");
        }

        foreach (var feature in features) {
            if (feature is null) {
                throw new InvalidOperationException($"{propertyName} must not contain null values.");
            }

            if (feature.Attributes is null) {
                throw new InvalidOperationException($"{propertyName}.Attributes must be provided.");
            }

            if (feature.Attributes.Keys.Any(static key => string.IsNullOrWhiteSpace(key))) {
                throw new InvalidOperationException($"{propertyName}.Attributes must not contain empty attribute names.");
            }
        }
    }
}