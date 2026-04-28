using System.Globalization;
using System.Text.Json;
using S100Framework.REST.Internal.Dto;
using S100Framework.REST.Internal.Http;
using S100Framework.REST.Models;

namespace S100Framework.REST.Clients;

/// <summary>
/// Provides layer-level <c>queryBins</c> operations for feature service endpoints.
/// </summary>
public sealed partial class FeatureServiceClient
{
    internal Task<EsriQueryBinsResponseDto> QueryBinsAsync(
        int layerId,
        QueryBinsRequest request,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);

        if (layerId < 0) {
            throw new ArgumentOutOfRangeException(nameof(layerId), layerId, "Layer ID must not be negative.");
        }

        request.Validate();

        var endpointUri = UriUtility.WithQuery(
            UriUtility.AppendPath(
                UriUtility.AppendPath(_serviceUri, layerId.ToString(CultureInfo.InvariantCulture)),
                "queryBins"),
            CreateQueryBinsParameters(request));

        return GetAsync<EsriQueryBinsResponseDto>(endpointUri, cancellationToken);
    }

    private static Dictionary<string, string?> CreateQueryBinsParameters(QueryBinsRequest request) {
        var parameters = new Dictionary<string, string?> {
            ["f"] = "json",
            ["where"] = string.IsNullOrWhiteSpace(request.Where) ? "1=1" : request.Where,
            ["bin"] = request.BinJson
        };

        if (request.Statistics is { Count: > 0 }) {
            parameters["outStatistics"] = SerializeOutStatistics(
                request.Statistics,
                PercentileStatisticNameFormat.LowerRest);
        }

        if (request.BinOrder.HasValue) {
            parameters["binOrder"] = MapQueryBinsOrder(request.BinOrder.Value);
        }

        if (request.OutTimeReferenceJson is not null) {
            parameters["outTimeReference"] = request.OutTimeReferenceJson;
        }

        if (request.DefaultSrid.HasValue) {
            parameters["defaultSR"] = request.DefaultSrid.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (request.OutSrid.HasValue) {
            parameters["outSR"] = request.OutSrid.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (request.DatumTransformationWkid.HasValue) {
            parameters["datumTransformation"] =
                request.DatumTransformationWkid.Value.ToString(CultureInfo.InvariantCulture);
        }
        else if (request.DatumTransformationJson is not null) {
            parameters["datumTransformation"] = request.DatumTransformationJson;
        }

        if (request.QuantizationParametersJson is not null) {
            parameters["quantizationParameters"] = request.QuantizationParametersJson;
        }

        if (!string.IsNullOrWhiteSpace(request.LowerBoundaryAlias)) {
            parameters["lowerBoundaryAlias"] = request.LowerBoundaryAlias;
        }

        if (!string.IsNullOrWhiteSpace(request.UpperBoundaryAlias)) {
            parameters["upperBoundaryAlias"] = request.UpperBoundaryAlias;
        }

        ApplySpatialFilter(parameters, request.SpatialFilter);

        return parameters;
    }
}