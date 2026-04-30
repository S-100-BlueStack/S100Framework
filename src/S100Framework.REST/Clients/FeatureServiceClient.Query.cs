using System.Globalization;
using System.Text.Json;
using NetTopologySuite.Geometries;
using S100Framework.REST.Internal.Dto;
using S100Framework.REST.Models;

namespace S100Framework.REST.Clients;

/// <summary>
/// Provides standard layer query operations for feature service endpoints.
/// </summary>
public sealed partial class FeatureServiceClient
{
    internal Task<EsriQueryResponseDto> QueryFeaturesAsync(
        int layerId,
        FeatureQuery query,
        int? resultOffset,
        int? resultRecordCount,
        IReadOnlyList<long>? objectIds,
        IReadOnlyList<FeatureUniqueId>? uniqueIds = null,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(query);

        ValidateFeatureQueryCommon(query);
        ValidateFeatureQueryProjection(query);
        ValidateFeatureQueryGeometryOptions(query);
        ValidateFeatureQueryOutFields(query);

        if (query.ReturnEnvelope && !query.ReturnGeometry) {
            throw new InvalidOperationException("ReturnEnvelope requires ReturnGeometry to be true.");
        }

        if (objectIds is { Count: > 0 } && uniqueIds is { Count: > 0 }) {
            throw new InvalidOperationException("ObjectIds and uniqueIds cannot be combined in the same feature query request.");
        }

        var parameters = CreateCommonQueryParameters(query, includeOutSrid: true, includeGeometryOptions: true);

        parameters["f"] = "json";
        parameters["returnGeometry"] = query.ReturnGeometry ? "true" : "false";
        parameters["returnTrueCurves"] = _options.ReturnTrueCurves ? "true" : "false";
        parameters["outFields"] = query.OutFields is { Count: > 0 }
            ? string.Join(",", query.OutFields)
            : "*";

        if (query.ReturnEnvelope) {
            parameters["returnEnvelope"] = "true";
        }

        if (query.ReturnCentroid.HasValue) {
            parameters["returnCentroid"] = query.ReturnCentroid.Value ? "true" : "false";
        }

        if (objectIds is { Count: > 0 }) {
            parameters.Remove("where");
            parameters.Remove("uniqueIds");
            parameters["objectIds"] = string.Join(",", objectIds);
        }
        else if (uniqueIds is { Count: > 0 }) {
            parameters["uniqueIds"] = SerializeFeatureUniqueIds(uniqueIds);
        }

        if (resultOffset.HasValue) {
            parameters["resultOffset"] = resultOffset.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (resultRecordCount.HasValue) {
            parameters["resultRecordCount"] = resultRecordCount.Value.ToString(CultureInfo.InvariantCulture);
        }

        return SendLayerQueryAsync<EsriQueryResponseDto>(
            $"{layerId.ToString(CultureInfo.InvariantCulture)}/query",
            parameters,
            cancellationToken);
    }

    internal Task<EsriIdsResponseDto> QueryIdsAsync(
        int layerId,
        FeatureQuery query,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(query);

        ValidateFeatureQueryCommon(query);

        var parameters = CreateCommonQueryParameters(query, includeOutSrid: false, includeGeometryOptions: false);

        parameters["f"] = "json";
        parameters["returnIdsOnly"] = "true";

        return SendLayerQueryAsync<EsriIdsResponseDto>(
            $"{layerId.ToString(CultureInfo.InvariantCulture)}/query",
            parameters,
            cancellationToken);
    }

    internal Task<EsriUniqueIdsResponseDto> QueryUniqueIdsAsync(
        int layerId,
        FeatureQuery query,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(query);

        ValidateFeatureQueryCommon(query);

        var parameters = CreateCommonQueryParameters(query, includeOutSrid: false, includeGeometryOptions: false);

        parameters["f"] = "json";
        parameters["returnUniqueIdsOnly"] = "true";

        return SendLayerQueryAsync<EsriUniqueIdsResponseDto>(
            $"{layerId.ToString(CultureInfo.InvariantCulture)}/query",
            parameters,
            cancellationToken);
    }

    internal async Task<long> QueryCountAsync(
        int layerId,
        FeatureQuery query,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(query);

        ValidateFeatureQueryCommon(query);

        var parameters = CreateCommonQueryParameters(query, includeOutSrid: false, includeGeometryOptions: false);

        parameters["f"] = "json";
        parameters["returnCountOnly"] = "true";

        var dto = await SendLayerQueryAsync<EsriCountResponseDto>(
            $"{layerId.ToString(CultureInfo.InvariantCulture)}/query",
            parameters,
            cancellationToken);

        return dto.Count;
    }

    internal async Task<FeatureExtent?> QueryExtentAsync(
        int layerId,
        FeatureQuery query,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(query);

        ValidateFeatureQueryCommon(query);
        ValidateFeatureQueryProjection(query);

        var parameters = CreateCommonQueryParameters(query, includeOutSrid: true, includeGeometryOptions: false);

        parameters["f"] = "json";
        parameters["returnExtentOnly"] = "true";

        var dto = await SendLayerQueryAsync<EsriExtentResponseDto>(
            $"{layerId.ToString(CultureInfo.InvariantCulture)}/query",
            parameters,
            cancellationToken);

        if (dto.Extent is null ||
            dto.Extent.XMin is null ||
            dto.Extent.YMin is null ||
            dto.Extent.XMax is null ||
            dto.Extent.YMax is null) {
            return null;
        }

        var srid = dto.Extent.SpatialReference is null
            ? null
            : _options.PreferLatestWkid
                ? dto.Extent.SpatialReference.LatestWkid ?? dto.Extent.SpatialReference.Wkid
                : dto.Extent.SpatialReference.Wkid ?? dto.Extent.SpatialReference.LatestWkid;

        return new FeatureExtent(
            new Envelope(
                dto.Extent.XMin.Value,
                dto.Extent.XMax.Value,
                dto.Extent.YMin.Value,
                dto.Extent.YMax.Value),
            srid);
    }

    private static void ValidateFeatureQueryCommon(FeatureQuery query) {
        static void ValidateJsonObject(string? json, string propertyName) {
            if (json is null) {
                return;
            }

            if (string.IsNullOrWhiteSpace(json)) {
                throw new InvalidOperationException($"{propertyName} must not be empty when provided.");
            }

            try {
                using var document = JsonDocument.Parse(json);

                if (document.RootElement.ValueKind != JsonValueKind.Object) {
                    throw new InvalidOperationException($"{propertyName} must be a JSON object.");
                }
            }
            catch (JsonException exception) {
                throw new InvalidOperationException($"{propertyName} must contain valid JSON.", exception);
            }
        }

        if (query.OrderBy is not null && string.IsNullOrWhiteSpace(query.OrderBy)) {
            throw new InvalidOperationException("OrderBy must not be empty when provided.");
        }

        if (query.DefaultSrid is <= 0) {
            throw new InvalidOperationException("DefaultSrid must be greater than zero when provided.");
        }

        if (query.DatumTransformationWkid is <= 0) {
            throw new InvalidOperationException("DatumTransformationWkid must be greater than zero when provided.");
        }

        if (query.DatumTransformationWkid.HasValue && query.DatumTransformationJson is not null) {
            throw new InvalidOperationException(
                "DatumTransformationWkid and DatumTransformationJson cannot both be specified.");
        }

        ValidateJsonObject(query.DatumTransformationJson, nameof(query.DatumTransformationJson));
        ValidateJsonObject(query.QuantizationParametersJson, nameof(query.QuantizationParametersJson));

        if (query.MultipatchOption.HasValue && !query.ReturnGeometry) {
            throw new InvalidOperationException("MultipatchOption requires ReturnGeometry to be true.");
        }

        if (query.UniqueIds is { Count: 0 }) {
            throw new InvalidOperationException("UniqueIds must contain at least one identifier when provided.");
        }

        if (query.UniqueIds is not null) {
            foreach (var uniqueId in query.UniqueIds) {
                if (uniqueId is null) {
                    throw new InvalidOperationException("UniqueIds must not contain null values.");
                }

                if (uniqueId.Components is not { Count: > 0 }) {
                    throw new InvalidOperationException("Each UniqueIds entry must contain at least one component.");
                }

                if (uniqueId.Components.Any(string.IsNullOrWhiteSpace)) {
                    throw new InvalidOperationException("UniqueIds components must not be empty.");
                }
            }
        }

        if (query.TimeInstant.HasValue && query.TimeExtent is not null) {
            throw new InvalidOperationException("TimeInstant and TimeExtent cannot both be specified.");
        }

        if (query.TimeExtent is not null) {
            if (!query.TimeExtent.Start.HasValue && !query.TimeExtent.End.HasValue) {
                throw new InvalidOperationException("TimeExtent must specify at least one bound.");
            }

            if (query.TimeExtent.Start.HasValue &&
                query.TimeExtent.End.HasValue &&
                query.TimeExtent.Start.Value > query.TimeExtent.End.Value) {
                throw new InvalidOperationException(
                    "TimeExtent.Start must be less than or equal to TimeExtent.End when both are provided.");
            }
        }
    }

    private static void ValidateFeatureQueryProjection(FeatureQuery query) {
        if (query.OutSrid is <= 0) {
            throw new InvalidOperationException("OutSrid must be greater than zero when provided.");
        }
    }

    private static void ValidateFeatureQueryGeometryOptions(FeatureQuery query) {
        if (query.GeometryPrecision is < 0) {
            throw new InvalidOperationException("GeometryPrecision must be greater than or equal to zero when provided.");
        }

        if (query.MaxAllowableOffset is < 0) {
            throw new InvalidOperationException("MaxAllowableOffset must be greater than or equal to zero when provided.");
        }
    }

    private static void ValidateFeatureQueryOutFields(FeatureQuery query) {
        if (query.OutFields?.Any(static field => string.IsNullOrWhiteSpace(field)) == true) {
            throw new InvalidOperationException("OutFields must not contain null, empty, or whitespace-only values.");
        }
    }

    private static Dictionary<string, string?> CreateCommonQueryParameters(
        FeatureQuery query,
        bool includeOutSrid,
        bool includeGeometryOptions) {
        static string FormatEpochMilliseconds(DateTimeOffset value) =>
            value.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);

        static string FormatTimeExtentBound(DateTimeOffset? value) =>
            value.HasValue
                ? FormatEpochMilliseconds(value.Value)
                : "null";

        static string MapResultType(FeatureQueryResultType value) {
            return value switch {
                FeatureQueryResultType.None => "none",
                FeatureQueryResultType.Standard => "standard",
                FeatureQueryResultType.Tile => "tile",
                _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
            };
        }

        static string MapMultipatchOption(FeatureQueryMultipatchOption value) {
            return value switch {
                FeatureQueryMultipatchOption.XyFootprint => "xyFootprint",
                FeatureQueryMultipatchOption.StripMaterials => "stripMaterials",
                FeatureQueryMultipatchOption.EmbedMaterials => "embedMaterials",
                FeatureQueryMultipatchOption.ExternalizeTextures => "externalizeTextures",
                FeatureQueryMultipatchOption.Extent => "extent",
                _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
            };
        }

        static string SerializeFullText(IReadOnlyList<FeatureQueryFullTextExpression> expressions) {
            static string MapSearchType(FeatureQueryFullTextSearchType value) =>
                value switch {
                    FeatureQueryFullTextSearchType.Simple => "simple",
                    FeatureQueryFullTextSearchType.Prefix => "prefix",
                    _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
                };

            static string MapOperator(FeatureQueryFullTextOperator value) =>
                value switch {
                    FeatureQueryFullTextOperator.And => "and",
                    FeatureQueryFullTextOperator.Or => "or",
                    FeatureQueryFullTextOperator.Not => "not",
                    _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
                };

            static string MapSearchOperator(FeatureQueryFullTextSearchOperator value) =>
                value switch {
                    FeatureQueryFullTextSearchOperator.And => "and",
                    FeatureQueryFullTextSearchOperator.Or => "or",
                    _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
                };

            ArgumentNullException.ThrowIfNull(expressions);

            if (expressions.Count == 0) {
                throw new InvalidOperationException("FullText must contain at least one expression when provided.");
            }

            var payload = new List<Dictionary<string, object?>>(expressions.Count);

            foreach (var expression in expressions) {
                ArgumentNullException.ThrowIfNull(expression);

                var hasSqlExpression = !string.IsNullOrWhiteSpace(expression.SqlExpression);
                var hasStructuredExpression =
                    expression.OnFields is { Count: > 0 } &&
                    !string.IsNullOrWhiteSpace(expression.SearchTerm);

                if (hasSqlExpression == hasStructuredExpression) {
                    throw new InvalidOperationException(
                        "Each FullText expression must specify either SqlExpression or OnFields/SearchTerm, but not both.");
                }

                var entry = new Dictionary<string, object?>();

                if (hasSqlExpression) {
                    entry["sqlExpression"] = expression.SqlExpression;
                }
                else {
                    if (expression.OnFields!.Any(string.IsNullOrWhiteSpace)) {
                        throw new InvalidOperationException("FullText OnFields must not contain empty field names.");
                    }

                    entry["onFields"] = expression.OnFields;
                    entry["searchTerm"] = expression.SearchTerm;

                    if (expression.SearchType.HasValue) {
                        entry["searchType"] = MapSearchType(expression.SearchType.Value);
                    }

                    if (expression.Operator.HasValue) {
                        entry["operator"] = MapOperator(expression.Operator.Value);
                    }
                }

                if (expression.SearchOperator.HasValue) {
                    entry["searchOperator"] = MapSearchOperator(expression.SearchOperator.Value);
                }

                payload.Add(entry);
            }

            return JsonSerializer.Serialize(payload);
        }


        var parameters = new Dictionary<string, string?> {
            ["where"] = string.IsNullOrWhiteSpace(query.Where) ? "1=1" : query.Where,
            ["orderByFields"] = query.OrderBy
        };

        if (query.TimeInstant.HasValue) {
            parameters["time"] = FormatEpochMilliseconds(query.TimeInstant.Value);
        }
        else if (query.TimeExtent is not null) {
            parameters["time"] =
                $"{FormatTimeExtentBound(query.TimeExtent.Start)},{FormatTimeExtentBound(query.TimeExtent.End)}";
        }

        if (query.HistoricMoment.HasValue) {
            parameters["historicMoment"] = FormatEpochMilliseconds(query.HistoricMoment.Value);
        }

        if (query.DefaultSrid.HasValue) {
            parameters["defaultSR"] = query.DefaultSrid.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (query.ResultType.HasValue) {
            parameters["resultType"] = MapResultType(query.ResultType.Value);
        }

        if (query.ReturnExceededLimitFeatures.HasValue) {
            parameters["returnExceededLimitFeatures"] =
                query.ReturnExceededLimitFeatures.Value ? "true" : "false";
        }

        if (query.DatumTransformationWkid.HasValue) {
            parameters["datumTransformation"] =
                query.DatumTransformationWkid.Value.ToString(CultureInfo.InvariantCulture);
        }
        else if (!string.IsNullOrWhiteSpace(query.DatumTransformationJson)) {
            parameters["datumTransformation"] = query.DatumTransformationJson;
        }

        if (query.SqlFormat.HasValue) {
            parameters["sqlFormat"] = query.SqlFormat.Value switch {
                FeatureQuerySqlFormat.None => "none",
                FeatureQuerySqlFormat.Standard => "standard",
                FeatureQuerySqlFormat.Native => "native",
                _ => throw new ArgumentOutOfRangeException(nameof(query.SqlFormat), query.SqlFormat, null)
            };
        }

        if (query.FullText is not null) {
            parameters["fullText"] = SerializeFullText(query.FullText);
        }

        if (query.UniqueIds is not null) {
            parameters["uniqueIds"] = SerializeFeatureUniqueIds(query.UniqueIds);
        }

        if (includeOutSrid && query.OutSrid.HasValue) {
            parameters["outSR"] = query.OutSrid.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (query.ReturnDistinctValues) {
            parameters["returnDistinctValues"] = "true";
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

            if (!string.IsNullOrWhiteSpace(query.QuantizationParametersJson)) {
                parameters["quantizationParameters"] = query.QuantizationParametersJson;
            }

            if (query.MultipatchOption.HasValue) {
                parameters["multipatchOption"] = MapMultipatchOption(query.MultipatchOption.Value);
            }
        }

        ApplySpatialFilter(parameters, query.SpatialFilter);

        return parameters;
    }

    private static string SerializeFeatureUniqueIds(IReadOnlyList<FeatureUniqueId> uniqueIds) {
        ArgumentNullException.ThrowIfNull(uniqueIds);

        if (uniqueIds.Count == 0) {
            throw new InvalidOperationException("UniqueIds must contain at least one identifier when provided.");
        }

        var payload = new List<object>(uniqueIds.Count);

        foreach (var uniqueId in uniqueIds) {
            ArgumentNullException.ThrowIfNull(uniqueId);

            if (uniqueId.Components is not { Count: > 0 }) {
                throw new InvalidOperationException("Each UniqueIds entry must contain at least one component.");
            }

            if (uniqueId.Components.Any(string.IsNullOrWhiteSpace)) {
                throw new InvalidOperationException("UniqueIds components must not be empty.");
            }

            payload.Add(uniqueId.Components.Count == 1
                ? uniqueId.Components[0]
                : uniqueId.Components.ToArray());
        }

        return JsonSerializer.Serialize(payload, JsonOptions);
    }
}