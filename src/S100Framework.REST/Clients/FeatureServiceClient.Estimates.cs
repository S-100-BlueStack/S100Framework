using System.Globalization;
using System.Text.Json;
using NetTopologySuite.Geometries;
using S100Framework.REST.Exceptions;
using S100Framework.REST.Internal.Dto;
using S100Framework.REST.Internal.Http;
using S100Framework.REST.Models;

namespace S100Framework.REST.Clients;

/// <summary>
/// Provides service-level and layer-level estimate operations for feature service endpoints.
/// </summary>
public sealed partial class FeatureServiceClient
{
    /// <inheritdoc />
    public Task<IReadOnlyList<FeatureLayerEstimate>> GetEstimatesAsync(
        CancellationToken cancellationToken = default) {
        return GetEstimatesCoreAsync(layerIds: null, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<FeatureLayerEstimate>> GetEstimatesAsync(
        IReadOnlyList<int> layerIds,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(layerIds);
        ValidateEstimateLayerIds(layerIds);

        return GetEstimatesCoreAsync(layerIds, cancellationToken);
    }

    internal async Task<FeatureLayerEstimate> GetLayerEstimatesAsync(
        int layerId,
        CancellationToken cancellationToken = default) {
        if (layerId < 0) {
            throw new ArgumentOutOfRangeException(nameof(layerId), layerId, "Layer ID must not be negative.");
        }

        var endpointUri = UriUtility.WithQuery(
            UriUtility.AppendPath(
                UriUtility.AppendPath(_serviceUri, layerId.ToString(CultureInfo.InvariantCulture)),
                "getEstimates"),
            new Dictionary<string, string?> {
                ["f"] = "json"
            });

        var dto = await GetAsync<EsriLayerEstimateDto>(endpointUri, cancellationToken);

        return new FeatureLayerEstimate(
            layerId,
            dto.Count,
            MapEstimateExtent(dto.Extent));
    }

    private async Task<IReadOnlyList<FeatureLayerEstimate>> GetEstimatesCoreAsync(
        IReadOnlyList<int>? layerIds,
        CancellationToken cancellationToken) {
        var parameters = new Dictionary<string, string?> {
            ["f"] = "json"
        };

        if (layerIds is not null) {
            parameters["layers"] = JsonSerializer.Serialize(layerIds, JsonOptions);
        }

        var endpointUri = UriUtility.WithQuery(
            UriUtility.AppendPath(_serviceUri, "getEstimates"),
            parameters);

        var dto = await GetAsync<EsriServiceEstimatesResponseDto>(endpointUri, cancellationToken);

        return (dto.LayerEstimates ?? Enumerable.Empty<EsriLayerEstimateDto?>())
            .Where(static estimate => estimate is not null)
            .Select(estimate => MapServiceLayerEstimate(estimate!, endpointUri))
            .ToArray();
    }

    private static void ValidateEstimateLayerIds(IReadOnlyList<int> layerIds) {
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

    private FeatureLayerEstimate MapServiceLayerEstimate(
     EsriLayerEstimateDto dto,
     Uri requestUri) {
        if (!dto.LayerId.HasValue) {
            throw new FeatureServiceException(
                "The service returned a layer estimate without a layer ID.",
                requestUri);
        }

        if (dto.LayerId.Value < 0) {
            throw new FeatureServiceException(
                "The service returned a layer estimate with a negative layer ID.",
                requestUri);
        }

        return new FeatureLayerEstimate(
            dto.LayerId.Value,
            dto.Count,
            MapEstimateExtent(dto.Extent));
    }

    private FeatureExtent? MapEstimateExtent(EsriEstimateExtentDto? extent) {
        if (extent is null ||
            !extent.XMin.HasValue ||
            !extent.YMin.HasValue ||
            !extent.XMax.HasValue ||
            !extent.YMax.HasValue) {
            return null;
        }

        var srid = extent.SpatialReference is null
            ? null
            : _options.PreferLatestWkid
                ? extent.SpatialReference.LatestWkid ?? extent.SpatialReference.Wkid
                : extent.SpatialReference.Wkid ?? extent.SpatialReference.LatestWkid;

        return new FeatureExtent(
            new Envelope(
                extent.XMin.Value,
                extent.XMax.Value,
                extent.YMin.Value,
                extent.YMax.Value),
            srid);
    }
}