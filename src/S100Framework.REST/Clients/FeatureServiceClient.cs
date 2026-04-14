using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using System.Net.Http.Headers;
using NetTopologySuite.Geometries;
using S100Framework.REST.Abstractions;
using S100Framework.REST.Configuration;
using S100Framework.REST.Exceptions;
using S100Framework.REST.Internal.Dto;
using S100Framework.REST.Internal.EsriGeometry;
using S100Framework.REST.Internal.Http;
using S100Framework.REST.Models;

namespace S100Framework.REST.Clients;

public sealed class FeatureServiceClient : IFeatureServiceClient
{
    private static readonly JsonSerializerOptions JsonOptions = new() {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly FeatureServiceClientOptions _options;
    private readonly IFeatureServiceRequestAuthorizer? _authorizer;
    private readonly Uri _serviceUri;

    public FeatureServiceClient(
        HttpClient httpClient,
        FeatureServiceClientOptions options)
        : this(httpClient, options, authorizer: null) {
    }

    public FeatureServiceClient(
        HttpClient httpClient,
        FeatureServiceClientOptions options,
        IFeatureServiceRequestAuthorizer? authorizer) {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _authorizer = authorizer;

        _options.Validate();
        _serviceUri = _options.ServiceUri ?? throw new InvalidOperationException("ServiceUri must be configured.");
    }

    public async Task<FeatureServiceMetadata> GetMetadataAsync(CancellationToken cancellationToken = default) {
        var uri = UriUtility.WithQuery(
            _serviceUri,
            new Dictionary<string, string?> {
                ["f"] = "json"
            });

        var dto = await GetAsync<EsriServiceMetadataDto>(uri, cancellationToken);

        return new FeatureServiceMetadata(
            _serviceUri,
            dto.Layers?.Select(MapDataset).ToArray() ?? Array.Empty<FeatureServiceDatasetInfo>(),
            dto.Tables?.Select(MapDataset).ToArray() ?? Array.Empty<FeatureServiceDatasetInfo>(),
            dto.Capabilities,
            dto.MaxRecordCount);
    }

    public IFeatureLayerClient GetLayerClient(int layerId) {
        if (layerId < 0) {
            throw new ArgumentOutOfRangeException(nameof(layerId));
        }

        return new FeatureLayerClient(this, layerId);
    }

    internal async Task<FeatureLayerSchema> GetLayerSchemaAsync(
        int layerId,
        CancellationToken cancellationToken = default) {
        var uri = UriUtility.WithQuery(
            UriUtility.AppendPath(_serviceUri, layerId.ToString(CultureInfo.InvariantCulture)),
            new Dictionary<string, string?> {
                ["f"] = "json"
            });

        var dto = await GetAsync<EsriLayerMetadataDto>(uri, cancellationToken);

        var srid = dto.Extent?.SpatialReference is null
            ? null
            : _options.PreferLatestWkid
                ? dto.Extent.SpatialReference.LatestWkid ?? dto.Extent.SpatialReference.Wkid
                : dto.Extent.SpatialReference.Wkid ?? dto.Extent.SpatialReference.LatestWkid;

        return new FeatureLayerSchema(
    dto.Id,
    dto.Name ?? $"Layer {dto.Id}",
    dto.GeometryType,
    srid,
    dto.HasZ ?? false,
    dto.HasM ?? false,
    dto.AdvancedQueryCapabilities?.SupportsPagination ?? false,
    dto.MaxRecordCount,
    dto.ObjectIdField,
    dto.Fields?.Select(MapField).ToArray() ?? Array.Empty<FeatureField>(),
    new FeatureLayerCapabilities(
        dto.HasAttachments ?? false,
        dto.SupportsQueryAttachments ?? false,
        dto.SupportsAttachmentsResizing ?? false,
        dto.SupportsTopFeaturesQuery ?? false,
        dto.AdvancedQueryCapabilities?.SupportsPagination ?? false,
        dto.AdvancedQueryCapabilities?.SupportsPaginationOnAggregatedQueries ?? false,
        dto.AdvancedQueryCapabilities?.SupportsQueryRelatedPagination ?? false,
        dto.AdvancedQueryCapabilities?.SupportsAdvancedQueryRelated ?? false,
        dto.AdvancedQueryCapabilities?.SupportsOrderBy ?? false,
        dto.AdvancedQueryCapabilities?.SupportsDistinct ?? false),
    dto.Relationships?.Select(MapRelationship).ToArray() ?? Array.Empty<FeatureRelationshipInfo>());
    }

    internal Task<EsriQueryResponseDto> QueryFeaturesAsync(
        int layerId,
        FeatureQuery query,
        int? resultOffset,
        int? resultRecordCount,
        IReadOnlyList<long>? objectIds,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(query);

        var parameters = CreateCommonQueryParameters(query, includeOutSrid: true, includeGeometryOptions: true);

        parameters["f"] = "json";
        parameters["returnGeometry"] = query.ReturnGeometry ? "true" : "false";
        parameters["returnTrueCurves"] = _options.ReturnTrueCurves ? "true" : "false";
        parameters["outFields"] = query.OutFields is { Count: > 0 }
            ? string.Join(",", query.OutFields)
            : "*";

        if (objectIds is { Count: > 0 }) {
            parameters.Remove("where");
            parameters["objectIds"] = string.Join(",", objectIds);
        }

        if (resultOffset.HasValue) {
            parameters["resultOffset"] = resultOffset.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (resultRecordCount.HasValue) {
            parameters["resultRecordCount"] = resultRecordCount.Value.ToString(CultureInfo.InvariantCulture);
        }

        var uri = UriUtility.WithQuery(
            UriUtility.AppendPath(_serviceUri, $"{layerId.ToString(CultureInfo.InvariantCulture)}/query"),
            parameters);

        return GetAsync<EsriQueryResponseDto>(uri, cancellationToken);
    }

    internal Task<EsriIdsResponseDto> QueryIdsAsync(
        int layerId,
        FeatureQuery query,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(query);

        var parameters = CreateCommonQueryParameters(query, includeOutSrid: false, includeGeometryOptions: false);

        parameters["f"] = "json";
        parameters["returnIdsOnly"] = "true";

        var uri = UriUtility.WithQuery(
            UriUtility.AppendPath(_serviceUri, $"{layerId.ToString(CultureInfo.InvariantCulture)}/query"),
            parameters);

        return GetAsync<EsriIdsResponseDto>(uri, cancellationToken);
    }

    internal async Task<long> QueryCountAsync(
        int layerId,
        FeatureQuery query,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(query);

        var parameters = CreateCommonQueryParameters(query, includeOutSrid: false, includeGeometryOptions: false);

        parameters["f"] = "json";
        parameters["returnCountOnly"] = "true";

        var uri = UriUtility.WithQuery(
            UriUtility.AppendPath(_serviceUri, $"{layerId.ToString(CultureInfo.InvariantCulture)}/query"),
            parameters);

        var dto = await GetAsync<EsriCountResponseDto>(uri, cancellationToken);
        return dto.Count;
    }

    internal async Task<FeatureExtent?> QueryExtentAsync(
        int layerId,
        FeatureQuery query,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(query);

        var parameters = CreateCommonQueryParameters(query, includeOutSrid: true, includeGeometryOptions: false);

        parameters["f"] = "json";
        parameters["returnExtentOnly"] = "true";

        var uri = UriUtility.WithQuery(
            UriUtility.AppendPath(_serviceUri, $"{layerId.ToString(CultureInfo.InvariantCulture)}/query"),
            parameters);

        var dto = await GetAsync<EsriExtentResponseDto>(uri, cancellationToken);

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

    internal FeatureServiceClientOptions Options => _options;

    private static Dictionary<string, string?> CreateCommonQueryParameters(
        FeatureQuery query,
        bool includeOutSrid,
        bool includeGeometryOptions) {
        var parameters = new Dictionary<string, string?> {
            ["where"] = string.IsNullOrWhiteSpace(query.Where) ? "1=1" : query.Where,
            ["orderByFields"] = query.OrderBy
        };

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
        }

        ApplySpatialFilter(parameters, query.SpatialFilter);

        return parameters;
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

    private static void ApplySpatialFilter(
        IDictionary<string, string?> parameters,
        FeatureSpatialFilter? spatialFilter) {
        if (spatialFilter is null) {
            return;
        }

        parameters["geometry"] = spatialFilter.GeometryJson;
        parameters["geometryType"] = spatialFilter.GeometryType;
        parameters["spatialRel"] = SpatialRelationshipMapper.ToEsriValue(spatialFilter.SpatialRelationship);

        if (spatialFilter.InSrid.HasValue) {
            parameters["inSR"] = spatialFilter.InSrid.Value.ToString(CultureInfo.InvariantCulture);
        }
    }

    private async Task<T> GetAsync<T>(Uri uri, CancellationToken cancellationToken) {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_options.RequestTimeout);

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);

        if (_authorizer is not null) {
            await _authorizer.ApplyAsync(request, timeoutCts.Token);
        }

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            timeoutCts.Token);

        var payload = await response.Content.ReadAsStringAsync(timeoutCts.Token);

        if (string.IsNullOrWhiteSpace(payload)) {
            throw new FeatureServiceException(
                "The server returned an empty payload.",
                uri,
                statusCode: response.StatusCode);
        }

        if (TryParseEsriError(payload, out var esriError)) {
            throw new FeatureServiceException(
                esriError.Message ?? "The server returned an Esri error payload.",
                uri,
                esriError.Code,
                esriError.Details?.ToArray(),
                response.StatusCode);
        }

        if (!response.IsSuccessStatusCode) {
            throw new FeatureServiceException(
                $"The server returned HTTP {(int)response.StatusCode} ({response.StatusCode}).",
                uri,
                statusCode: response.StatusCode);
        }

        try {
            var result = JsonSerializer.Deserialize<T>(payload, JsonOptions);

            return result ?? throw new FeatureServiceException(
                $"The payload could not be deserialized to {typeof(T).Name}.",
                uri,
                statusCode: response.StatusCode);
        }
        catch (JsonException exception) {
            throw new FeatureServiceException(
                $"The payload could not be deserialized to {typeof(T).Name}.",
                uri,
                statusCode: response.StatusCode,
                innerException: exception);
        }
    }

    private static bool TryParseEsriError(
        string payload,
        [NotNullWhen(true)] out EsriErrorDto? error) {
        error = null;

        try {
            var envelope = JsonSerializer.Deserialize<EsriErrorEnvelopeDto>(payload, JsonOptions);
            error = envelope?.Error;

            return error is not null;
        }
        catch (JsonException) {
            return false;
        }
    }

    private static FeatureServiceDatasetInfo MapDataset(EsriDatasetDto dto) {
        return new FeatureServiceDatasetInfo(dto.Id, dto.Name ?? $"Dataset {dto.Id}");
    }

    private static FeatureField MapField(EsriFieldDto dto) {
        return new FeatureField(
            dto.Name ?? string.Empty,
            dto.Type ?? string.Empty,
            dto.Alias,
            dto.Nullable ?? true,
            dto.Length);
    }

    private static FeatureRelationshipInfo MapRelationship(EsriRelationshipInfoDto dto) {
        return new FeatureRelationshipInfo(
            dto.Id,
            dto.Name,
            dto.RelatedTableId,
            dto.Cardinality,
            dto.Role,
            dto.KeyField,
            dto.Composite);
    }

    internal Task<EsriQueryResponseDto> QueryStatisticsAsync(
    int layerId,
    FeatureStatisticsQuery query,
    CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(query);
        query.Validate();

        var parameters = new Dictionary<string, string?> {
            ["f"] = "json",
            ["where"] = string.IsNullOrWhiteSpace(query.Where) ? "1=1" : query.Where,
            ["returnGeometry"] = "false",
            ["outStatistics"] = JsonSerializer.Serialize(
                query.Statistics.Select(statistic => new Dictionary<string, object?> {
                    ["statisticType"] = StatisticTypeMapper.ToEsriValue(statistic.StatisticType),
                    ["onStatisticField"] = statistic.OnStatisticField,
                    ["outStatisticFieldName"] = statistic.OutStatisticFieldName
                }).ToArray()),
            ["groupByFieldsForStatistics"] = query.GroupByFields is { Count: > 0 }
                ? string.Join(",", query.GroupByFields)
                : null,
            ["havingClause"] = query.HavingClause,
            ["orderByFields"] = query.OrderBy
        };

        ApplySpatialFilter(parameters, query.SpatialFilter);

        var uri = UriUtility.WithQuery(
            UriUtility.AppendPath(_serviceUri, $"{layerId.ToString(CultureInfo.InvariantCulture)}/query"),
            parameters);

        return GetAsync<EsriQueryResponseDto>(uri, cancellationToken);
    }

    internal Task<EsriRelatedRecordsResponseDto> QueryRelatedRecordsAsync(
    int layerId,
    RelatedRecordsQuery query,
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
            ["orderByFields"] = query.OrderBy,
            ["returnTrueCurves"] = _options.ReturnTrueCurves ? "true" : "false",
        };

        if (query.OutSrid.HasValue) {
            parameters["outSR"] = query.OutSrid.Value.ToString(CultureInfo.InvariantCulture);
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
            parameters["maxAllowableOffset"] = query.MaxAllowableOffset.Value.ToString(CultureInfo.InvariantCulture);
        }

        var uri = UriUtility.WithQuery(
            UriUtility.AppendPath(
                _serviceUri,
                $"{layerId.ToString(CultureInfo.InvariantCulture)}/queryRelatedRecords"),
            parameters);

        return GetAsync<EsriRelatedRecordsResponseDto>(uri, cancellationToken);
    }

    internal Task<EsriAttachmentQueryResponseDto> QueryAttachmentsAsync(
    int layerId,
    AttachmentQuery query,
    CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(query);
        query.Validate();

        var parameters = new Dictionary<string, string?> {
            ["f"] = "json",
            ["objectIds"] = query.ObjectIds is { Count: > 0 }
                ? string.Join(",", query.ObjectIds)
                : null,
            ["definitionExpression"] = query.DefinitionExpression,
            ["attachmentTypes"] = query.AttachmentTypes is { Count: > 0 }
                ? string.Join(",", query.AttachmentTypes)
                : null,
            ["keywords"] = query.Keywords is { Count: > 0 }
                ? string.Join(",", query.Keywords)
                : null,
            ["returnUrl"] = query.ReturnUrl ? "true" : null,
            ["returnMetadata"] = query.ReturnMetadata ? "true" : null
        };

        if (query.MinimumSizeBytes.HasValue || query.MaximumSizeBytes.HasValue) {
            var min = query.MinimumSizeBytes?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            var max = query.MaximumSizeBytes?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            parameters["size"] = $"{min},{max}";
        }

        var uri = UriUtility.WithQuery(
            UriUtility.AppendPath(
                _serviceUri,
                $"{layerId.ToString(CultureInfo.InvariantCulture)}/queryAttachments"),
            parameters);

        return GetAsync<EsriAttachmentQueryResponseDto>(uri, cancellationToken);
    }

    internal async Task<AttachmentContent> DownloadAttachmentAsync(
        int layerId,
        long objectId,
        long attachmentId,
        CancellationToken cancellationToken = default) {
        var uri = UriUtility.AppendPath(
            _serviceUri,
            $"{layerId.ToString(CultureInfo.InvariantCulture)}/{objectId.ToString(CultureInfo.InvariantCulture)}/attachments/{attachmentId.ToString(CultureInfo.InvariantCulture)}");

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_options.RequestTimeout);

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);

        if (_authorizer is not null) {
            await _authorizer.ApplyAsync(request, timeoutCts.Token);
        }

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            timeoutCts.Token);

        if (!response.IsSuccessStatusCode) {
            var payload = await response.Content.ReadAsStringAsync(timeoutCts.Token);

            if (!string.IsNullOrWhiteSpace(payload) && TryParseEsriError(payload, out var esriError)) {
                throw new FeatureServiceException(
                    esriError.Message ?? "The server returned an Esri error payload.",
                    uri,
                    esriError.Code,
                    esriError.Details?.ToArray(),
                    response.StatusCode);
            }

            throw new FeatureServiceException(
                $"The server returned HTTP {(int)response.StatusCode} ({response.StatusCode}).",
                uri,
                statusCode: response.StatusCode);
        }

        var bytes = await response.Content.ReadAsByteArrayAsync(timeoutCts.Token);
        var contentType = response.Content.Headers.ContentType?.MediaType;
        var fileName = GetContentDispositionFileName(response.Content.Headers);

        return new AttachmentContent(bytes, contentType, fileName);
    }

    private static string? GetContentDispositionFileName(HttpContentHeaders headers) {
        var contentDisposition = headers.ContentDisposition;

        if (contentDisposition is null) {
            return null;
        }

        var fileName = contentDisposition.FileNameStar ?? contentDisposition.FileName;
        return string.IsNullOrWhiteSpace(fileName)
            ? null
            : fileName.Trim('"');
    }

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
        parameters["outFields"] = query.OutFields is { Count: > 0 }
            ? string.Join(",", query.OutFields)
            : "*";
        parameters["returnTrueCurves"] = _options.ReturnTrueCurves ? "true" : "false";
        var uri = UriUtility.WithQuery(
            UriUtility.AppendPath(
                _serviceUri,
                $"{layerId.ToString(CultureInfo.InvariantCulture)}/queryTopFeatures"),
            parameters);

        return GetAsync<EsriQueryResponseDto>(uri, cancellationToken);
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

        var uri = UriUtility.WithQuery(
            UriUtility.AppendPath(
                _serviceUri,
                $"{layerId.ToString(CultureInfo.InvariantCulture)}/queryTopFeatures"),
            parameters);

        return GetAsync<EsriIdsResponseDto>(uri, cancellationToken);
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

        var uri = UriUtility.WithQuery(
            UriUtility.AppendPath(
                _serviceUri,
                $"{layerId.ToString(CultureInfo.InvariantCulture)}/queryTopFeatures"),
            parameters);

        var dto = await GetAsync<EsriTopFeaturesCountResponseDto>(uri, cancellationToken);

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
                new NetTopologySuite.Geometries.Envelope(
                    dto.Extent.XMin.Value,
                    dto.Extent.XMax.Value,
                    dto.Extent.YMin.Value,
                    dto.Extent.YMax.Value),
                srid);
        }

        return new TopFeaturesCountResult(dto.Count, extent);
    }
}