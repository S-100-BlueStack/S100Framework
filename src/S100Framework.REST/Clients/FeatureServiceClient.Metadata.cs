using S100Framework.REST.Internal.Dto;
using S100Framework.REST.Internal.Http;
using S100Framework.REST.Models;

namespace S100Framework.REST.Clients;

/// <summary>
/// Provides metadata operations for feature service root endpoints.
/// </summary>
public sealed partial class FeatureServiceClient
{
    /// <inheritdoc />
    public async Task<FeatureServiceMetadata> GetMetadataAsync(CancellationToken cancellationToken = default) {
        var uri = UriUtility.WithQuery(
            _serviceUri,
            new Dictionary<string, string?> {
                ["f"] = "json"
            });

        var dto = await GetAsync<EsriServiceMetadataDto>(uri, cancellationToken);

        var serviceCapabilities = ParseServiceCapabilities(
     dto.Capabilities,
     dto.SyncEnabled,
     dto.SupportsAppend,
     dto.SupportsQueryDomains,
     dto.SupportsQueryDataElements,
     dto.AdvancedEditingCapabilities);

        var extractChangesCapabilities = dto.ExtractChangesCapabilities is null
            ? null
            : new ExtractChangesCapabilities(
                dto.ExtractChangesCapabilities.SupportsReturnIdsOnly ?? false,
                dto.ExtractChangesCapabilities.SupportsReturnExtentOnly ?? false,
                dto.ExtractChangesCapabilities.SupportsReturnAttachments ?? false,
                dto.ExtractChangesCapabilities.SupportsLayerQueries ?? false,
                dto.ExtractChangesCapabilities.SupportsGeometry ?? false,
                dto.ExtractChangesCapabilities.SupportsReturnFeature ?? false,
                dto.ExtractChangesCapabilities.SupportsFieldsToCompare ?? false,
                dto.ExtractChangesCapabilities.SupportsServerGens ?? false,
                dto.ExtractChangesCapabilities.SupportsReturnHasGeometryUpdates ?? false);

        var supportedAppendFormats = dto.SupportedAppendFormats?
            .Where(static format => !string.IsNullOrWhiteSpace(format))
            .ToArray();

        return new FeatureServiceMetadata(
            _serviceUri,
            dto.Layers?.Select(MapDataset).ToArray() ?? Array.Empty<FeatureServiceDatasetInfo>(),
            dto.Tables?.Select(MapDataset).ToArray() ?? Array.Empty<FeatureServiceDatasetInfo>(),
            dto.Capabilities,
            dto.MaxRecordCount,
            serviceCapabilities,
            extractChangesCapabilities,
            supportedAppendFormats);
    }

    private static FeatureServiceCapabilities ParseServiceCapabilities(
     string? capabilities,
     bool? syncEnabled,
     bool? supportsAppend,
     bool? supportsQueryDomains,
     bool? supportsQueryDataElements,
     EsriAdvancedEditingCapabilitiesDto? advancedEditingCapabilities) {
        var values = (capabilities ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new FeatureServiceCapabilities(
            SupportsQuery: values.Contains("Query"),
            SupportsCreate: values.Contains("Create"),
            SupportsUpdate: values.Contains("Update"),
            SupportsDelete: values.Contains("Delete"),
            SupportsEditing: values.Contains("Editing"),
            SupportsUploads: values.Contains("Uploads"),
            SupportsSync: values.Contains("Sync"),
            SupportsChangeTracking: values.Contains("ChangeTracking"),
            SyncEnabled: syncEnabled ?? false,
            SupportsAsyncApplyEdits: advancedEditingCapabilities?.SupportsAsyncApplyEdits ?? false,
            SupportsAppend: supportsAppend ?? false,
            SupportsQueryDomains: supportsQueryDomains ?? false,
            SupportsQueryDataElements: supportsQueryDataElements ?? false);
    }

    private static FeatureServiceDatasetInfo MapDataset(EsriDatasetDto dto) {
        return new FeatureServiceDatasetInfo(dto.Id, dto.Name ?? $"Dataset {dto.Id}");
    }
}