using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using S100Framework.REST.Configuration;
using S100Framework.REST.Exceptions;
using S100Framework.REST.Internal.Dto;
using S100Framework.REST.Internal.EsriGeometry;
using S100Framework.REST.Internal.Http;
using S100Framework.REST.Models;

namespace S100Framework.REST.Clients;

/// <summary>
/// Provides shared HTTP transport helpers for <see cref="FeatureServiceClient" />.
/// </summary>
public sealed partial class FeatureServiceClient
{
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

    private static string MapSpatialDistanceUnit(FeatureSpatialDistanceUnit value) {
        return value switch {
            FeatureSpatialDistanceUnit.Meter => "esriSRUnit_Meter",
            FeatureSpatialDistanceUnit.StatuteMile => "esriSRUnit_StatuteMile",
            FeatureSpatialDistanceUnit.Foot => "esriSRUnit_Foot",
            FeatureSpatialDistanceUnit.Kilometer => "esriSRUnit_Kilometer",
            FeatureSpatialDistanceUnit.NauticalMile => "esriSRUnit_NauticalMile",
            FeatureSpatialDistanceUnit.UsNauticalMile => "esriSRUnit_USNauticalMile",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported spatial distance unit.")
        };
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

        if (spatialFilter.Distance.HasValue) {
            parameters["distance"] = spatialFilter.Distance.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (spatialFilter.DistanceUnit.HasValue) {
            parameters["units"] = MapSpatialDistanceUnit(spatialFilter.DistanceUnit.Value);
        }
    }

    private async Task<T> GetAsync<T>(
        Uri uri,
        CancellationToken cancellationToken) {
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
                string.IsNullOrWhiteSpace(esriError.Message)
                    ? "The server returned an Esri error payload."
                    : esriError.Message,
                requestUri,
                esriError.Code,
                MapEsriErrorDetails(esriError.Details),
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

    private static IReadOnlyList<string> MapEsriErrorDetails(
    IEnumerable<string?>? details) {
        return details?
            .Where(static detail => !string.IsNullOrWhiteSpace(detail))
            .Select(static detail => detail!)
            .ToArray() ?? Array.Empty<string>();
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