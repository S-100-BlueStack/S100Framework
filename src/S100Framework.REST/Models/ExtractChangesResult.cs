namespace S100Framework.REST.Models;

public sealed record ExtractChangesResult(
    IReadOnlyList<ExtractChangesLayerServerGen> LayerServerGens,
    IReadOnlyList<ExtractChangesLayerEdits> Edits,
    string? TransportType,
    string? ResponseType,
    FeatureExtent? Extent);

public sealed record ExtractChangesLayerEdits(
    int LayerId,
    ExtractChangesIdChanges? ObjectIds,
    ExtractChangesFeatureChanges? Features,
    ExtractChangesAttachmentChanges? Attachments,
    IReadOnlyList<object?> FieldUpdates,
    bool? HasGeometryUpdates);

public sealed record ExtractChangesIdChanges(
    IReadOnlyList<object?> Adds,
    IReadOnlyList<object?> Updates,
    IReadOnlyList<object?> Deletes);

public sealed record ExtractChangesFeatureChanges(
    IReadOnlyList<FeatureRecord> Adds,
    IReadOnlyList<FeatureRecord> Updates,
    IReadOnlyList<FeatureRecord> Deletes,
    IReadOnlyList<object?> DeleteIds);

public sealed record ExtractChangesAttachmentChanges(
    IReadOnlyList<object?> Adds,
    IReadOnlyList<object?> Updates,
    IReadOnlyList<object?> Deletes,
    IReadOnlyList<object?> DeleteIds);