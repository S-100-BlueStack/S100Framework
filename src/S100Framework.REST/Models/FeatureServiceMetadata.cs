namespace S100Framework.REST.Models;

public sealed record FeatureServiceMetadata(
    Uri ServiceUri,
    IReadOnlyList<FeatureServiceDatasetInfo> Layers,
    IReadOnlyList<FeatureServiceDatasetInfo> Tables,
    string? Capabilities,
    int? MaxRecordCount,
    FeatureServiceCapabilities CapabilityInfo,
    ExtractChangesCapabilities? ExtractChangesCapabilities);

public sealed record FeatureServiceDatasetInfo(
    int Id,
    string Name);