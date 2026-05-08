using S100Framework.REST.Exceptions;
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
            dto.SupportsQueryContingentValues,
            dto.SupportedContingentValuesFormats,
            dto.SupportsContingentValuesJson,
            dto.SupportsRelationshipsResource,
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

        var syncCapabilities = dto.SyncCapabilities is null
            ? null
            : new FeatureServiceSyncCapabilities(
                dto.SyncCapabilities.SupportsRegisteringExistingData ?? false,
                dto.SyncCapabilities.SupportsSyncDirectionControl ?? false,
                dto.SyncCapabilities.SupportsPerLayerSync ?? false,
                dto.SyncCapabilities.SupportsPerReplicaSync ?? false,
                dto.SyncCapabilities.SupportsSyncModelNone ?? false,
                dto.SyncCapabilities.SupportsRollbackOnFailure ?? false,
                dto.SyncCapabilities.SupportsAsync ?? false,
                dto.SyncCapabilities.SupportsAttachmentsSyncDirection ?? false,
                dto.SyncCapabilities.SupportsBiDirectionalSyncForServer ?? false);

        var supportedAppendFormats = dto.SupportedAppendFormats?
            .Where(static format => !string.IsNullOrWhiteSpace(format))
            .ToArray();

        var supportedExportFormats = dto.SupportedExportFormats?
            .Where(static format => !string.IsNullOrWhiteSpace(format))
            .ToArray();

        return new FeatureServiceMetadata(
            _serviceUri,
            dto.Layers?
                .Where(static dataset => dataset is not null)
                .Select(dataset => MapDataset(dataset!, uri, "layers"))
                .ToArray() ?? Array.Empty<FeatureServiceDatasetInfo>(),
            dto.Tables?
                .Where(static dataset => dataset is not null)
                .Select(dataset => MapDataset(dataset!, uri, "tables"))
                .ToArray() ?? Array.Empty<FeatureServiceDatasetInfo>(),
            dto.Capabilities,
            dto.MaxRecordCount,
            serviceCapabilities,
            extractChangesCapabilities,
            supportedAppendFormats,
            supportedExportFormats,
            syncCapabilities);
    }

    private static FeatureServiceCapabilities ParseServiceCapabilities(
        string? capabilities,
        bool? syncEnabled,
        bool? supportsAppend,
        bool? supportsQueryDomains,
        bool? supportsQueryDataElements,
        bool? supportsQueryContingentValues,
        string? supportedContingentValuesFormats,
        int? supportsContingentValuesJson,
        bool? supportsRelationshipsResource,
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
            SupportsQueryDataElements: supportsQueryDataElements ?? false,
            SupportsQueryContingentValues: supportsQueryContingentValues ?? false,
            SupportsRelationshipsResource: supportsRelationshipsResource ?? false) {
            SupportedContingentValuesFormats = ParseDelimitedValues(supportedContingentValuesFormats),
            ContingentValuesJsonVersion = supportsContingentValuesJson is > 0
                ? supportsContingentValuesJson
                : null
        };
    }

    private static IReadOnlyList<string> ParseDelimitedValues(string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return Array.Empty<string>();
        }

        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static item => item.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static FeatureServiceDatasetInfo MapDataset(
        EsriDatasetDto dto,
        Uri requestUri,
        string collectionName) {
        if (!dto.Id.HasValue) {
            throw new FeatureServiceException(
                $"The service metadata returned a {collectionName} item without an ID.",
                requestUri);
        }

        if (dto.Id.Value < 0) {
            throw new FeatureServiceException(
                $"The service metadata returned a {collectionName} item with a negative ID.",
                requestUri);
        }

        return new FeatureServiceDatasetInfo(
            dto.Id.Value,
            dto.Name ?? $"Dataset {dto.Id.Value}");
    }
}
