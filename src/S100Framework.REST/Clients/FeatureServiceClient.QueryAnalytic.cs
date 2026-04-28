using System.Globalization;
using S100Framework.REST.Internal.Dto;
using S100Framework.REST.Models;

namespace S100Framework.REST.Clients;

/// <summary>
/// Provides layer-level <c>queryAnalytic</c> operations for feature service endpoints.
/// </summary>
public sealed partial class FeatureServiceClient
{
    internal Task<EsriQueryResponseDto> QueryAnalyticAsync(
        int layerId,
        QueryAnalyticRequest request,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);

        if (layerId < 0) {
            throw new ArgumentOutOfRangeException(nameof(layerId), layerId, "Layer ID must not be negative.");
        }

        request.Validate();

        return SendLayerQueryAsync<EsriQueryResponseDto>(
            $"{layerId.ToString(CultureInfo.InvariantCulture)}/queryAnalytic",
            CreateQueryAnalyticParameters(request),
            cancellationToken);
    }

    private static Dictionary<string, string?> CreateQueryAnalyticParameters(QueryAnalyticRequest request) {
        var parameters = new Dictionary<string, string?> {
            ["f"] = "json",
            ["where"] = string.IsNullOrWhiteSpace(request.Where) ? "1=1" : request.Where,
            ["analyticWhere"] = request.AnalyticWhere,
            ["outAnalytics"] = request.OutAnalyticsJson,
            ["outFields"] = request.OutFields is { Count: > 0 }
                ? string.Join(",", request.OutFields)
                : "*",
            ["returnGeometry"] = request.ReturnGeometry ? "true" : "false",
            ["orderByFields"] = request.OrderBy,
            ["dataFormat"] = "json"
        };

        if (request.OutSrid.HasValue) {
            parameters["outSR"] = request.OutSrid.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (request.ResultType.HasValue) {
            parameters["resultType"] = MapQueryAnalyticResultType(request.ResultType.Value);
        }

        if (request.CacheHint.HasValue) {
            parameters["cacheHint"] = request.CacheHint.Value ? "true" : "false";
        }

        if (request.ResultOffset.HasValue) {
            parameters["resultOffset"] = request.ResultOffset.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (request.ResultRecordCount.HasValue) {
            parameters["resultRecordCount"] = request.ResultRecordCount.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (!string.IsNullOrWhiteSpace(request.QuantizationParametersJson)) {
            parameters["quantizationParameters"] = request.QuantizationParametersJson;
        }

        if (request.SqlFormat.HasValue) {
            parameters["sqlFormat"] = MapQueryAnalyticSqlFormat(request.SqlFormat.Value);
        }

        ApplySpatialFilter(parameters, request.SpatialFilter);

        return parameters;
    }

    private static string MapQueryAnalyticResultType(FeatureQueryResultType value) {
        return value switch {
            FeatureQueryResultType.None => "none",
            FeatureQueryResultType.Standard => "standard",
            FeatureQueryResultType.Tile => "tile",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported query analytic result type.")
        };
    }

    private static string MapQueryAnalyticSqlFormat(FeatureQuerySqlFormat value) {
        return value switch {
            FeatureQuerySqlFormat.None => "none",
            FeatureQuerySqlFormat.Standard => "standard",
            FeatureQuerySqlFormat.Native => "native",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported query analytic SQL format.")
        };
    }
}