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

        var endpointUri = new Uri(_serviceUri, $"{layerId.ToString(CultureInfo.InvariantCulture)}/queryAnalytic");

        var statusUrl = ReadOptionalAbsoluteUri(
            root,
            endpointUri,
            "queryAnalytic",
            "statusUrl",
            "statusURL");

        if (statusUrl is not null) {
            return new QueryAnalyticSubmissionResponse(
                Result: null,
                StatusUrl: statusUrl);
        }

        var result = root.Deserialize<EsriQueryResponseDto>(JsonOptions)
     ?? throw new FeatureServiceException(
         "The queryAnalytic submission payload could not be deserialized.",
         endpointUri);

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
     Status: TryGetString(root, "status", "jobStatus", out var status)
         ? status ?? "Unknown"
         : "Unknown",
     ResultUrl: ReadOptionalAbsoluteUri(
         root,
         statusUrl,
         "queryAnalytic",
         "resultUrl",
         "resultURL"),
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

    private static Uri? ReadOptionalAbsoluteUri(
     JsonElement root,
     Uri endpointUri,
     string operationName,
     params string[] propertyNames) {
        foreach (var propertyName in propertyNames) {
            if (!root.TryGetProperty(propertyName, out var element)) {
                continue;
            }

            if (element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) {
                return null;
            }

            if (element.ValueKind != JsonValueKind.String) {
                throw new FeatureServiceException(
                    $"The server returned an invalid {propertyName} for {operationName}.",
                    endpointUri);
            }

            var rawUri = element.GetString();

            if (string.IsNullOrWhiteSpace(rawUri)) {
                return null;
            }

            if (!Uri.TryCreate(rawUri, UriKind.Absolute, out var uri)) {
                throw new FeatureServiceException(
                    $"The server returned an invalid {propertyName} for {operationName}.",
                    endpointUri);
            }

            return uri;
        }

        return null;
    }

    private static bool TryGetUri(
    JsonElement root,
    string propertyName,
    string alternatePropertyName,
    out Uri? uri) {
        uri = null;

        if (!TryGetString(root, propertyName, alternatePropertyName, out var rawUri) ||
            string.IsNullOrWhiteSpace(rawUri)) {
            return false;
        }

        return Uri.TryCreate(rawUri, UriKind.Absolute, out uri);
    }

    private static bool TryGetString(
        JsonElement root,
        string propertyName,
        out string? value) {
        value = null;

        if (!root.TryGetProperty(propertyName, out var element)) {
            return false;
        }

        value = element.GetString();
        return true;
    }

    private static bool TryGetString(
        JsonElement root,
        string primaryPropertyName,
        string alternatePropertyName,
        out string? value) {
        return TryGetString(
            root,
            [primaryPropertyName, alternatePropertyName],
            out value);
    }

    private static bool TryGetString(
        JsonElement root,
        IReadOnlyList<string> propertyNames,
        out string? value) {
        ArgumentNullException.ThrowIfNull(propertyNames);

        value = null;

        foreach (var propertyName in propertyNames) {
            if (TryGetString(root, propertyName, out value)) {
                return true;
            }
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