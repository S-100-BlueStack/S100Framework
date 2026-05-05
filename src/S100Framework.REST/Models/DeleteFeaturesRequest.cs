namespace S100Framework.REST.Models;

/// <summary>
/// Represents a direct layer-level <c>deleteFeatures</c> request.
/// </summary>
public sealed record DeleteFeaturesRequest
{
    /// <summary>
    /// Gets the object IDs to delete.
    /// </summary>
    /// <remarks>
    /// At least one of <see cref="ObjectIds" />, <see cref="Where" />, or <see cref="SpatialFilter" /> must be provided.
    /// </remarks>
    public IReadOnlyList<long>? ObjectIds { get; init; }

    /// <summary>
    /// Gets the SQL WHERE clause used to select features to delete.
    /// </summary>
    /// <remarks>
    /// At least one of <see cref="ObjectIds" />, <see cref="Where" />, or <see cref="SpatialFilter" /> must be provided.
    /// </remarks>
    public string? Where { get; init; }

    /// <summary>
    /// Gets the optional spatial filter used to select features to delete.
    /// </summary>
    /// <remarks>
    /// At least one of <see cref="ObjectIds" />, <see cref="Where" />, or <see cref="SpatialFilter" /> must be provided.
    /// </remarks>
    public FeatureSpatialFilter? SpatialFilter { get; init; }

    /// <summary>
    /// Gets the geodatabase version to edit when the target data supports versioned editing.
    /// </summary>
    public string? GdbVersion { get; init; }

    /// <summary>
    /// Gets a value indicating whether the server should roll back the full request if one delete fails.
    /// </summary>
    public bool RollbackOnFailure { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether the service should return per-feature delete results when supported.
    /// </summary>
    public bool ReturnDeleteResults { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether the service should return the edit moment.
    /// </summary>
    public bool ReturnEditMoment { get; init; }

    /// <summary>
    /// Creates a delete request for object IDs.
    /// </summary>
    /// <param name="objectIds">
    /// The object IDs to delete.
    /// </param>
    /// <returns>
    /// The created delete request.
    /// </returns>
    public static DeleteFeaturesRequest ForObjectIds(IReadOnlyList<long> objectIds) {
        return new DeleteFeaturesRequest {
            ObjectIds = objectIds
        };
    }

    /// <summary>
    /// Creates a delete request for a SQL WHERE clause.
    /// </summary>
    /// <param name="where">
    /// The SQL WHERE clause used to select features to delete.
    /// </param>
    /// <returns>
    /// The created delete request.
    /// </returns>
    public static DeleteFeaturesRequest ForWhere(string where) {
        return new DeleteFeaturesRequest {
            Where = where
        };
    }

    /// <summary>
    /// Creates a delete request for a spatial filter.
    /// </summary>
    /// <param name="spatialFilter">
    /// The spatial filter used to select features to delete.
    /// </param>
    /// <returns>
    /// The created delete request.
    /// </returns>
    public static DeleteFeaturesRequest ForSpatialFilter(FeatureSpatialFilter spatialFilter) {
        return new DeleteFeaturesRequest {
            SpatialFilter = spatialFilter
        };
    }

    /// <summary>
    /// Validates the delete request before it is sent.
    /// </summary>
    public void Validate() {
        var hasObjectIds = ObjectIds is { Count: > 0 };
        var hasWhere = !string.IsNullOrWhiteSpace(Where);
        var hasSpatialFilter = SpatialFilter is not null;

        if (!hasObjectIds && !hasWhere && !hasSpatialFilter) {
            throw new InvalidOperationException(
                "At least one of ObjectIds, Where, or SpatialFilter must be provided.");
        }

        if (ObjectIds is { Count: 0 }) {
            throw new InvalidOperationException("ObjectIds must not be empty when provided.");
        }

        if (ObjectIds?.Any(static objectId => objectId <= 0) == true) {
            throw new InvalidOperationException("ObjectIds must contain only positive values.");
        }

        if (ObjectIds is not null && ObjectIds.Distinct().Count() != ObjectIds.Count) {
            throw new InvalidOperationException("ObjectIds must not contain duplicate values.");
        }

        if (Where is not null && string.IsNullOrWhiteSpace(Where)) {
            throw new InvalidOperationException("Where must not be empty when provided.");
        }

        if (GdbVersion is not null && string.IsNullOrWhiteSpace(GdbVersion)) {
            throw new InvalidOperationException("GdbVersion must not be empty when provided.");
        }
    }
}