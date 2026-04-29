using System.Net.Http.Headers;
using S100Framework.REST.Exceptions;
using S100Framework.REST.Internal.Dto;
using S100Framework.REST.Internal.Http;
using S100Framework.REST.Models;

namespace S100Framework.REST.Clients;

/// <summary>
/// Provides upload operations for feature service server-side upload items.
/// </summary>
public sealed partial class FeatureServiceClient
{
    /// <inheritdoc />
    public async Task<FeatureServiceUploadResult> UploadItemAsync(
        FeatureServiceUploadRequest request,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);

        request.Validate();

        await EnsureUploadsSupportedAsync(cancellationToken);

        var endpointUri = UriUtility.AppendPath(_serviceUri, "uploads/upload");

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_options.RequestTimeout);

        using var multipartContent = new MultipartFormDataContent();
        multipartContent.Add(new StringContent("json"), "f");

        if (!string.IsNullOrWhiteSpace(request.Description)) {
            multipartContent.Add(new StringContent(request.Description), "description");
        }

        var fileContent = new StreamContent(request.Content!);

        if (!string.IsNullOrWhiteSpace(request.ContentType)) {
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(request.ContentType);
        }

        multipartContent.Add(fileContent, "file", request.FileName);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpointUri) {
            Content = multipartContent
        };

        var dto = await SendAsync<EsriUploadResponseDto>(
            httpRequest,
            endpointUri,
            timeoutCts.Token);

        if (dto.Success == false) {
            throw new FeatureServiceException(
                "The server reported that the upload did not succeed.",
                endpointUri);
        }

        if (dto.Item is null || string.IsNullOrWhiteSpace(dto.Item.ItemId)) {
            throw new FeatureServiceException(
                "The server returned an upload response without an item ID.",
                endpointUri);
        }

        return new FeatureServiceUploadResult(
            dto.Item.ItemId,
            dto.Item.ItemName,
            dto.Item.Description,
            dto.Item.Date,
            dto.Item.Committed);
    }

    private async Task EnsureUploadsSupportedAsync(CancellationToken cancellationToken) {
        var metadata = await GetMetadataAsync(cancellationToken);

        if (!metadata.Capabilities.SupportsUploads) {
            throw new FeatureServiceCapabilityException(
                "The feature service does not advertise upload support.");
        }
    }

    /// <inheritdoc />
    public async Task<FeatureServiceUploadDeleteResult> DeleteUploadItemAsync(
        string itemId,
        CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(itemId)) {
            throw new ArgumentException("Upload item ID must be provided.", nameof(itemId));
        }

        await EnsureUploadsSupportedAsync(cancellationToken);

        var endpointUri = UriUtility.AppendPath(
            _serviceUri,
            $"uploads/{Uri.EscapeDataString(itemId)}/delete");

        var dto = await PostFormAsync<EsriUploadDeleteResponseDto>(
            endpointUri,
            new Dictionary<string, string?> {
                ["f"] = "json"
            },
            cancellationToken);

        if (!dto.Success.HasValue) {
            throw new FeatureServiceException(
                "The server returned an upload delete response without a success value.",
                endpointUri);
        }

        return new FeatureServiceUploadDeleteResult(
            itemId,
            dto.Success.Value);
    }
}