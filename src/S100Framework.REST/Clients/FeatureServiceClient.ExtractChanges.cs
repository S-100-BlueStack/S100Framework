using S100Framework.REST.Exceptions;
using S100Framework.REST.Internal.Dto;
using S100Framework.REST.Models;

namespace S100Framework.REST.Clients;

/// <summary>
/// Provides <c>extractChanges</c> operations for feature service endpoints.
/// </summary>
public sealed partial class FeatureServiceClient
{
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
}