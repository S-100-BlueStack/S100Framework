using System.Globalization;
using System.Text.Json;
using S100Framework.REST.Internal.Dto;
using S100Framework.REST.Internal.Http;
using S100Framework.REST.Models;

namespace S100Framework.REST.Clients;

/// <summary>
/// Provides layer-level <c>queryDateBins</c> operations for feature service endpoints.
/// </summary>
public sealed partial class FeatureServiceClient
{
    internal Task<EsriQueryDateBinsResponseDto> QueryDateBinsAsync(
        int layerId,
        QueryDateBinsRequest request,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);

        if (layerId < 0) {
            throw new ArgumentOutOfRangeException(nameof(layerId), layerId, "Layer ID must not be negative.");
        }

        request.Validate();

        var endpointUri = UriUtility.WithQuery(
            UriUtility.AppendPath(
                UriUtility.AppendPath(_serviceUri, layerId.ToString(CultureInfo.InvariantCulture)),
                "queryDateBins"),
            CreateQueryDateBinsParameters(request));

        return GetAsync<EsriQueryDateBinsResponseDto>(endpointUri, cancellationToken);
    }

    private static Dictionary<string, string?> CreateQueryDateBinsParameters(QueryDateBinsRequest request) {
        static string FormatEpochMilliseconds(DateTimeOffset value) =>
            value.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);

        static string FormatTimeExtentBound(DateTimeOffset? value) =>
            value.HasValue
                ? FormatEpochMilliseconds(value.Value)
                : "null";

        var parameters = new Dictionary<string, string?> {
            ["f"] = "json",
            ["binField"] = request.BinField,
            ["bin"] = request.BinJson,
            ["outStatistics"] = SerializeQueryBinsStatistics(request.Statistics),
            ["where"] = string.IsNullOrWhiteSpace(request.Where) ? "1=1" : request.Where
        };

        if (request.TimeExtent is not null) {
            parameters["time"] =
                $"{FormatTimeExtentBound(request.TimeExtent.Start)},{FormatTimeExtentBound(request.TimeExtent.End)}";
        }

        if (request.BinOrder.HasValue) {
            parameters["binOrder"] = request.BinOrder.Value switch {
                QueryBinsOrder.Ascending => "ASC",
                QueryBinsOrder.Descending => "DESC",
                _ => throw new ArgumentOutOfRangeException(
                    nameof(request.BinOrder),
                    request.BinOrder,
                    "Unsupported bin order.")
            };
        }

        if (request.ReturnCentroid.HasValue) {
            parameters["returnCentroid"] = request.ReturnCentroid.Value ? "true" : "false";
        }

        if (request.OutSrid.HasValue) {
            parameters["outSR"] = request.OutSrid.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (!string.IsNullOrWhiteSpace(request.QuantizationParametersJson)) {
            parameters["quantizationParameters"] = request.QuantizationParametersJson;
        }

        if (request.ResultOffset.HasValue) {
            parameters["resultOffset"] = request.ResultOffset.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (request.ResultRecordCount.HasValue) {
            parameters["resultRecordCount"] = request.ResultRecordCount.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (request.ReturnExceededLimitFeatures.HasValue) {
            parameters["returnExceededLimitFeatures"] =
                request.ReturnExceededLimitFeatures.Value ? "true" : "false";
        }

        if (!string.IsNullOrWhiteSpace(request.BinBoundaryAlias)) {
            parameters["binBoundaryAlias"] = request.BinBoundaryAlias;
        }

        ApplySpatialFilter(parameters, request.SpatialFilter);

        return parameters;
    }
}