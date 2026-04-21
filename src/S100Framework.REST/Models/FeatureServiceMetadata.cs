namespace S100Framework.REST.Models;

/// <summary>
/// Represents metadata for a feature service.
/// </summary>
/// <param name="ServiceUri">The canonical URI of the feature service.</param>
/// <param name="Layers">The layers exposed by the service.</param>
/// <param name="Tables">The tables exposed by the service.</param>
/// <param name="CapabilityText">The raw capability text returned by the server.</param>
/// <param name="MaxRecordCount">The maximum record count reported by the server, when available.</param>
/// <param name="Capabilities">The parsed core service capabilities.</param>
/// <param name="ExtractChangesCapabilities">
/// The parsed <c>extractChanges</c> capabilities, when the service exposes them.
/// </param>
public sealed record FeatureServiceMetadata(
    Uri ServiceUri,
    IReadOnlyList<FeatureServiceDatasetInfo> Layers,
    IReadOnlyList<FeatureServiceDatasetInfo> Tables,
    string? CapabilityText,
    int? MaxRecordCount,
    FeatureServiceCapabilities Capabilities,
    ExtractChangesCapabilities? ExtractChangesCapabilities);

/// <summary>
/// Represents a layer or table entry returned by feature service metadata.
/// </summary>
/// <param name="Id">The dataset ID.</param>
/// <param name="Name">The dataset name.</param>
public sealed record FeatureServiceDatasetInfo(
    int Id,
    string Name);