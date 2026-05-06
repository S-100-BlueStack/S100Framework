using System.Globalization;
using System.Net.Http.Headers;
using S100Framework.REST.Exceptions;
using S100Framework.REST.Internal.Dto;
using S100Framework.REST.Internal.Http;
using S100Framework.REST.Models;

namespace S100Framework.REST.Clients;

/// <summary>
/// Provides attachment operations for feature service layer endpoints.
/// </summary>
public sealed partial class FeatureServiceClient
{
    internal Task<EsriAttachmentQueryResponseDto> QueryAttachmentsAsync(
    int layerId,
    AttachmentQuery query,
    CancellationToken cancellationToken = default) {
        return QueryAttachmentsAsync(
            layerId,
            query,
            returnCountOnly: false,
            cancellationToken);
    }

    internal Task<EsriAttachmentQueryResponseDto> QueryAttachmentsAsync(
       int layerId,
       AttachmentQuery query,
       bool returnCountOnly,
       CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(query);

        query.Validate();

        var parameters = new Dictionary<string, string?> {
            ["f"] = "json",
            ["objectIds"] = query.ObjectIds is { Count: > 0 }
                ? string.Join(",", query.ObjectIds)
                : null,
            ["globalIds"] = query.GlobalIds is { Count: > 0 }
                ? string.Join(",", query.GlobalIds)
                : null,
            ["definitionExpression"] = query.DefinitionExpression,
            ["attachmentTypes"] = query.AttachmentTypes is { Count: > 0 }
                ? string.Join(",", query.AttachmentTypes)
                : null,
            ["keywords"] = query.Keywords is { Count: > 0 }
                ? string.Join(",", query.Keywords)
                : null,
            ["orderByFields"] = query.OrderByFields is { Count: > 0 }
                ? string.Join(",", query.OrderByFields)
                : null,
            ["returnUrl"] = query.ReturnUrl ? "true" : null,
            ["returnMetadata"] = query.ReturnMetadata ? "true" : null,
            ["returnCountOnly"] = returnCountOnly ? "true" : null
        };

        if (query.ResultOffset.HasValue) {
            parameters["resultOffset"] = query.ResultOffset.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (query.ResultRecordCount.HasValue) {
            parameters["resultRecordCount"] = query.ResultRecordCount.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (query.MinimumSizeBytes.HasValue || query.MaximumSizeBytes.HasValue) {
            var min = query.MinimumSizeBytes?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            var max = query.MaximumSizeBytes?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

            parameters["size"] = $"{min},{max}";
        }

        return SendLayerQueryAsync<EsriAttachmentQueryResponseDto>(
            $"{layerId.ToString(CultureInfo.InvariantCulture)}/queryAttachments",
            parameters,
            cancellationToken);
    }

    internal async Task<AttachmentContent> DownloadAttachmentAsync(
        int layerId,
        long objectId,
        long attachmentId,
        CancellationToken cancellationToken = default) {
        if (objectId < 0) {
            throw new InvalidOperationException("ObjectId must be greater than or equal to zero.");
        }

        if (attachmentId < 0) {
            throw new InvalidOperationException("AttachmentId must be greater than or equal to zero.");
        }

        var uri = UriUtility.AppendPath(
            _serviceUri,
            $"{layerId.ToString(CultureInfo.InvariantCulture)}/" +
            $"{objectId.ToString(CultureInfo.InvariantCulture)}/attachments/" +
            $"{attachmentId.ToString(CultureInfo.InvariantCulture)}");

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
                    MapEsriErrorDetails(esriError.Details),
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
            $"{layerId.ToString(CultureInfo.InvariantCulture)}/" +
            $"{request.ObjectId.ToString(CultureInfo.InvariantCulture)}/deleteAttachments");

        var dto = await PostFormAsync<EsriDeleteAttachmentsResponseDto>(
            endpointUri,
            parameters,
            cancellationToken);

        return new DeleteAttachmentsResult(
            (dto.DeleteAttachmentResults ?? Enumerable.Empty<EsriAttachmentEditResultDto?>())
                .Where(static result => result is not null)
                .Select(static result => MapAttachmentEditResult(result!))
                .ToArray(),
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
            $"{layerId.ToString(CultureInfo.InvariantCulture)}/" +
            $"{request.ObjectId.ToString(CultureInfo.InvariantCulture)}/addAttachment");

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

        var results = (dto.UpdateAttachmentResults ?? Enumerable.Empty<EsriAttachmentEditResultDto?>())
            .Where(static result => result is not null)
            .ToArray();

        var resultDto = results.Length == 1
            ? results[0]!
            : throw new FeatureServiceException(
                "The server did not return exactly one updateAttachmentResults entry.",
                endpointUri);

        return new UpdateAttachmentResult(
            MapAttachmentEditResult(resultDto),
            dto.EditMoment);
    }
}