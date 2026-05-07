using System.Text.Json;
using S100Framework.REST.Exceptions;
using S100Framework.REST.Internal.Dto;
using S100Framework.REST.Internal.Http;
using S100Framework.REST.Models;

namespace S100Framework.REST.Clients;

/// <summary>
/// Provides <c>queryDataElements</c> operations for feature service endpoints.
/// </summary>
public sealed partial class FeatureServiceClient
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<FeatureLayerDataElement>> QueryDataElementsAsync(
        IReadOnlyList<int> layerIds,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(layerIds);

        ValidateQueryDataElementsLayerIds(layerIds);

        var metadata = await GetMetadataAsync(cancellationToken);

        if (!metadata.Capabilities.SupportsQueryDataElements) {
            throw new FeatureServiceCapabilityException(
                "The feature service does not advertise queryDataElements support.");
        }

        var uri = UriUtility.WithQuery(
            UriUtility.AppendPath(_serviceUri, "queryDataElements"),
            new Dictionary<string, string?> {
                ["f"] = "json",
                ["layers"] = JsonSerializer.Serialize(layerIds, JsonOptions)
            });

        var dto = await GetAsync<EsriQueryDataElementsResponseDto>(uri, cancellationToken);

        return (dto.LayerDataElements ?? Enumerable.Empty<EsriLayerDataElementDto?>())
            .Where(static layerDataElement => layerDataElement is not null)
            .Select(layerDataElement => MapFeatureLayerDataElement(layerDataElement!, uri))
            .ToArray();
    }

    private static void ValidateQueryDataElementsLayerIds(IReadOnlyList<int> layerIds) {
        if (layerIds.Count == 0) {
            throw new InvalidOperationException("At least one layer ID must be provided.");
        }

        if (layerIds.Any(static layerId => layerId < 0)) {
            throw new InvalidOperationException("Layer IDs must not contain negative values.");
        }

        if (layerIds.Distinct().Count() != layerIds.Count) {
            throw new InvalidOperationException("Layer IDs must not contain duplicate values.");
        }
    }

    private static FeatureLayerDataElement MapFeatureLayerDataElement(
     EsriLayerDataElementDto dto,
     Uri requestUri) {
        if (!dto.LayerId.HasValue) {
            throw new FeatureServiceException(
                "The service returned a data element without a layer ID.",
                requestUri);
        }

        if (dto.LayerId.Value < 0) {
            throw new FeatureServiceException(
                "The service returned a data element with a negative layer ID.",
                requestUri);
        }

        return new FeatureLayerDataElement(
            dto.LayerId.Value,
            dto.DataElement?.Clone() ?? default);
    }
}