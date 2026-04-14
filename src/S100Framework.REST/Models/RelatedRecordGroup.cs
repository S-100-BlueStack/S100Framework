namespace S100Framework.REST.Models;

public sealed record RelatedRecordGroup(
    long SourceObjectId,
    IReadOnlyList<FeatureRecord> Records);