using System.Globalization;
using S100Framework.REST.Exceptions;
using S100Framework.REST.Internal.Dto;
using S100Framework.REST.Internal.Http;
using S100Framework.REST.Models;

namespace S100Framework.REST.Clients;

/// <summary>
/// Provides related-record query operations for feature service layer endpoints.
/// </summary>
public sealed partial class FeatureServiceClient
{
    internal async Task<EsriRelatedRecordsResponseDto> QueryRelatedRecordsAsync(
        int layerId,
        RelatedRecordsQuery query,
        bool returnCountOnly,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(query);

        query.Validate();

        var parameters = new Dictionary<string, string?> {
            ["f"] = "json",
            ["objectIds"] = string.Join(",", query.ObjectIds),
            ["relationshipId"] = query.RelationshipId.ToString(CultureInfo.InvariantCulture),
            ["outFields"] = query.OutFields is { Count: > 0 }
                ? string.Join(",", query.OutFields)
                : "*",
            ["definitionExpression"] = query.DefinitionExpression,
            ["returnGeometry"] = query.ReturnGeometry ? "true" : "false",
            ["returnTrueCurves"] = _options.ReturnTrueCurves ? "true" : "false",
            ["orderByFields"] = query.OrderBy
        };

        if (returnCountOnly) {
            parameters["returnCountOnly"] = "true";
        }

        if (query.ResultOffset.HasValue) {
            parameters["resultOffset"] = query.ResultOffset.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (query.ResultRecordCount.HasValue) {
            parameters["resultRecordCount"] = query.ResultRecordCount.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (query.OutSrid.HasValue) {
            parameters["outSR"] = query.OutSrid.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (!string.IsNullOrWhiteSpace(query.GdbVersion)) {
            parameters["gdbVersion"] = query.GdbVersion;
        }

        if (query.HistoricMoment.HasValue) {
            parameters["historicMoment"] = query.HistoricMoment.Value.ToUnixTimeMilliseconds()
                .ToString(CultureInfo.InvariantCulture);
        }

        if (query.TimeReferenceUnknownClient) {
            parameters["timeReferenceUnknownClient"] = "true";
        }

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
            parameters["maxAllowableOffset"] =
                query.MaxAllowableOffset.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (query.DatumTransformationWkid.HasValue) {
            parameters["datumTransformation"] =
                query.DatumTransformationWkid.Value.ToString(CultureInfo.InvariantCulture);
        }
        else if (!string.IsNullOrWhiteSpace(query.DatumTransformationJson)) {
            parameters["datumTransformation"] = query.DatumTransformationJson;
        }

        var endpointPath = $"{layerId.ToString(CultureInfo.InvariantCulture)}/queryRelatedRecords";
        var endpointUri = UriUtility.AppendPath(_serviceUri, endpointPath);

        var response = await SendLayerQueryAsync<EsriRelatedRecordsResponseDto>(
            endpointPath,
            parameters,
            cancellationToken);

        ValidateRelatedRecordGroups(response, endpointUri, returnCountOnly);

        return response;
    }

    private static void ValidateRelatedRecordGroups(
     EsriRelatedRecordsResponseDto response,
     Uri requestUri,
     bool returnCountOnly) {
        foreach (var group in response.RelatedRecordGroups ?? Enumerable.Empty<EsriRelatedRecordGroupDto?>()) {
            if (group is null) {
                continue;
            }

            if (!group.ObjectId.HasValue) {
                throw new FeatureServiceException(
                    "The queryRelatedRecords payload returned a related record group without an objectId.",
                    requestUri);
            }

            if (group.ObjectId.Value < 0) {
                throw new FeatureServiceException(
                    "The queryRelatedRecords payload returned a related record group with a negative objectId.",
                    requestUri);
            }

            if (returnCountOnly && group.Count is < 0) {
                throw new FeatureServiceException(
                    "The queryRelatedRecords count payload returned a related record group with a negative count value.",
                    requestUri);
            }
        }
    }
}
