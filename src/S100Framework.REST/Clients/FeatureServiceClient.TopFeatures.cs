using System.Globalization;
using System.Text.Json;
using NetTopologySuite.Geometries;
using S100Framework.REST.Exceptions;
using S100Framework.REST.Internal.Dto;
using S100Framework.REST.Internal.Http;
using S100Framework.REST.Models;

namespace S100Framework.REST.Clients;

/// <summary>
/// Provides top-features query operations for feature service layer endpoints.
/// </summary>
public sealed partial class FeatureServiceClient
{
    internal Task<EsriQueryResponseDto> QueryTopFeaturesAsync(
        int layerId,
        TopFeaturesQuery query,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(query);

        query.Validate();

        var parameters = CreateCommonTopFeaturesParameters(
            query,
            includeOutSrid: true,
            includeGeometryOptions: true);

        parameters["f"] = "json";
        parameters["returnGeometry"] = query.ReturnGeometry ? "true" : "false";
        parameters["returnTrueCurves"] = _options.ReturnTrueCurves ? "true" : "false";
        parameters["outFields"] = query.OutFields is { Count: > 0 }
            ? string.Join(",", query.OutFields)
            : "*";

        return SendLayerQueryAsync<EsriQueryResponseDto>(
            $"{layerId.ToString(CultureInfo.InvariantCulture)}/queryTopFeatures",
            parameters,
            cancellationToken);
    }

    internal Task<EsriIdsResponseDto> QueryTopFeatureIdsAsync(
        int layerId,
        TopFeaturesQuery query,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(query);

        query.Validate();

        if (query.ObjectIds is { Count: > 0 }) {
            throw new InvalidOperationException(
                "ObjectIds cannot be combined with returnIdsOnly for queryTopFeatures.");
        }

        var parameters = CreateCommonTopFeaturesParameters(
            query,
            includeOutSrid: false,
            includeGeometryOptions: false);

        parameters["f"] = "json";
        parameters["returnIdsOnly"] = "true";

        return SendLayerQueryAsync<EsriIdsResponseDto>(
            $"{layerId.ToString(CultureInfo.InvariantCulture)}/queryTopFeatures",
            parameters,
            cancellationToken);
    }

    internal async Task<TopFeaturesCountResult> QueryTopFeatureCountAsync(
        int layerId,
        TopFeaturesQuery query,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(query);

        query.Validate();

        var parameters = CreateCommonTopFeaturesParameters(
            query,
            includeOutSrid: true,
            includeGeometryOptions: false);

        parameters["f"] = "json";
        parameters["returnCountOnly"] = "true";

        var endpointPath = $"{layerId.ToString(CultureInfo.InvariantCulture)}/queryTopFeatures";
        var endpointUri = UriUtility.AppendPath(_serviceUri, endpointPath);

        var dto = await SendLayerQueryAsync<EsriTopFeaturesCountResponseDto>(
            endpointPath,
            parameters,
            cancellationToken);

        FeatureExtent? extent = null;

        if (dto.Extent is not null &&
            dto.Extent.XMin.HasValue &&
            dto.Extent.XMax.HasValue &&
            dto.Extent.YMin.HasValue &&
            dto.Extent.YMax.HasValue) {
            var srid = dto.Extent.SpatialReference is null
                ? null
                : _options.PreferLatestWkid
                    ? dto.Extent.SpatialReference.LatestWkid ?? dto.Extent.SpatialReference.Wkid
                    : dto.Extent.SpatialReference.Wkid ?? dto.Extent.SpatialReference.LatestWkid;

            extent = new FeatureExtent(
                new Envelope(
                    dto.Extent.XMin.Value,
                    dto.Extent.XMax.Value,
                    dto.Extent.YMin.Value,
                    dto.Extent.YMax.Value),
                srid);
        }

        return new TopFeaturesCountResult(ReadRequiredTopFeaturesCount(dto.Count, endpointUri), extent);
    }

    private static long ReadRequiredTopFeaturesCount(
        long? count,
        Uri requestUri) {
        if (!count.HasValue) {
            throw new FeatureServiceException(
                "The queryTopFeatures count payload did not include a count value.",
                requestUri);
        }

        if (count.Value < 0) {
            throw new FeatureServiceException(
                "The queryTopFeatures count payload returned a negative count value.",
                requestUri);
        }

        return count.Value;
    }

    private static Dictionary<string, string?> CreateCommonTopFeaturesParameters(
        TopFeaturesQuery query,
        bool includeOutSrid,
        bool includeGeometryOptions) {
        var parameters = new Dictionary<string, string?> {
            ["where"] = string.IsNullOrWhiteSpace(query.Where) ? "1=1" : query.Where,
            ["objectIds"] = query.ObjectIds is { Count: > 0 }
                ? string.Join(",", query.ObjectIds)
                : null,
            ["topFilter"] = CreateTopFilterJson(query.TopFilter!)
        };

        if (includeOutSrid && query.OutSrid.HasValue) {
            parameters["outSR"] = query.OutSrid.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (includeGeometryOptions) {
            if (query.ReturnZ.HasValue) {
                parameters["returnZ"] = query.ReturnZ.Value ? "true" : "false";
            }

            if (query.ReturnM.HasValue) {
                parameters["returnM"] = query.ReturnM.Value ? "true" : "false";
            }

            if (query.GeometryPrecision.HasValue) {
                parameters["geometryPrecision"] = query.GeometryPrecision.Value.ToString(CultureInfo.InvariantCulture);
            }

            if (query.MaxAllowableOffset.HasValue) {
                parameters["maxAllowableOffset"] = query.MaxAllowableOffset.Value.ToString(CultureInfo.InvariantCulture);
            }
        }

        ApplySpatialFilter(parameters, query.SpatialFilter);

        return parameters;
    }

    private static string CreateTopFilterJson(TopFeaturesFilter topFilter) {
        return JsonSerializer.Serialize(new Dictionary<string, object?> {
            ["groupByFields"] = string.Join(",", topFilter.GroupByFields),
            ["topCount"] = topFilter.TopCount,
            ["orderByFields"] = string.Join(",", topFilter.OrderByFields)
        });
    }
}