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

}