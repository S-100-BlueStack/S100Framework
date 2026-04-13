using System.Net.Http.Json;
using System.Text.Json;
using S100Framework.REST.Abstractions;
using S100Framework.REST.Configuration;
using S100Framework.REST.Internal.Dto;
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

    public FeatureServiceClient(
        HttpClient httpClient,
        FeatureServiceClientOptions options,
        IFeatureServiceRequestAuthorizer? authorizer = null) {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _authorizer = authorizer;

        _options.Validate();
    }

    public async Task<FeatureServiceMetadata> GetMetadataAsync(CancellationToken cancellationToken = default) {
        var uri = UriUtility.WithQuery(
            _options.ServiceUri,
            new Dictionary<string, string?> {
                ["f"] = "json"
            });

        var dto = await GetAsync<EsriServiceMetadataDto>(uri, cancellationToken);

        return new FeatureServiceMetadata(
            _options.ServiceUri,
            dto.Layers?.Select(MapDataset).ToArray() ?? [],
            dto.Tables?.Select(MapDataset).ToArray() ?? [],
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
            UriUtility.AppendPath(_options.ServiceUri, layerId.ToString()),
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
            dto.Fields?.Select(MapField).ToArray() ?? []);
    }

    internal Task<EsriQueryResponseDto> QueryFeaturesAsync(
        int layerId,
        FeatureQuery query,
        int? resultOffset,
        int? resultRecordCount,
        IReadOnlyList<long>? objectIds,
        CancellationToken cancellationToken = default) {
        var parameters = new Dictionary<string, string?> {
            ["f"] = "json",
            ["returnGeometry"] = query.ReturnGeometry ? "true" : "false",
            ["outFields"] = query.OutFields is { Count: > 0 }
                ? string.Join(",", query.OutFields)
                : "*",
            ["orderByFields"] = query.OrderBy
        };

        if (objectIds is { Count: > 0 }) {
            parameters["objectIds"] = string.Join(",", objectIds);
        }
        else {
            parameters["where"] = string.IsNullOrWhiteSpace(query.Where) ? "1=1" : query.Where;
        }

        if (resultOffset.HasValue) {
            parameters["resultOffset"] = resultOffset.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        if (resultRecordCount.HasValue) {
            parameters["resultRecordCount"] = resultRecordCount.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        var uri = UriUtility.WithQuery(
            UriUtility.AppendPath(_options.ServiceUri, $"{layerId}/query"),
            parameters);

        return GetAsync<EsriQueryResponseDto>(uri, cancellationToken);
    }

    internal Task<EsriIdsResponseDto> QueryIdsAsync(
        int layerId,
        FeatureQuery query,
        CancellationToken cancellationToken = default) {
        var uri = UriUtility.WithQuery(
            UriUtility.AppendPath(_options.ServiceUri, $"{layerId}/query"),
            new Dictionary<string, string?> {
                ["f"] = "json",
                ["where"] = string.IsNullOrWhiteSpace(query.Where) ? "1=1" : query.Where,
                ["returnIdsOnly"] = "true",
                ["orderByFields"] = query.OrderBy
            });

        return GetAsync<EsriIdsResponseDto>(uri, cancellationToken);
    }

    internal FeatureServiceClientOptions Options => _options;

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

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<T>(JsonOptions, timeoutCts.Token);

        return payload ?? throw new InvalidOperationException(
            $"Received an empty payload while deserializing '{typeof(T).Name}'.");
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
}