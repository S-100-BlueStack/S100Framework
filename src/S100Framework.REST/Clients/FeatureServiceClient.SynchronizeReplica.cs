using System.Globalization;
using System.Text.Json;
using S100Framework.REST.Exceptions;
using S100Framework.REST.Internal.Dto;
using S100Framework.REST.Internal.Http;
using S100Framework.REST.Models;

namespace S100Framework.REST.Clients;

/// <summary>
/// Provides service-level <c>synchronizeReplica</c> operations for feature service endpoints.
/// </summary>
public sealed partial class FeatureServiceClient
{
    /// <inheritdoc />
    public async Task<SynchronizeReplicaResult> SynchronizeReplicaAsync(
        SynchronizeReplicaRequest request,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);

        request.Validate();

        var metadata = await GetMetadataAsync(cancellationToken);
        EnsureSynchronizeReplicaSupported(metadata, request);

        var submission = await SubmitSynchronizeReplicaAsync(request, cancellationToken);

        if (submission.StatusUrl is not null) {
            throw new InvalidOperationException(
                "The server returned an asynchronous synchronizeReplica response. Use GetSynchronizeReplicaStatusAsync to poll the job.");
        }

        return submission.Result
            ?? throw new InvalidOperationException(
                "The synchronizeReplica request did not return an embedded result.");
    }

    /// <inheritdoc />
    public async Task<SynchronizeReplicaSubmissionResult> SubmitSynchronizeReplicaAsync(
        SynchronizeReplicaRequest request,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);

        request.Validate();

        var metadata = await GetMetadataAsync(cancellationToken);
        EnsureSynchronizeReplicaSupported(metadata, request);

        var endpointUri = UriUtility.AppendPath(_serviceUri, "synchronizeReplica");
        var parameters = BuildSynchronizeReplicaParameters(request);

        var document = await PostFormAsync<JsonDocument>(endpointUri, parameters, cancellationToken);
        var root = document.RootElement;

        if (root.TryGetProperty("statusUrl", out var statusUrlElement) ||
            root.TryGetProperty("statusURL", out statusUrlElement)) {
            if (statusUrlElement.ValueKind != JsonValueKind.String) {
                throw new FeatureServiceException(
                    "The server returned an invalid statusUrl for synchronizeReplica.",
                    endpointUri);
            }

            var rawStatusUrl = statusUrlElement.GetString();

            if (string.IsNullOrWhiteSpace(rawStatusUrl)) {
                throw new FeatureServiceException(
                    "The server returned an empty statusUrl for synchronizeReplica.",
                    endpointUri);
            }

            if (!Uri.TryCreate(rawStatusUrl, UriKind.Absolute, out var statusUrl)) {
                throw new FeatureServiceException(
                    "The server returned an invalid statusUrl for synchronizeReplica.",
                    endpointUri);
            }

            return new SynchronizeReplicaSubmissionResult(
                Result: null,
                StatusUrl: statusUrl);
        }

        return new SynchronizeReplicaSubmissionResult(
            MapSynchronizeReplicaResult(root, endpointUri),
            StatusUrl: null);
    }

    /// <inheritdoc />
    public async Task<SynchronizeReplicaJobStatus> GetSynchronizeReplicaStatusAsync(
        Uri statusUrl,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(statusUrl);

        using var document = await GetSynchronizeReplicaStatusDocumentAsync(statusUrl, cancellationToken);

        var dto = JsonSerializer.Deserialize<EsriSynchronizeReplicaJobStatusDto>(
                      document.RootElement.GetRawText(),
                      JsonOptions)
                  ?? throw new FeatureServiceException(
                      "The synchronizeReplica status payload could not be deserialized.",
                      statusUrl);

        Uri? resultUrl = null;
        var rawResultUrl = dto.ResultUrl ?? dto.Url;

        if (!string.IsNullOrWhiteSpace(rawResultUrl) &&
            !Uri.TryCreate(rawResultUrl, UriKind.Absolute, out resultUrl)) {
            throw new FeatureServiceException(
                "The server returned an invalid resultUrl for synchronizeReplica.",
                statusUrl);
        }

        return new SynchronizeReplicaJobStatus(
            Status: dto.Status ?? "Unknown",
            ReplicaName: dto.ReplicaName,
            ResponseType: dto.ResponseType,
            TransportType: dto.TransportType,
            ResultUrl: resultUrl,
            SubmissionTime: ReadOptionalSynchronizeReplicaInt64(dto.SubmissionTime, statusUrl, "submissionTime"),
            LastUpdatedTime: ReadOptionalSynchronizeReplicaInt64(dto.LastUpdatedTime, statusUrl, "lastUpdatedTime")) {
            ErrorCode = dto.Error?.Code,
            ErrorMessage = string.IsNullOrWhiteSpace(dto.Error?.Message) ? null : dto.Error.Message,
            ErrorDetails = dto.Error?.Details?
                               .Where(static detail => !string.IsNullOrWhiteSpace(detail))
                               .Select(static detail => detail!)
                               .ToArray()
                           ?? Array.Empty<string>()
        };
    }

    private async Task<JsonDocument> GetSynchronizeReplicaStatusDocumentAsync(
    Uri statusUrl,
    CancellationToken cancellationToken) {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_options.RequestTimeout);

        using var request = new HttpRequestMessage(HttpMethod.Get, statusUrl);

        if (_authorizer is not null) {
            await _authorizer.ApplyAsync(request, timeoutCts.Token);
        }

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            timeoutCts.Token);

        var payload = await response.Content.ReadAsStringAsync(timeoutCts.Token);

        if (!response.IsSuccessStatusCode) {
            if (!string.IsNullOrWhiteSpace(payload) && TryParseEsriError(payload, out var esriError)) {
                throw new FeatureServiceException(
                    esriError.Message ?? "The server returned an Esri error payload.",
                    statusUrl,
                    esriError.Code,
                    esriError.Details?
                        .Where(static detail => !string.IsNullOrWhiteSpace(detail))
                        .Select(static detail => detail!)
                        .ToArray(),
                    response.StatusCode);
            }

            throw new FeatureServiceException(
                $"The server returned HTTP {(int)response.StatusCode} ({response.StatusCode}).",
                statusUrl,
                statusCode: response.StatusCode);
        }

        if (string.IsNullOrWhiteSpace(payload)) {
            throw new FeatureServiceException(
                "The server returned an empty synchronizeReplica status payload.",
                statusUrl,
                statusCode: response.StatusCode);
        }

        JsonDocument document;

        try {
            document = JsonDocument.Parse(payload);
        }
        catch (JsonException) {
            throw new FeatureServiceException(
                "The server returned an invalid synchronizeReplica status JSON payload.",
                statusUrl,
                statusCode: response.StatusCode);
        }

        if (document.RootElement.ValueKind == JsonValueKind.Object &&
            document.RootElement.TryGetProperty("error", out _) &&
            !document.RootElement.TryGetProperty("status", out _)) {
            document.Dispose();

            if (TryParseEsriError(payload, out var esriError)) {
                throw new FeatureServiceException(
                    esriError.Message ?? "The server returned an Esri error payload.",
                    statusUrl,
                    esriError.Code,
                    esriError.Details?
                        .Where(static detail => !string.IsNullOrWhiteSpace(detail))
                        .Select(static detail => detail!)
                        .ToArray(),
                    response.StatusCode);
            }

            throw new FeatureServiceException(
                "The server returned an Esri error payload.",
                statusUrl,
                statusCode: response.StatusCode);
        }

        return document;
    }

    /// <inheritdoc />
    public async Task<SynchronizeReplicaFileResult> DownloadSynchronizeReplicaFileAsync(
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
                    esriError.Details?
                        .Where(static detail => !string.IsNullOrWhiteSpace(detail))
                        .Select(static detail => detail!)
                        .ToArray(),
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

        return new SynchronizeReplicaFileResult(bytes, contentType, fileName, resultUrl);
    }

    private static Dictionary<string, string?> BuildSynchronizeReplicaParameters(
    SynchronizeReplicaRequest request) {
        var syncModel = request.SyncModel switch {
            SynchronizeReplicaSyncModel.PerReplica => "perReplica",
            SynchronizeReplicaSyncModel.PerLayer => "perLayer",
            _ => throw new ArgumentOutOfRangeException(nameof(request), request.SyncModel, null)
        };

        var parameters = new Dictionary<string, string?> {
            ["f"] = "json",
            ["replicaID"] = request.ReplicaId,
            ["syncModel"] = syncModel,
            ["transportType"] = MapSynchronizeReplicaTransportType(request.TransportType),
            ["closeReplica"] = request.CloseReplica ? "true" : "false",
            ["returnAttachmentsDataByUrl"] = request.ReturnAttachmentsDataByUrl ? "true" : "false",
            ["async"] = request.IsAsync ? "true" : "false",
            ["syncDirection"] = MapSynchronizeReplicaSyncDirection(request.SyncDirection),
            ["dataFormat"] = MapSynchronizeReplicaDataFormat(request.DataFormat)
        };

        if (request.ReplicaServerGen.HasValue) {
            parameters["replicaServerGen"] = request.ReplicaServerGen.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (request.SyncLayers is { Count: > 0 }) {
            parameters["syncLayers"] = JsonSerializer.Serialize(
                request.SyncLayers.Select(static layer => new Dictionary<string, object?> {
                    ["id"] = layer.Id,
                    ["serverGen"] = layer.ServerGen,
                    ["syncDirection"] = MapSynchronizeReplicaSyncDirection(layer.SyncDirection)
                }));
        }

        return parameters;
    }

    private static string MapSynchronizeReplicaDataFormat(SynchronizeReplicaDataFormat value) {
        return value switch {
            SynchronizeReplicaDataFormat.Json => "json",
            SynchronizeReplicaDataFormat.Sqlite => "sqlite",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
        };
    }

    private static string MapSynchronizeReplicaSyncDirection(SynchronizeReplicaSyncDirection value) {
        return value switch {
            SynchronizeReplicaSyncDirection.Download => "download",
            SynchronizeReplicaSyncDirection.Snapshot => "snapshot",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
        };
    }

    private static string MapSynchronizeReplicaTransportType(SynchronizeReplicaTransportType value) {
        return value switch {
            SynchronizeReplicaTransportType.Url => "esriTransportTypeURL",
            SynchronizeReplicaTransportType.Embedded => "esriTransportTypeEmbedded",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
        };
    }

    private static SynchronizeReplicaResult MapSynchronizeReplicaResult(
        JsonElement root,
        Uri endpointUri) {
        var dto = JsonSerializer.Deserialize<EsriSynchronizeReplicaResponseDto>(
                      root.GetRawText(),
                      JsonOptions)
                  ?? throw new FeatureServiceException(
                      "The synchronizeReplica payload could not be deserialized.",
                      endpointUri);

        return new SynchronizeReplicaResult(
            ReplicaId: dto.ReplicaId,
            ReplicaName: dto.ReplicaName,
            TransportType: dto.TransportType,
            ResponseType: dto.ResponseType,
            ReplicaServerGen: ReadOptionalSynchronizeReplicaInt64(dto.ReplicaServerGen, endpointUri, "replicaServerGen"),
            LayerServerGens: (dto.LayerServerGens ?? Enumerable.Empty<EsriSynchronizeReplicaLayerServerGenDto?>())
                .Where(static layerServerGen => layerServerGen is not null)
                .Select(layerServerGen => MapSynchronizeReplicaLayerServerGen(layerServerGen!, endpointUri))
                .ToArray(),
            ResultUrl: ReadOptionalSynchronizeReplicaUri(dto.ResultUrl ?? dto.Url, endpointUri, "resultUrl"),
            Status: dto.Status,
            SubmissionTime: ReadOptionalSynchronizeReplicaInt64(dto.SubmissionTime, endpointUri, "submissionTime"),
            LastUpdatedTime: ReadOptionalSynchronizeReplicaInt64(dto.LastUpdatedTime, endpointUri, "lastUpdatedTime"));
    }

    private static SynchronizeReplicaLayerServerGen MapSynchronizeReplicaLayerServerGen(
        EsriSynchronizeReplicaLayerServerGenDto dto,
        Uri endpointUri) {
        var layerId = ReadRequiredSynchronizeReplicaInt32(
            dto.Id,
            endpointUri,
            "layerServerGens",
            "layer ID");

        var serverGen = ReadRequiredSynchronizeReplicaInt64(
            dto.ServerGen,
            endpointUri,
            "layerServerGens",
            "serverGen");

        if (layerId < 0) {
            throw new FeatureServiceException(
                "The synchronizeReplica payload returned a layerServerGens item with a negative layer ID.",
                endpointUri);
        }

        if (serverGen < 0) {
            throw new FeatureServiceException(
                "The synchronizeReplica payload returned a layerServerGens item with a negative serverGen value.",
                endpointUri);
        }

        return new SynchronizeReplicaLayerServerGen(layerId, serverGen);
    }

    private static Uri? ReadOptionalSynchronizeReplicaUri(
        string? rawUrl,
        Uri endpointUri,
        string propertyName) {
        if (string.IsNullOrWhiteSpace(rawUrl)) {
            return null;
        }

        if (!Uri.TryCreate(rawUrl, UriKind.Absolute, out var uri)) {
            throw new FeatureServiceException(
                $"The server returned an invalid {propertyName} for synchronizeReplica.",
                endpointUri);
        }

        return uri;
    }

    private static int ReadRequiredSynchronizeReplicaInt32(
        JsonElement? element,
        Uri endpointUri,
        string collectionName,
        string propertyName) {
        if (!TryReadSynchronizeReplicaInt64(element, out var value) ||
            value < int.MinValue ||
            value > int.MaxValue) {
            throw new FeatureServiceException(
                $"The synchronizeReplica payload returned a {collectionName} item without a valid {propertyName} value.",
                endpointUri);
        }

        return (int)value;
    }

    private static long ReadRequiredSynchronizeReplicaInt64(
        JsonElement? element,
        Uri endpointUri,
        string collectionName,
        string propertyName) {
        if (!TryReadSynchronizeReplicaInt64(element, out var value)) {
            throw new FeatureServiceException(
                $"The synchronizeReplica payload returned a {collectionName} item without a valid {propertyName} value.",
                endpointUri);
        }

        return value;
    }

    private static long? ReadOptionalSynchronizeReplicaInt64(
        JsonElement? element,
        Uri endpointUri,
        string propertyName) {
        if (!element.HasValue ||
            element.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) {
            return null;
        }

        if (!TryReadSynchronizeReplicaInt64(element, out var value)) {
            throw new FeatureServiceException(
                $"The server returned an invalid {propertyName} value for synchronizeReplica.",
                endpointUri);
        }

        if (value < 0) {
            throw new FeatureServiceException(
                $"The server returned a negative {propertyName} value for synchronizeReplica.",
                endpointUri);
        }

        return value;
    }

    private static bool TryReadSynchronizeReplicaInt64(
        JsonElement? element,
        out long value) {
        value = 0;

        if (!element.HasValue) {
            return false;
        }

        var jsonElement = element.Value;

        return jsonElement.ValueKind switch {
            JsonValueKind.Number => jsonElement.TryGetInt64(out value),
            JsonValueKind.String => long.TryParse(
                jsonElement.GetString(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out value),
            _ => false
        };
    }

    private static void EnsureSynchronizeReplicaSupported(
     FeatureServiceMetadata metadata,
     SynchronizeReplicaRequest request) {
        if (!metadata.Capabilities.SupportsSync && !metadata.Capabilities.SyncEnabled) {
            throw new FeatureServiceCapabilityException(
                "The feature service does not support sync, so synchronizeReplica is not available.");
        }

        var syncCapabilities = metadata.SyncCapabilities;
        if (syncCapabilities is null) {
            throw new FeatureServiceCapabilityException(
                "The feature service does not expose sync capabilities.");
        }

        if (request.IsAsync && !syncCapabilities.SupportsAsync) {
            throw new FeatureServiceCapabilityException(
                "The feature service does not support asynchronous synchronizeReplica requests.");
        }

        switch (request.SyncModel) {
            case SynchronizeReplicaSyncModel.PerReplica when !syncCapabilities.SupportsPerReplicaSync:
                throw new FeatureServiceCapabilityException(
                    "The feature service does not support synchronizeReplica with the perReplica sync model.");
            case SynchronizeReplicaSyncModel.PerLayer when !syncCapabilities.SupportsPerLayerSync:
                throw new FeatureServiceCapabilityException(
                    "The feature service does not support synchronizeReplica with the perLayer sync model.");
        }
    }
}