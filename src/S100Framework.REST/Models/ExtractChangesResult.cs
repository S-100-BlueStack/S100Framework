namespace S100Framework.REST.Models;

/// <summary>
/// Represents the final result of a completed <c>extractChanges</c> operation.
/// </summary>
/// <param name="LayerServerGens">The returned server generations per layer.</param>
/// <param name="Edits">The returned changes per layer.</param>
/// <param name="TransportType">The reported transport type, when available.</param>
/// <param name="ResponseType">The reported response type, when available.</param>
/// <param name="Extent">The returned extent when extent-only mode is used.</param>
public sealed record ExtractChangesResult(
    IReadOnlyList<ExtractChangesLayerServerGen> LayerServerGens,
    IReadOnlyList<ExtractChangesLayerEdits> Edits,
    string? TransportType,
    string? ResponseType,
    FeatureExtent? Extent);

/// <summary>
/// Represents the changes returned for a single layer.
/// </summary>
/// <param name="LayerId">The layer ID.</param>
/// <param name="ObjectIds">The changed object IDs, when ID-only mode is used.</param>
/// <param name="Features">The changed feature payloads, when feature results are returned.</param>
/// <param name="Attachments">The changed attachments, when attachment results are returned.</param>
/// <param name="FieldUpdates">The fields reported as updated.</param>
/// <param name="HasGeometryUpdates">Whether geometry updates were reported for the layer.</param>
public sealed record ExtractChangesLayerEdits(
    int LayerId,
    ExtractChangesIdChanges? ObjectIds,
    ExtractChangesFeatureChanges? Features,
    ExtractChangesAttachmentChanges? Attachments,
    IReadOnlyList<object?> FieldUpdates,
    bool? HasGeometryUpdates);

/// <summary>
/// Represents changed object IDs for a layer.
/// </summary>
/// <param name="Adds">The added object IDs.</param>
/// <param name="Updates">The updated object IDs.</param>
/// <param name="Deletes">The deleted object IDs.</param>
public sealed record ExtractChangesIdChanges(
    IReadOnlyList<object?> Adds,
    IReadOnlyList<object?> Updates,
    IReadOnlyList<object?> Deletes);

/// <summary>
/// Represents changed features for a layer.
/// </summary>
/// <param name="Adds">The added features.</param>
/// <param name="Updates">The updated features.</param>
/// <param name="Deletes">The deleted features, when returned as features.</param>
/// <param name="DeleteIds">The deleted feature IDs, when returned separately.</param>
public sealed record ExtractChangesFeatureChanges(
    IReadOnlyList<FeatureRecord> Adds,
    IReadOnlyList<FeatureRecord> Updates,
    IReadOnlyList<FeatureRecord> Deletes,
    IReadOnlyList<object?> DeleteIds);

/// <summary>
/// Represents changed attachments for a layer.
/// </summary>
/// <param name="Adds">The added attachments.</param>
/// <param name="Updates">The updated attachments.</param>
/// <param name="Deletes">The deleted attachments, when returned as attachment payloads.</param>
/// <param name="DeleteIds">The deleted attachment IDs, when returned separately.</param>
public sealed record ExtractChangesAttachmentChanges(
    IReadOnlyList<object?> Adds,
    IReadOnlyList<object?> Updates,
    IReadOnlyList<object?> Deletes,
    IReadOnlyList<object?> DeleteIds);