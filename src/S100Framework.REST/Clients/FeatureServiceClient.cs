using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using NetTopologySuite.Geometries;
using S100Framework.REST.Abstractions;
using S100Framework.REST.Configuration;
using S100Framework.REST.Exceptions;
using S100Framework.REST.Internal.Dto;
using S100Framework.REST.Internal.EsriGeometry;
using S100Framework.REST.Internal.Http;
using S100Framework.REST.Models;

namespace S100Framework.REST.Clients;

/// <summary>
/// Provides operations for reading metadata, resolving layers, executing service-level edit workflows, and calling ArcGIS Feature Service REST endpoints.
/// </summary>
public sealed partial class FeatureServiceClient : IFeatureServiceClient
{
    private static readonly JsonSerializerOptions JsonOptions = new() {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly FeatureServiceClientOptions _options;
    private readonly IFeatureServiceRequestAuthorizer? _authorizer;
    private readonly Uri _serviceUri;

    /// <summary>
    /// Initializes the client with an HTTP client and validated options.
    /// </summary>
    /// <param name="httpClient">The HTTP client used to call the feature service.</param>
    /// <param name="options">The configured client options.</param>
    [ActivatorUtilitiesConstructor]
    public FeatureServiceClient(
        HttpClient httpClient,
        FeatureServiceClientOptions options)
        : this(httpClient, options, authorizer: null) {
    }

    /// <summary>
    /// Initializes the client with an HTTP client, validated options, and an optional request authorizer.
    /// </summary>
    /// <param name="httpClient">The HTTP client used to call the feature service.</param>
    /// <param name="options">The configured client options.</param>
    /// <param name="authorizer">An optional request authorizer that can add authentication to outgoing requests.</param>
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

    /// <inheritdoc />
    public async Task<FeatureServiceMetadata> GetMetadataAsync(CancellationToken cancellationToken = default) {
        var uri = UriUtility.WithQuery(
            _serviceUri,
            new Dictionary<string, string?> {
                ["f"] = "json"
            });

        var dto = await GetAsync<EsriServiceMetadataDto>(uri, cancellationToken);

        var serviceCapabilities = ParseServiceCapabilities(
            dto.Capabilities,
            dto.SyncEnabled,
            dto.SupportsAppend,
            dto.SupportsQueryDomains,
            dto.AdvancedEditingCapabilities);

        var extractChangesCapabilities = dto.ExtractChangesCapabilities is null
            ? null
            : new ExtractChangesCapabilities(
                dto.ExtractChangesCapabilities.SupportsReturnIdsOnly ?? false,
                dto.ExtractChangesCapabilities.SupportsReturnExtentOnly ?? false,
                dto.ExtractChangesCapabilities.SupportsReturnAttachments ?? false,
                dto.ExtractChangesCapabilities.SupportsLayerQueries ?? false,
                dto.ExtractChangesCapabilities.SupportsGeometry ?? false,
                dto.ExtractChangesCapabilities.SupportsReturnFeature ?? false,
                dto.ExtractChangesCapabilities.SupportsFieldsToCompare ?? false,
                dto.ExtractChangesCapabilities.SupportsServerGens ?? false,
                dto.ExtractChangesCapabilities.SupportsReturnHasGeometryUpdates ?? false);

        var supportedAppendFormats = dto.SupportedAppendFormats?
            .Where(static format => !string.IsNullOrWhiteSpace(format))
            .ToArray();

        return new FeatureServiceMetadata(
            _serviceUri,
            dto.Layers?.Select(MapDataset).ToArray() ?? Array.Empty<FeatureServiceDatasetInfo>(),
            dto.Tables?.Select(MapDataset).ToArray() ?? Array.Empty<FeatureServiceDatasetInfo>(),
            dto.Capabilities,
            dto.MaxRecordCount,
            serviceCapabilities,
            extractChangesCapabilities,
            supportedAppendFormats);
    }
    private static FeatureServiceCapabilities ParseServiceCapabilities(
    string? capabilities,
    bool? syncEnabled,
    bool? supportsAppend,
    bool? supportsQueryDomains,
    EsriAdvancedEditingCapabilitiesDto? advancedEditingCapabilities) {
        var values = (capabilities ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new FeatureServiceCapabilities(
            SupportsQuery: values.Contains("Query"),
            SupportsCreate: values.Contains("Create"),
            SupportsUpdate: values.Contains("Update"),
            SupportsDelete: values.Contains("Delete"),
            SupportsEditing: values.Contains("Editing"),
            SupportsUploads: values.Contains("Uploads"),
            SupportsSync: values.Contains("Sync"),
            SupportsChangeTracking: values.Contains("ChangeTracking"),
            SyncEnabled: syncEnabled ?? false,
            SupportsAsyncApplyEdits: advancedEditingCapabilities?.SupportsAsyncApplyEdits ?? false,
            SupportsAppend: supportsAppend ?? false,
            SupportsQueryDomains: supportsQueryDomains ?? false);
    }

    /// <inheritdoc />
    public IFeatureLayerClient GetLayerClient(int layerId) {
        if (layerId < 0) {
            throw new ArgumentOutOfRangeException(nameof(layerId));
        }

        return new FeatureLayerClient(this, layerId);
    }

    /// <inheritdoc />
    public async Task<IFeatureLayerClient> GetLayerClientAsync(
    string layerName,
    CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(layerName)) {
            throw new ArgumentException("Layer name must be provided.", nameof(layerName));
        }

        var metadata = await GetMetadataAsync(cancellationToken);

        var matches = metadata.Layers
            .Concat(metadata.Tables)
            .Where(dataset => string.Equals(dataset.Name, layerName, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        return matches.Length switch {
            0 => throw new InvalidOperationException(
                $"No layer or table named '{layerName}' was found in the feature service."),
            > 1 => throw new InvalidOperationException(
                $"Multiple layers or tables named '{layerName}' were found in the feature service. Use the layer ID instead."),
            _ => GetLayerClient(matches[0].Id)
        };
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

        var supportsPagination = dto.AdvancedQueryCapabilities?.SupportsPagination ?? false;

        var uniqueIdInfo = dto.UniqueIdInfo is null
            ? null
            : new FeatureLayerUniqueIdInfo(
                dto.UniqueIdInfo.Type ?? "unknown",
                dto.UniqueIdInfo.Fields?
                    .Where(static field => !string.IsNullOrWhiteSpace(field))
                    .ToArray() ?? Array.Empty<string>(),
                dto.UniqueIdInfo.OidFieldContainsHashValue ?? false);

        return new FeatureLayerSchema(
            dto.Id,
            dto.Name ?? $"Layer {dto.Id}",
            dto.GeometryType,
            srid,
            dto.HasZ ?? false,
            dto.HasM ?? false,
            dto.MaxRecordCount,
            dto.ObjectIdField,
            dto.Fields?.Select(MapField).ToArray() ?? Array.Empty<FeatureField>(),
            new FeatureLayerCapabilities(
    dto.HasAttachments ?? false,
    dto.SupportsQueryAttachments ?? false,
    dto.SupportsAttachmentsResizing ?? false,
    dto.SupportsTopFeaturesQuery ?? false,
    supportsPagination,
    dto.AdvancedQueryCapabilities?.SupportsPaginationOnAggregatedQueries ?? false,
    dto.AdvancedQueryCapabilities?.SupportsQueryRelatedPagination ?? false,
    dto.AdvancedQueryCapabilities?.SupportsAdvancedQueryRelated ?? false,
    dto.AdvancedQueryCapabilities?.SupportsOrderBy ?? false,
    dto.AdvancedQueryCapabilities?.SupportsDistinct ?? false,
    dto.AdvancedEditingCapabilities?.SupportsAsyncApplyEdits ?? false,
    dto.AdvancedQueryCapabilities?.SupportsReturningGeometryEnvelope ?? false,
    dto.AdvancedQueryCapabilities?.SupportsFullTextSearch ?? false,
    dto.AdvancedQueryCapabilities?.SupportsPercentileStatistics ?? false,
    dto.SupportsAppend ?? false,
    dto.AdvancedQueryCapabilities?.SupportsQueryDateBins ?? false),
dto.Relationships?.Select(MapRelationship).ToArray() ?? Array.Empty<FeatureRelationshipInfo>()) {
            UniqueIdInfo = uniqueIdInfo,
            SupportedAppendFormats = dto.SupportedAppendFormats?
        .Where(static value => !string.IsNullOrWhiteSpace(value))
        .ToArray() ?? Array.Empty<string>(),
            SupportedAppendSourceFilterFormats = dto.SupportedAppendSourceFilterFormats?
        .Where(static value => !string.IsNullOrWhiteSpace(value))
        .ToArray() ?? Array.Empty<string>(),
            SupportedAppendCapabilities = dto.SupportedAppendCapabilities
        };
    }

    internal Task<EsriQueryResponseDto> QueryFeaturesAsync(
    int layerId,
    FeatureQuery query,
    int? resultOffset,
    int? resultRecordCount,
    IReadOnlyList<long>? objectIds,
    IReadOnlyList<FeatureUniqueId>? uniqueIds = null,
    CancellationToken cancellationToken = default) {
        static string SerializeUniqueIds(IReadOnlyList<FeatureUniqueId> values) {
            ArgumentNullException.ThrowIfNull(values);

            if (values.Count == 0) {
                throw new InvalidOperationException("UniqueIds must contain at least one identifier when provided.");
            }

            var payload = new List<object>(values.Count);

            foreach (var value in values) {
                ArgumentNullException.ThrowIfNull(value);

                if (value.Components is not { Count: > 0 }) {
                    throw new InvalidOperationException("Each UniqueIds entry must contain at least one component.");
                }

                if (value.Components.Any(string.IsNullOrWhiteSpace)) {
                    throw new InvalidOperationException("UniqueIds components must not be empty.");
                }

                payload.Add(value.Components.Count == 1
                    ? value.Components[0]
                    : value.Components.ToArray());
            }

            return JsonSerializer.Serialize(payload);
        }

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

        if (objectIds is { Count: > 0 }) {
            parameters.Remove("where");
            parameters.Remove("uniqueIds");
            parameters["objectIds"] = string.Join(",", objectIds);
        }
        else if (uniqueIds is { Count: > 0 }) {
            parameters["uniqueIds"] = SerializeUniqueIds(uniqueIds);
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

    internal Task<EsriQueryResponseDto> QueryStatisticsAsync(
    int layerId,
    FeatureStatisticsQuery query,
    CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(query);
        query.Validate();

        static string MapStatisticType(StatisticType value) {
            return value switch {
                StatisticType.Count => "count",
                StatisticType.Sum => "sum",
                StatisticType.Min => "min",
                StatisticType.Max => "max",
                StatisticType.Average => "avg",
                StatisticType.StandardDeviation => "stddev",
                StatisticType.Variance => "var",
                StatisticType.PercentileContinuous => "PERCENTILE_CONT",
                StatisticType.PercentileDiscrete => "PERCENTILE_DISC",
                _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
            };
        }

        static string MapPercentileOrder(StatisticPercentileOrder value) {
            return value switch {
                StatisticPercentileOrder.Asc => "ASC",
                StatisticPercentileOrder.Desc => "DESC",
                _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
            };
        }

        var parameters = new Dictionary<string, string?> {
            ["f"] = "json",
            ["where"] = string.IsNullOrWhiteSpace(query.Where) ? "1=1" : query.Where,
            ["returnGeometry"] = "false",
            ["outStatistics"] = JsonSerializer.Serialize(
                query.Statistics.Select(statistic => {
                    var payload = new Dictionary<string, object?> {
                        ["statisticType"] = MapStatisticType(statistic.StatisticType),
                        ["onStatisticField"] = statistic.OnStatisticField,
                        ["outStatisticFieldName"] = statistic.OutStatisticFieldName
                    };

                    if (statistic.PercentileParameters is not null) {
                        payload["statisticParameters"] = new Dictionary<string, object?> {
                            ["value"] = statistic.PercentileParameters.Value,
                            ["orderBy"] = MapPercentileOrder(statistic.PercentileParameters.OrderBy)
                        };
                    }

                    return payload;
                }).ToArray()),
            ["groupByFieldsForStatistics"] = query.GroupByFields is { Count: > 0 }
                ? string.Join(",", query.GroupByFields)
                : null,
            ["havingClause"] = query.HavingClause,
            ["orderByFields"] = query.OrderBy
        };

        if (query.ResultOffset.HasValue) {
            parameters["resultOffset"] = query.ResultOffset.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (query.ResultRecordCount.HasValue) {
            parameters["resultRecordCount"] = query.ResultRecordCount.Value.ToString(CultureInfo.InvariantCulture);
        }

        ApplySpatialFilter(parameters, query.SpatialFilter);

        return SendLayerQueryAsync<EsriQueryResponseDto>(
            $"{layerId.ToString(CultureInfo.InvariantCulture)}/query",
            parameters,
            cancellationToken);
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
            ["returnTrueCurves"] = _options.ReturnTrueCurves ? "true" : "false",
            ["orderByFields"] = query.OrderBy
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
                new Envelope(
                    dto.Extent.XMin.Value,
                    dto.Extent.XMax.Value,
                    dto.Extent.YMin.Value,
                    dto.Extent.YMax.Value),
                srid);
        }

        return new TopFeaturesCountResult(dto.Count, extent);
    }

    internal FeatureServiceClientOptions Options => _options;

    private Task<T> SendLayerQueryAsync<T>(
        string relativePath,
        IReadOnlyDictionary<string, string?> parameters,
        CancellationToken cancellationToken) {
        var endpointUri = UriUtility.AppendPath(_serviceUri, relativePath);
        var method = ResolveLayerQueryMethod(endpointUri, parameters);

        return method == HttpMethod.Post
            ? PostFormAsync<T>(endpointUri, parameters, cancellationToken)
            : GetAsync<T>(UriUtility.WithQuery(endpointUri, parameters), cancellationToken);
    }

    private HttpMethod ResolveLayerQueryMethod(
        Uri endpointUri,
        IReadOnlyDictionary<string, string?> parameters) {
        return _options.QueryRequestMethodPreference switch {
            QueryRequestMethodPreference.Get => HttpMethod.Get,
            QueryRequestMethodPreference.Post => HttpMethod.Post,
            QueryRequestMethodPreference.Auto => ShouldUsePostForLayerQuery(endpointUri, parameters)
                ? HttpMethod.Post
                : HttpMethod.Get,
            _ => throw new ArgumentOutOfRangeException(
                nameof(_options.QueryRequestMethodPreference),
                _options.QueryRequestMethodPreference,
                null)
        };
    }

    private bool ShouldUsePostForLayerQuery(
        Uri endpointUri,
        IReadOnlyDictionary<string, string?> parameters) {
        var getUri = UriUtility.WithQuery(endpointUri, parameters);
        return getUri.AbsoluteUri.Length >= _options.AutoPostQueryLengthThreshold;
    }

    private async Task<T> PostFormAsync<T>(
        Uri endpointUri,
        IReadOnlyDictionary<string, string?> parameters,
        CancellationToken cancellationToken) {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_options.RequestTimeout);

        using var request = new HttpRequestMessage(HttpMethod.Post, endpointUri) {
            Content = new FormUrlEncodedContent(BuildFormParameters(parameters))
        };

        return await SendAsync<T>(request, endpointUri, timeoutCts.Token);
    }

    private static IEnumerable<KeyValuePair<string, string>> BuildFormParameters(
        IReadOnlyDictionary<string, string?> parameters) {
        foreach (var pair in parameters) {
            if (string.IsNullOrWhiteSpace(pair.Value)) {
                continue;
            }

            yield return new KeyValuePair<string, string>(pair.Key, pair.Value);
        }
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

        static string SerializeUniqueIds(IReadOnlyList<FeatureUniqueId> uniqueIds) {
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
            parameters["uniqueIds"] = SerializeUniqueIds(query.UniqueIds);
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

        return await SendAsync<T>(request, uri, timeoutCts.Token);
    }

    private async Task<T> SendAsync<T>(
        HttpRequestMessage request,
        Uri requestUri,
        CancellationToken cancellationToken) {
        if (_authorizer is not null) {
            await _authorizer.ApplyAsync(request, cancellationToken);
        }

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        var payload = await response.Content.ReadAsStringAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(payload)) {
            throw new FeatureServiceException(
                "The server returned an empty payload.",
                requestUri,
                statusCode: response.StatusCode);
        }

        if (TryParseEsriError(payload, out var esriError)) {
            throw new FeatureServiceException(
                esriError.Message ?? "The server returned an Esri error payload.",
                requestUri,
                esriError.Code,
                esriError.Details?.ToArray(),
                response.StatusCode);
        }

        if (!response.IsSuccessStatusCode) {
            throw new FeatureServiceException(
                $"The server returned HTTP {(int)response.StatusCode} ({response.StatusCode}).",
                requestUri,
                statusCode: response.StatusCode);
        }

        try {
            var result = JsonSerializer.Deserialize<T>(payload, JsonOptions);

            return result ?? throw new FeatureServiceException(
                $"The payload could not be deserialized to {typeof(T).Name}.",
                requestUri,
                statusCode: response.StatusCode);
        }
        catch (JsonException exception) {
            throw new FeatureServiceException(
                $"The payload could not be deserialized to {typeof(T).Name}.",
                requestUri,
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

    internal async Task<ApplyEditsResult> WaitForLayerApplyEditsCompletionAsync(
    int layerId,
    FeatureEdits edits,
    ApplyEditsWaitOptions? options = null,
    CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(edits);

        var submission = await SubmitApplyEditsAsync(layerId, edits, cancellationToken);

        if (submission.Result is not null) {
            return submission.Result;
        }

        if (submission.StatusUrl is null) {
            throw new FeatureServiceException(
                "The applyEdits submission did not return a statusUrl or a result payload.",
                UriUtility.AppendPath(
                    _serviceUri,
                    $"{layerId.ToString(CultureInfo.InvariantCulture)}/applyEdits"));
        }

        return await WaitForLayerApplyEditsCompletionAsync(
            submission.StatusUrl,
            options,
            cancellationToken);
    }

    internal Task<ApplyEditsResult> WaitForLayerApplyEditsCompletionAsync(
        Uri statusUrl,
        ApplyEditsWaitOptions? options = null,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(statusUrl);

        return WaitForApplyEditsJobCompletionAsync(
            statusUrl,
            options,
            GetLayerApplyEditsStatusAsync,
            GetLayerApplyEditsResultAsync,
            cancellationToken);
    }

    internal async Task<ApplyEditsResult> ApplyEditsAsync(
    int layerId,
    FeatureEdits edits,
    CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(edits);
        edits.Validate();

        var endpointUri = UriUtility.AppendPath(
            _serviceUri,
            $"{layerId.ToString(CultureInfo.InvariantCulture)}/applyEdits");

        var dto = await PostFormAsync<EsriApplyEditsResponseDto>(
            endpointUri,
            BuildLayerApplyEditsParameters(edits, applyAsync: false),
            cancellationToken);

        return MapApplyEditsResult(dto);
    }

    internal async Task<ApplyEditsSubmissionResult> SubmitApplyEditsAsync(
        int layerId,
        FeatureEdits edits,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(edits);
        edits.Validate();

        var endpointUri = UriUtility.AppendPath(
            _serviceUri,
            $"{layerId.ToString(CultureInfo.InvariantCulture)}/applyEdits");

        var document = await PostFormAsync<JsonDocument>(
            endpointUri,
            BuildLayerApplyEditsParameters(edits, applyAsync: true),
            cancellationToken);

        var root = document.RootElement;

        if (root.TryGetProperty("statusUrl", out var statusUrlElement)) {
            var rawStatusUrl = statusUrlElement.GetString();

            if (string.IsNullOrWhiteSpace(rawStatusUrl)) {
                throw new FeatureServiceException(
                    "The server returned an empty statusUrl for applyEdits.",
                    endpointUri);
            }

            return new ApplyEditsSubmissionResult(
                Result: null,
                StatusUrl: new Uri(rawStatusUrl, UriKind.Absolute));
        }

        return new ApplyEditsSubmissionResult(
            Result: MapApplyEditsResult(root, endpointUri),
            StatusUrl: null);
    }

    internal async Task<ApplyEditsJobStatus> GetLayerApplyEditsStatusAsync(
        Uri statusUrl,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(statusUrl);

        var document = await GetAsync<JsonDocument>(statusUrl, cancellationToken);
        var root = document.RootElement;

        return new ApplyEditsJobStatus(
            Status: root.TryGetProperty("status", out var statusElement)
                ? statusElement.GetString() ?? "Unknown"
                : "Unknown",
            ResultUrl: root.TryGetProperty("resultUrl", out var resultUrlElement) &&
                       !string.IsNullOrWhiteSpace(resultUrlElement.GetString())
                ? new Uri(resultUrlElement.GetString()!, UriKind.Absolute)
                : null,
            SubmissionTime: root.TryGetProperty("submissionTime", out var submissionTimeElement) &&
                            submissionTimeElement.TryGetInt64(out var submissionTime)
                ? submissionTime
                : null,
            LastUpdatedTime: root.TryGetProperty("lastUpdatedTime", out var lastUpdatedTimeElement) &&
                             lastUpdatedTimeElement.TryGetInt64(out var lastUpdatedTime)
                ? lastUpdatedTime
                : null);
    }

    internal async Task<ApplyEditsResult> GetLayerApplyEditsResultAsync(
        Uri resultUrl,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(resultUrl);

        var document = await GetAsync<JsonDocument>(resultUrl, cancellationToken);

        return MapApplyEditsResult(document.RootElement, resultUrl);
    }

    private static Dictionary<string, string?> BuildLayerApplyEditsParameters(
        FeatureEdits edits,
        bool applyAsync) {
        return new Dictionary<string, string?> {
            ["f"] = "json",
            ["rollbackOnFailure"] = edits.RollbackOnFailure ? "true" : "false",
            ["useGlobalIds"] = edits.UseGlobalIds ? "true" : "false",
            ["async"] = applyAsync ? "true" : null,
            ["adds"] = edits.Adds is { Count: > 0 }
                ? EsriEditGeometryWriter.WriteFeatures(edits.Adds)
                : null,
            ["updates"] = edits.Updates is { Count: > 0 }
                ? EsriEditGeometryWriter.WriteFeatures(edits.Updates)
                : null,
            ["deletes"] = edits.Deletes is { Count: > 0 }
                ? string.Join(",", edits.Deletes)
                : null
        };
    }

    private async Task<TResult> WaitForApplyEditsJobCompletionAsync<TResult>(
    Uri statusUrl,
    ApplyEditsWaitOptions? options,
    Func<Uri, CancellationToken, Task<ApplyEditsJobStatus>> getStatusAsync,
    Func<Uri, CancellationToken, Task<TResult>> getResultAsync,
    CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(statusUrl);
        ArgumentNullException.ThrowIfNull(getStatusAsync);
        ArgumentNullException.ThrowIfNull(getResultAsync);

        var effectiveOptions = GetValidatedApplyEditsWaitOptions(options);
        var startedAt = DateTimeOffset.UtcNow;

        while (true) {
            cancellationToken.ThrowIfCancellationRequested();

            var status = await getStatusAsync(statusUrl, cancellationToken);

            if (status.IsCompleted) {
                if (status.ResultUrl is null) {
                    throw new FeatureServiceException(
                        "The applyEdits job completed without a resultUrl.",
                        statusUrl);
                }

                return await getResultAsync(status.ResultUrl, cancellationToken);
            }

            if (status.IsTerminal) {
                throw new FeatureServiceException(
                    $"The applyEdits job ended with terminal status '{status.Status}'.",
                    statusUrl);
            }

            if (effectiveOptions.Timeout is { } timeout &&
                DateTimeOffset.UtcNow - startedAt >= timeout) {
                throw new TimeoutException(
                    $"The applyEdits job did not complete within {timeout}.");
            }

            if (effectiveOptions.PollInterval > TimeSpan.Zero) {
                await Task.Delay(effectiveOptions.PollInterval, cancellationToken);
            }
            else {
                await Task.Yield();
            }
        }
    }

    private static ApplyEditsWaitOptions GetValidatedApplyEditsWaitOptions(
        ApplyEditsWaitOptions? options) {
        var effectiveOptions = options ?? new ApplyEditsWaitOptions();

        if (effectiveOptions.PollInterval < TimeSpan.Zero) {
            throw new ArgumentOutOfRangeException(
                nameof(ApplyEditsWaitOptions.PollInterval),
                "PollInterval cannot be negative.");
        }

        if (effectiveOptions.Timeout is { } timeout && timeout < TimeSpan.Zero) {
            throw new ArgumentOutOfRangeException(
                nameof(ApplyEditsWaitOptions.Timeout),
                "Timeout cannot be negative.");
        }

        return effectiveOptions;
    }

    private static ApplyEditsResult MapApplyEditsResult(
        JsonElement root,
        Uri endpointUri) {
        var dto = root.Deserialize<EsriApplyEditsResponseDto>(JsonOptions)
            ?? throw new FeatureServiceException(
                "The applyEdits payload could not be deserialized.",
                endpointUri);

        return MapApplyEditsResult(dto);
    }

    private static ApplyEditsResult MapApplyEditsResult(
        EsriApplyEditsResponseDto dto) {
        return new ApplyEditsResult(
            dto.AddResults?.Select(MapEditResult).ToArray() ?? Array.Empty<EditResult>(),
            dto.UpdateResults?.Select(MapEditResult).ToArray() ?? Array.Empty<EditResult>(),
            dto.DeleteResults?.Select(MapEditResult).ToArray() ?? Array.Empty<EditResult>());
    }

    private static EditResult MapEditResult(EsriEditResultDto dto) {
        return new EditResult(
            dto.Success,
            dto.ObjectId,
            dto.GlobalId,
            dto.Error?.Code,
            dto.Error?.Description);
    }

    /// <inheritdoc />
    public async Task<FeatureServiceApplyEditsResult> ApplyEditsAsync(
    FeatureServiceEdits edits,
    CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(edits);
        edits.Validate();

        var endpointUri = UriUtility.AppendPath(_serviceUri, "applyEdits");
        var dto = await PostFormAsync<List<EsriServiceLayerEditResultsDto>>(
            endpointUri,
            BuildServiceApplyEditsParameters(edits, applyAsync: false),
            cancellationToken);

        return MapServiceApplyEditsResult(dto);
    }

    /// <inheritdoc />
    public async Task<FeatureServiceApplyEditsSubmissionResult> SubmitApplyEditsAsync(
        FeatureServiceEdits edits,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(edits);
        edits.Validate();

        var metadata = await GetMetadataAsync(cancellationToken);

        if (!metadata.Capabilities.SupportsAsyncApplyEdits) {
            throw new FeatureServiceCapabilityException(
                "The feature service does not support asynchronous applyEdits.");
        }

        var endpointUri = UriUtility.AppendPath(_serviceUri, "applyEdits");
        var document = await PostFormAsync<JsonDocument>(
            endpointUri,
            BuildServiceApplyEditsParameters(edits, applyAsync: true),
            cancellationToken);

        var root = document.RootElement;

        if (root.ValueKind == JsonValueKind.Array) {
            return new FeatureServiceApplyEditsSubmissionResult(
                Result: MapServiceApplyEditsResult(root, endpointUri),
                StatusUrl: null);
        }

        if (root.ValueKind != JsonValueKind.Object) {
            throw new FeatureServiceException(
                "The applyEdits payload had an unexpected JSON shape.",
                endpointUri);
        }

        if (root.TryGetProperty("statusUrl", out var statusUrlElement)) {
            var rawStatusUrl = statusUrlElement.GetString();

            if (string.IsNullOrWhiteSpace(rawStatusUrl)) {
                throw new FeatureServiceException(
                    "The server returned an empty statusUrl for applyEdits.",
                    endpointUri);
            }

            return new FeatureServiceApplyEditsSubmissionResult(
                Result: null,
                StatusUrl: new Uri(rawStatusUrl, UriKind.Absolute));
        }

        return new FeatureServiceApplyEditsSubmissionResult(
            Result: MapServiceApplyEditsResult(root, endpointUri),
            StatusUrl: null);
    }

    /// <inheritdoc />
    public async Task<ApplyEditsJobStatus> GetApplyEditsStatusAsync(
        Uri statusUrl,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(statusUrl);

        var document = await GetAsync<JsonDocument>(statusUrl, cancellationToken);
        var root = document.RootElement;

        return new ApplyEditsJobStatus(
            Status: root.TryGetProperty("status", out var statusElement)
                ? statusElement.GetString() ?? "Unknown"
                : "Unknown",
            ResultUrl: root.TryGetProperty("resultUrl", out var resultUrlElement) &&
                       !string.IsNullOrWhiteSpace(resultUrlElement.GetString())
                ? new Uri(resultUrlElement.GetString()!, UriKind.Absolute)
                : null,
            SubmissionTime: root.TryGetProperty("submissionTime", out var submissionTimeElement) &&
                            submissionTimeElement.TryGetInt64(out var submissionTime)
                ? submissionTime
                : null,
            LastUpdatedTime: root.TryGetProperty("lastUpdatedTime", out var lastUpdatedTimeElement) &&
                             lastUpdatedTimeElement.TryGetInt64(out var lastUpdatedTime)
                ? lastUpdatedTime
                : null);
    }

    /// <inheritdoc />
    public async Task<FeatureServiceApplyEditsResult> GetApplyEditsResultAsync(
        Uri resultUrl,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(resultUrl);

        var document = await GetAsync<JsonDocument>(resultUrl, cancellationToken);

        return MapServiceApplyEditsResult(document.RootElement, resultUrl);
    }

    private static Dictionary<string, string?> BuildServiceApplyEditsParameters(
        FeatureServiceEdits edits,
        bool applyAsync) {
        return new Dictionary<string, string?> {
            ["f"] = "json",
            ["rollbackOnFailure"] = edits.RollbackOnFailure ? "true" : "false",
            ["useGlobalIds"] = edits.UseGlobalIds ? "true" : "false",
            ["async"] = applyAsync ? "true" : null,
            ["edits"] = SerializeServiceEdits(edits)
        };
    }

    private static FeatureServiceApplyEditsResult MapServiceApplyEditsResult(
        JsonElement root,
        Uri endpointUri) {
        var dto = root.Deserialize<List<EsriServiceLayerEditResultsDto>>(JsonOptions)
            ?? throw new FeatureServiceException(
                "The applyEdits payload could not be deserialized.",
                endpointUri);

        return MapServiceApplyEditsResult(dto);
    }

    private static FeatureServiceApplyEditsResult MapServiceApplyEditsResult(
        IReadOnlyList<EsriServiceLayerEditResultsDto>? dto) {
        return new FeatureServiceApplyEditsResult(
            dto?.Select(MapServiceLayerEditResults).ToArray() ?? Array.Empty<ServiceLayerEditResults>());
    }

    private static string SerializeServiceEdits(FeatureServiceEdits edits) {
        var payload = edits.Layers
            .Select(layer => SerializeServiceLayerEdits(layer, edits.UseGlobalIds))
            .ToArray();

        return JsonSerializer.Serialize(payload);
    }

    private static Dictionary<string, object?> SerializeServiceLayerEdits(
        ServiceLayerEdits layer,
        bool useGlobalIds) {
        var payload = new Dictionary<string, object?> {
            ["id"] = layer.LayerId
        };

        if (layer.Adds is { Count: > 0 }) {
            payload["adds"] = JsonSerializer.Deserialize<JsonElement>(
                EsriEditGeometryWriter.WriteFeatures(layer.Adds));
        }

        if (layer.Updates is { Count: > 0 }) {
            payload["updates"] = JsonSerializer.Deserialize<JsonElement>(
                EsriEditGeometryWriter.WriteFeatures(layer.Updates));
        }

        if (useGlobalIds) {
            if (layer.DeleteGlobalIds is { Count: > 0 }) {
                payload["deletes"] = layer.DeleteGlobalIds;
            }
        }
        else {
            if (layer.DeleteObjectIds is { Count: > 0 }) {
                payload["deletes"] = layer.DeleteObjectIds;
            }
        }

        return payload;
    }

    private static ServiceLayerEditResults MapServiceLayerEditResults(EsriServiceLayerEditResultsDto dto) {
        return new ServiceLayerEditResults(
            dto.Id,
            dto.AddResults?.Select(MapEditResult).ToArray() ?? Array.Empty<EditResult>(),
            dto.UpdateResults?.Select(MapEditResult).ToArray() ?? Array.Empty<EditResult>(),
            dto.DeleteResults?.Select(MapEditResult).ToArray() ?? Array.Empty<EditResult>());
    }

    /// <inheritdoc />
    public async Task<FeatureServiceApplyEditsResult> WaitForApplyEditsCompletionAsync(
    FeatureServiceEdits edits,
    ApplyEditsWaitOptions? options = null,
    CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(edits);

        var submission = await SubmitApplyEditsAsync(edits, cancellationToken);

        if (submission.Result is not null) {
            return submission.Result;
        }

        if (submission.StatusUrl is null) {
            throw new FeatureServiceException(
                "The applyEdits submission did not return a statusUrl or a result payload.",
                UriUtility.AppendPath(_serviceUri, "applyEdits"));
        }

        return await WaitForApplyEditsCompletionAsync(
            submission.StatusUrl,
            options,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<FeatureServiceApplyEditsResult> WaitForApplyEditsCompletionAsync(
        Uri statusUrl,
        ApplyEditsWaitOptions? options = null,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(statusUrl);

        return WaitForApplyEditsJobCompletionAsync(
            statusUrl,
            options,
            GetApplyEditsStatusAsync,
            GetApplyEditsResultAsync,
            cancellationToken);
    }

    internal async Task<DeleteAttachmentsResult> DeleteAttachmentsAsync(
    int layerId,
    DeleteAttachmentsRequest request,
    CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();

        var parameters = new Dictionary<string, string?> {
            ["f"] = "json",
            ["attachmentIds"] = string.Join(",", request.AttachmentIds),
            ["rollbackOnFailure"] = request.RollbackOnFailure ? "true" : "false",
            ["returnEditMoment"] = request.ReturnEditMoment ? "true" : "false",
            ["gdbVersion"] = request.GdbVersion
        };

        var endpointUri = UriUtility.AppendPath(
            _serviceUri,
            $"{layerId.ToString(CultureInfo.InvariantCulture)}/{request.ObjectId.ToString(CultureInfo.InvariantCulture)}/deleteAttachments");

        var dto = await PostFormAsync<EsriDeleteAttachmentsResponseDto>(
            endpointUri,
            parameters,
            cancellationToken);

        return new DeleteAttachmentsResult(
            dto.DeleteAttachmentResults?.Select(MapAttachmentEditResult).ToArray() ?? Array.Empty<AttachmentEditResult>(),
            dto.EditMoment);
    }

    private static AttachmentEditResult MapAttachmentEditResult(EsriAttachmentEditResultDto dto) {
        return new AttachmentEditResult(
            dto.Success,
            dto.ObjectId,
            dto.GlobalId,
            dto.Error?.Code,
            dto.Error?.Description);
    }

    internal async Task<AddAttachmentResult> AddAttachmentAsync(
    int layerId,
    AddAttachmentRequest request,
    CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();

        var endpointUri = UriUtility.AppendPath(
            _serviceUri,
            $"{layerId.ToString(CultureInfo.InvariantCulture)}/{request.ObjectId.ToString(CultureInfo.InvariantCulture)}/addAttachment");

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_options.RequestTimeout);

        using var multipartContent = new MultipartFormDataContent();

        multipartContent.Add(
            new StringContent("json"),
            "f");

        if (!string.IsNullOrWhiteSpace(request.GdbVersion)) {
            multipartContent.Add(
                new StringContent(request.GdbVersion),
                "gdbVersion");
        }

        if (request.ReturnEditMoment) {
            multipartContent.Add(
                new StringContent("true"),
                "returnEditMoment");
        }

        if (!string.IsNullOrWhiteSpace(request.Keywords)) {
            multipartContent.Add(
                new StringContent(request.Keywords),
                "keywords");
        }

        var attachmentContent = new StreamContent(request.Content);

        if (!string.IsNullOrWhiteSpace(request.ContentType)) {
            attachmentContent.Headers.ContentType = new MediaTypeHeaderValue(request.ContentType);
        }

        multipartContent.Add(
            attachmentContent,
            "attachment",
            request.FileName);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpointUri) {
            Content = multipartContent
        };

        var dto = await SendAsync<EsriAddAttachmentResponseDto>(
            httpRequest,
            endpointUri,
            timeoutCts.Token);

        var resultDto = dto.AddAttachmentResult
            ?? throw new FeatureServiceException(
                "The server did not return an addAttachmentResult payload.",
                endpointUri);

        return new AddAttachmentResult(
            MapAttachmentEditResult(resultDto),
            dto.EditMoment);
    }

    internal async Task<UpdateAttachmentResult> UpdateAttachmentAsync(
    int layerId,
    UpdateAttachmentRequest request,
    CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();

        var endpointUri = UriUtility.AppendPath(
            _serviceUri,
            $"{layerId.ToString(CultureInfo.InvariantCulture)}/" +
            $"{request.ObjectId.ToString(CultureInfo.InvariantCulture)}/attachments/" +
            $"{request.AttachmentId.ToString(CultureInfo.InvariantCulture)}/update");

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_options.RequestTimeout);

        using var multipartContent = new MultipartFormDataContent();

        multipartContent.Add(
            new StringContent("json"),
            "f");

        if (!string.IsNullOrWhiteSpace(request.GdbVersion)) {
            multipartContent.Add(
                new StringContent(request.GdbVersion),
                "gdbVersion");
        }

        if (request.ReturnEditMoment) {
            multipartContent.Add(
                new StringContent("true"),
                "returnEditMoment");
        }

        if (!string.IsNullOrWhiteSpace(request.Keywords)) {
            multipartContent.Add(
                new StringContent(request.Keywords),
                "keywords");
        }

        var attachmentContent = new StreamContent(request.Content);

        if (!string.IsNullOrWhiteSpace(request.ContentType)) {
            attachmentContent.Headers.ContentType = new MediaTypeHeaderValue(request.ContentType);
        }

        multipartContent.Add(
            attachmentContent,
            "attachment",
            request.FileName);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpointUri) {
            Content = multipartContent
        };

        var dto = await SendAsync<EsriUpdateAttachmentResponseDto>(
            httpRequest,
            endpointUri,
            timeoutCts.Token);

        var resultDto = dto.UpdateAttachmentResults?.SingleOrDefault()
            ?? throw new FeatureServiceException(
                "The server did not return exactly one updateAttachmentResults entry.",
                endpointUri);

        return new UpdateAttachmentResult(
            MapAttachmentEditResult(resultDto),
            dto.EditMoment);
    }

    /// <inheritdoc />
    public async Task<ExtractChangesResult> ExtractChangesAsync(
        ExtractChangesRequest request,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();

        var metadata = await GetMetadataAsync(cancellationToken);
        EnsureExtractChangesSupported(metadata, request);

        var submission = await SubmitExtractChangesAsync(request, cancellationToken);

        if (submission.StatusUrl is not null) {
            throw new InvalidOperationException(
                "The server returned an asynchronous extractChanges response. Use GetExtractChangesStatusAsync to poll the job.");
        }

        return submission.Result
            ?? throw new InvalidOperationException(
                "The extractChanges request did not return an embedded result.");
    }

    /// <inheritdoc />
    public async Task<ExtractChangesSubmissionResult> SubmitExtractChangesAsync(
    ExtractChangesRequest request,
    CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();

        var metadata = await GetMetadataAsync(cancellationToken);
        EnsureExtractChangesSupported(metadata, request);

        var parameters = BuildExtractChangesParameters(request);
        var endpointUri = UriUtility.AppendPath(_serviceUri, "extractChanges");

        var document = await PostFormAsync<JsonDocument>(endpointUri, parameters, cancellationToken);
        var root = document.RootElement;

        if (root.TryGetProperty("statusUrl", out var statusUrlElement)) {
            var rawStatusUrl = statusUrlElement.GetString();

            if (string.IsNullOrWhiteSpace(rawStatusUrl)) {
                throw new FeatureServiceException(
                    "The server returned an empty statusUrl for extractChanges.",
                    endpointUri);
            }

            return new ExtractChangesSubmissionResult(
                Result: null,
                StatusUrl: new Uri(rawStatusUrl, UriKind.Absolute));
        }

        var result = await MapExtractChangesResultAsync(root, cancellationToken);

        return new ExtractChangesSubmissionResult(
            Result: result,
            StatusUrl: null);
    }

    /// <inheritdoc />
    public async Task<ExtractChangesFileResult> DownloadExtractChangesFileAsync(
    Uri resultUrl,
    CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(resultUrl);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_options.RequestTimeout);

        using var request = new HttpRequestMessage(HttpMethod.Get, resultUrl);

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
                    resultUrl,
                    esriError.Code,
                    esriError.Details?.ToArray(),
                    response.StatusCode);
            }

            throw new FeatureServiceException(
                $"The server returned HTTP {(int)response.StatusCode} ({response.StatusCode}).",
                resultUrl,
                statusCode: response.StatusCode);
        }

        var bytes = await response.Content.ReadAsByteArrayAsync(timeoutCts.Token);
        var contentType = response.Content.Headers.ContentType?.MediaType;
        var fileName = GetContentDispositionFileName(response.Content.Headers);

        return new ExtractChangesFileResult(bytes, contentType, fileName, resultUrl);
    }

    private Dictionary<string, string?> BuildExtractChangesParameters(
    ExtractChangesRequest request) {
        var parameters = new Dictionary<string, string?> {
            ["f"] = "json",
            ["layers"] = JsonSerializer.Serialize(request.Layers),
            ["returnInserts"] = request.ReturnInserts ? "true" : "false",
            ["returnUpdates"] = request.ReturnUpdates ? "true" : "false",
            ["returnDeletes"] = request.ReturnDeletes ? "true" : "false",
            ["returnIdsOnly"] = request.ReturnIdsOnly ? "true" : "false",
            ["returnHasGeometryUpdates"] = request.ReturnHasGeometryUpdates ? "true" : "false",
            ["returnDeletedFeatures"] = request.ReturnDeletedFeatures ? "true" : "false",
            ["returnExtentOnly"] = request.ReturnExtentOnly ? "true" : "false",
            ["changesExtentGridCell"] = MapChangesExtentGridCell(request.ChangesExtentGridCell),
            ["dataFormat"] = MapExtractChangesDataFormat(request.DataFormat),
            ["async"] = request.DataFormat == ExtractChangesDataFormat.Sqlite ? "true" : null
        };

        if (request.OutSrid.HasValue) {
            parameters["outSR"] = request.OutSrid.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (request.ServerGens is not null) {
            parameters["serverGens"] = request.ServerGens.ToParameterValue();
        }
        else {
            parameters["layerServerGens"] = JsonSerializer.Serialize(
                request.LayerServerGens!.Select(x => new Dictionary<string, object?> {
                    ["id"] = x.Id,
                    ["serverGen"] = x.ServerGen,
                    ["minServerGen"] = x.MinServerGen
                }).ToArray());
        }

        if (request.LayerQueries is { Count: > 0 }) {
            parameters["layerQueries"] = SerializeLayerQueries(request.LayerQueries);
        }

        if (request.SpatialFilter is not null) {
            parameters["geometry"] = request.SpatialFilter.GeometryJson;
            parameters["geometryType"] = request.SpatialFilter.GeometryType;

            if (request.SpatialFilter.InSrid.HasValue) {
                parameters["inSR"] = request.SpatialFilter.InSrid.Value.ToString(CultureInfo.InvariantCulture);
            }
        }

        if (request.ReturnAttachments) {
            parameters["returnAttachments"] = "true";
        }

        if (request.ReturnAttachmentsDataByUrl) {
            parameters["returnAttachmentsDataByUrl"] = "true";
        }

        if (request.FieldsToCompare is { Count: > 0 }) {
            parameters["fieldsToCompare"] = JsonSerializer.Serialize(new Dictionary<string, object?> {
                ["fields"] = request.FieldsToCompare
            });
        }

        return parameters;
    }

    private static string MapChangesExtentGridCell(ExtractChangesExtentGridCell value) {
        return value switch {
            ExtractChangesExtentGridCell.None => "none",
            ExtractChangesExtentGridCell.Large => "large",
            ExtractChangesExtentGridCell.Medium => "medium",
            ExtractChangesExtentGridCell.Small => "small",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
        };
    }

    private static string MapExtractChangesDataFormat(ExtractChangesDataFormat value) {
        return value switch {
            ExtractChangesDataFormat.Json => "json",
            ExtractChangesDataFormat.Sqlite => "sqllite",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
        };
    }

    private async Task<ExtractChangesResult> MapExtractChangesResultAsync(
    JsonElement root,
    CancellationToken cancellationToken) {
        var dto = JsonSerializer.Deserialize<EsriExtractChangesResponseDto>(
                      root.GetRawText(),
                      JsonOptions)
                  ?? throw new FeatureServiceException(
                      "The extractChanges payload could not be deserialized.",
                      UriUtility.AppendPath(_serviceUri, "extractChanges"));

        var schemasByLayerId = new Dictionary<int, FeatureLayerSchema>();
        var edits = new List<ExtractChangesLayerEdits>();

        foreach (var editDto in dto.Edits ?? Enumerable.Empty<EsriExtractChangesLayerEditsDto>()) {
            FeatureLayerSchema? schema = null;

            if (editDto.Features is not null) {
                if (!schemasByLayerId.TryGetValue(editDto.Id, out schema)) {
                    schema = await GetLayerSchemaAsync(editDto.Id, cancellationToken);
                    schemasByLayerId[editDto.Id] = schema;
                }
            }

            edits.Add(new ExtractChangesLayerEdits(
                editDto.Id,
                editDto.ObjectIds is null
                    ? null
                    : new ExtractChangesIdChanges(
                        ReadJsonValueList(editDto.ObjectIds.Adds),
                        ReadJsonValueList(editDto.ObjectIds.Updates),
                        ReadJsonValueList(editDto.ObjectIds.Deletes)),
                editDto.Features is null || schema is null
                    ? null
                    : new ExtractChangesFeatureChanges(
                        MapExtractChangesFeatures(schema, editDto.Features.Adds),
                        MapExtractChangesFeatures(schema, editDto.Features.Updates),
                        MapExtractChangesFeatures(schema, editDto.Features.Deletes),
                        ReadJsonValueList(editDto.Features.DeleteIds)),
                editDto.Attachments is null
                    ? null
                    : new ExtractChangesAttachmentChanges(
                        ReadJsonValueList(editDto.Attachments.Adds),
                        ReadJsonValueList(editDto.Attachments.Updates),
                        ReadJsonValueList(editDto.Attachments.Deletes),
                        ReadJsonValueList(editDto.Attachments.DeleteIds)),
                ReadJsonValueList(editDto.FieldUpdates),
                editDto.HasGeometryUpdates));
        }

        return new ExtractChangesResult(
            dto.LayerServerGens?.Select(MapLayerServerGen).ToArray() ?? Array.Empty<ExtractChangesLayerServerGen>(),
            edits,
            dto.TransportType,
            dto.ResponseType,
            MapExtractChangesExtent(dto.Extent));
    }

    private FeatureExtent? MapExtractChangesExtent(EsriExtractChangesExtentDto? dto) {
        if (dto is null ||
            dto.XMin is null ||
            dto.YMin is null ||
            dto.XMax is null ||
            dto.YMax is null) {
            return null;
        }

        var srid = dto.SpatialReference is null
            ? null
            : _options.PreferLatestWkid
                ? dto.SpatialReference.LatestWkid ?? dto.SpatialReference.Wkid
                : dto.SpatialReference.Wkid ?? dto.SpatialReference.LatestWkid;

        return new FeatureExtent(
            new NetTopologySuite.Geometries.Envelope(
                dto.XMin.Value,
                dto.XMax.Value,
                dto.YMin.Value,
                dto.YMax.Value),
            srid);
    }

    /// <inheritdoc />
    public async Task<ExtractChangesJobStatus> GetExtractChangesStatusAsync(
    Uri statusUrl,
    CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(statusUrl);

        var dto = await GetAsync<EsriExtractChangesJobStatusDto>(statusUrl, cancellationToken);

        return new ExtractChangesJobStatus(
            Status: dto.Status ?? "Unknown",
            ResponseType: dto.ResponseType,
            TransportType: dto.TransportType,
            ResultUrl: string.IsNullOrWhiteSpace(dto.ResultUrl)
                ? null
                : new Uri(dto.ResultUrl, UriKind.Absolute),
            SubmissionTime: dto.SubmissionTime,
            LastUpdatedTime: dto.LastUpdatedTime);
    }

    private static string SerializeLayerQueries(
    IReadOnlyDictionary<int, ExtractChangesLayerQuery> layerQueries) {
        var payload = layerQueries.ToDictionary(
            pair => pair.Key.ToString(CultureInfo.InvariantCulture),
            pair => new Dictionary<string, object?> {
                ["queryOption"] = MapLayerQueryOption(pair.Value.QueryOption),
                ["where"] = pair.Value.Where,
                ["useGeometry"] = pair.Value.UseGeometry,
                ["includeRelated"] = pair.Value.IncludeRelated
            });

        return JsonSerializer.Serialize(payload);
    }

    private static string MapLayerQueryOption(ExtractChangesLayerQueryOption option) {
        return option switch {
            ExtractChangesLayerQueryOption.None => "none",
            ExtractChangesLayerQueryOption.UseFilter => "useFilter",
            ExtractChangesLayerQueryOption.All => "all",
            _ => throw new ArgumentOutOfRangeException(nameof(option), option, null)
        };
    }

    private static ExtractChangesLayerServerGen MapLayerServerGen(EsriLayerServerGenDto dto) {
        return new ExtractChangesLayerServerGen(dto.Id, dto.ServerGen, dto.MinServerGen);
    }

    private IReadOnlyList<FeatureRecord> MapExtractChangesFeatures(
        FeatureLayerSchema schema,
        List<EsriFeatureDto>? features) {
        if (features is null || features.Count == 0) {
            return Array.Empty<FeatureRecord>();
        }

        return features.Select(feature => MapExtractChangesFeature(schema, feature)).ToArray();
    }

    private FeatureRecord MapExtractChangesFeature(
        FeatureLayerSchema schema,
        EsriFeatureDto feature) {
        var attributes = ReadExtractChangesAttributes(feature.Attributes);

        Geometry? geometry = null;

        if (feature.Geometry.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined) {
            geometry = EsriGeometryReader.Read(
                feature.Geometry,
                schema.GeometryType,
                schema.Srid,
                _options.PreferLatestWkid,
                _options.FixInvalidGeometries,
                _options.TrueCurveHandling,
                _options.CircularArcSegmentCount);
        }

        long? objectId = null;

        if (!string.IsNullOrWhiteSpace(schema.ObjectIdFieldName) &&
            attributes.TryGetValue(schema.ObjectIdFieldName, out var rawObjectId)) {
            objectId = ConvertToNullableInt64(rawObjectId);
        }

        return new FeatureRecord(geometry, attributes, objectId);
    }

    private static IReadOnlyDictionary<string, object?> ReadExtractChangesAttributes(JsonElement attributesElement) {
        if (attributesElement.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) {
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        }

        var attributes = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var property in attributesElement.EnumerateObject()) {
            attributes[property.Name] = ConvertJsonValue(property.Value);
        }

        return attributes;
    }

    private static IReadOnlyList<object?> ReadJsonValueList(List<JsonElement>? elements) {
        if (elements is null || elements.Count == 0) {
            return Array.Empty<object?>();
        }

        return elements.Select(ConvertJsonValue).ToArray();
    }

    private static object? ConvertJsonValue(JsonElement element) {
        return element.ValueKind switch {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt64(out var int64Value) => int64Value,
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonValue).ToArray(),
            JsonValueKind.Object => element.EnumerateObject()
                .ToDictionary(
                    property => property.Name,
                    property => ConvertJsonValue(property.Value),
                    StringComparer.OrdinalIgnoreCase),
            _ => element.ToString()
        };
    }

    private static long? ConvertToNullableInt64(object? value) {
        return value switch {
            null => null,
            long longValue => longValue,
            int intValue => intValue,
            decimal decimalValue when decimal.Truncate(decimalValue) == decimalValue &&
                                      decimalValue >= long.MinValue &&
                                      decimalValue <= long.MaxValue => (long)decimalValue,
            double doubleValue when !double.IsNaN(doubleValue) &&
                                    !double.IsInfinity(doubleValue) &&
                                    Math.Abs(doubleValue % 1) < double.Epsilon &&
                                    doubleValue >= long.MinValue &&
                                    doubleValue <= long.MaxValue => (long)doubleValue,
            string stringValue when long.TryParse(
                stringValue,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var parsed) => parsed,
            _ => null
        };
    }
    private static void EnsureExtractChangesSupported(
        FeatureServiceMetadata metadata,
        ExtractChangesRequest request) {
        if (!metadata.Capabilities.SupportsChangeTracking) {
            throw new FeatureServiceCapabilityException(
                "The feature service does not support change tracking, so extractChanges is not available.");
        }

        var capabilities = metadata.ExtractChangesCapabilities;
        if (capabilities is null) {
            throw new FeatureServiceCapabilityException(
                "The feature service does not expose extractChanges capabilities.");
        }

        if (request.ReturnIdsOnly && !capabilities.SupportsReturnIdsOnly) {
            throw new FeatureServiceCapabilityException(
                "The feature service does not support extractChanges with ReturnIdsOnly.");
        }

        if (request.ReturnExtentOnly && !capabilities.SupportsReturnExtentOnly) {
            throw new FeatureServiceCapabilityException(
                "The feature service does not support extractChanges with ReturnExtentOnly.");
        }

        if (request.ChangesExtentGridCell != ExtractChangesExtentGridCell.None &&
            !capabilities.SupportsReturnExtentOnly) {
            throw new FeatureServiceCapabilityException(
                "The feature service does not support extractChanges extent grid cells.");
        }

        if (request.ReturnAttachments && !capabilities.SupportsReturnAttachments) {
            throw new FeatureServiceCapabilityException(
                "The feature service does not support extractChanges with ReturnAttachments.");
        }

        if (request.LayerQueries is { Count: > 0 } && !capabilities.SupportsLayerQueries) {
            throw new FeatureServiceCapabilityException(
                "The feature service does not support extractChanges layerQueries.");
        }

        if (request.SpatialFilter is not null && !capabilities.SupportsGeometry) {
            throw new FeatureServiceCapabilityException(
                "The feature service does not support extractChanges geometry filters.");
        }

        if (request.FieldsToCompare is { Count: > 0 } && !capabilities.SupportsFieldsToCompare) {
            throw new FeatureServiceCapabilityException(
                "The feature service does not support extractChanges fieldsToCompare.");
        }

        if ((request.ServerGens is not null || request.LayerServerGens is { Count: > 0 }) &&
            !capabilities.SupportsServerGens) {
            throw new FeatureServiceCapabilityException(
                "The feature service does not support extractChanges server generation inputs.");
        }

        if (request.ReturnHasGeometryUpdates && !capabilities.SupportsReturnHasGeometryUpdates) {
            throw new FeatureServiceCapabilityException(
                "The feature service does not support extractChanges with ReturnHasGeometryUpdates.");
        }
    }

}