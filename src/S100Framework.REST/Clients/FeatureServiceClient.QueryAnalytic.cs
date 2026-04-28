using System.Globalization;
using System.Text.Json;
using S100Framework.REST.Exceptions;
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

        ValidateQueryAnalyticLayerId(layerId);

        request.Validate();

        return SendLayerQueryAsync<EsriQueryResponseDto>(
            $"{layerId.ToString(CultureInfo.InvariantCulture)}/queryAnalytic",
            CreateQueryAnalyticParameters(request, queryAsync: false),
            cancellationToken);
    }

    internal async Task<QueryAnalyticSubmissionResponse> SubmitQueryAnalyticAsync(
        int layerId,
        QueryAnalyticRequest request,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);

        ValidateQueryAnalyticLayerId(layerId);

        request.Validate();

        using var document = await SendLayerQueryAsync<JsonDocument>(
            $"{layerId.ToString(CultureInfo.InvariantCulture)}/queryAnalytic",
            CreateQueryAnalyticParameters(request, queryAsync: true),
            cancellationToken);

        var root = document.RootElement;

        if (TryGetUri(root, "statusUrl", "statusURL", out var statusUrl)) {
            return new QueryAnalyticSubmissionResponse(
                Result: null,
                StatusUrl: statusUrl);
        }

        var result = root.Deserialize<EsriQueryResponseDto>(JsonOptions)
            ?? throw new FeatureServiceException(
                "The queryAnalytic submission payload could not be deserialized.",
                new Uri(_serviceUri, $"{layerId.ToString(CultureInfo.InvariantCulture)}/queryAnalytic"));

        return new QueryAnalyticSubmissionResponse(
            Result: result,
            StatusUrl: null);
    }

    internal async Task<QueryAnalyticJobStatus> GetQueryAnalyticStatusAsync(
        Uri statusUrl,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(statusUrl);

        using var document = await GetAsync<JsonDocument>(statusUrl, cancellationToken);
        var root = document.RootElement;

        return new QueryAnalyticJobStatus(
            Status: root.TryGetProperty("status", out var statusElement)
                ? statusElement.GetString() ?? "Unknown"
                : "Unknown",
            ResultUrl: TryGetUri(root, "resultUrl", "resultURL", out var resultUrl)
                ? resultUrl
                : null,
            SubmissionTime: TryGetInt64(root, "submissionTime", out var submissionTime)
                ? submissionTime
                : null,
            LastUpdatedTime: TryGetInt64(root, "lastUpdatedTime", out var lastUpdatedTime)
                ? lastUpdatedTime
                : null);
    }

    internal Task<EsriQueryResponseDto> GetQueryAnalyticResultAsync(
        Uri resultUrl,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(resultUrl);

        return GetAsync<EsriQueryResponseDto>(resultUrl, cancellationToken);
    }

    private static void ValidateQueryAnalyticLayerId(int layerId) {
        if (layerId < 0) {
            throw new ArgumentOutOfRangeException(nameof(layerId), layerId, "Layer ID must not be negative.");
        }
    }

    private static Dictionary<string, string?> CreateQueryAnalyticParameters(
        QueryAnalyticRequest request,
        bool queryAsync) {
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
            ["async"] = queryAsync ? "true" : "false",
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

    private static bool TryGetUri(
        JsonElement root,
        string primaryPropertyName,
        string alternatePropertyName,
        out Uri? uri) {
        uri = null;

        if (!TryGetString(root, primaryPropertyName, alternatePropertyName, out var rawUri) ||
            string.IsNullOrWhiteSpace(rawUri)) {
            return false;
        }

        uri = new Uri(rawUri, UriKind.Absolute);
        return true;
    }

    private static bool TryGetString(
        JsonElement root,
        string primaryPropertyName,
        string alternatePropertyName,
        out string? value) {
        value = null;

        if (root.TryGetProperty(primaryPropertyName, out var primaryElement)) {
            value = primaryElement.GetString();
            return true;
        }

        if (root.TryGetProperty(alternatePropertyName, out var alternateElement)) {
            value = alternateElement.GetString();
            return true;
        }

        return false;
    }

    private static bool TryGetInt64(
        JsonElement root,
        string propertyName,
        out long value) {
        value = default;

        if (!root.TryGetProperty(propertyName, out var element)) {
            return false;
        }

        return element.ValueKind switch {
            JsonValueKind.Number => element.TryGetInt64(out value),
            JsonValueKind.String => long.TryParse(
                element.GetString(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out value),
            _ => false
        };
    }

    internal sealed record QueryAnalyticSubmissionResponse(
        EsriQueryResponseDto? Result,
        Uri? StatusUrl);
}