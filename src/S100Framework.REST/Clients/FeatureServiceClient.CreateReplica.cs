using System.Globalization;
using System.Text.Json;
using S100Framework.REST.Exceptions;
using S100Framework.REST.Internal.Dto;
using S100Framework.REST.Internal.Http;
using S100Framework.REST.Internal.Json;
using S100Framework.REST.Models;

namespace S100Framework.REST.Clients;

/// <summary>
/// Provides <c>createReplica</c> operations for feature service endpoints.
/// </summary>
public sealed partial class FeatureServiceClient
{
    /// <inheritdoc />
    public async Task<CreateReplicaResult> CreateReplicaAsync(
        CreateReplicaRequest request,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);

        request.Validate();

        var metadata = await GetMetadataAsync(cancellationToken);
        EnsureCreateReplicaSupported(metadata, request);

        var submission = await SubmitCreateReplicaAsync(request, cancellationToken);

        if (submission.StatusUrl is not null) {
            throw new InvalidOperationException(
                "The server returned an asynchronous createReplica response. Use GetCreateReplicaStatusAsync to poll the job.");
        }

        return submission.Result
            ?? throw new InvalidOperationException(
                "The createReplica request did not return an embedded result.");
    }

    /// <inheritdoc />
    public async Task<CreateReplicaSubmissionResult> SubmitCreateReplicaAsync(
        CreateReplicaRequest request,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);

        request.Validate();

        var metadata = await GetMetadataAsync(cancellationToken);
        EnsureCreateReplicaSupported(metadata, request);

        var parameters = BuildCreateReplicaParameters(request);
        var endpointUri = UriUtility.AppendPath(_serviceUri, "createReplica");

        var document = await PostFormAsync<JsonDocument>(endpointUri, parameters, cancellationToken);
        var root = document.RootElement;

        if (root.TryGetProperty("statusUrl", out var statusUrlElement) ||
    root.TryGetProperty("statusURL", out statusUrlElement)) {
            if (statusUrlElement.ValueKind != JsonValueKind.String) {
                throw new FeatureServiceException(
                    "The server returned an invalid statusUrl for createReplica.",
                    endpointUri);
            }

            var rawStatusUrl = statusUrlElement.GetString();

            if (string.IsNullOrWhiteSpace(rawStatusUrl)) {
                throw new FeatureServiceException(
                    "The server returned an empty statusUrl for createReplica.",
                    endpointUri);
            }

            if (!Uri.TryCreate(rawStatusUrl, UriKind.Absolute, out var statusUrl)) {
                throw new FeatureServiceException(
                    "The server returned an invalid statusUrl for createReplica.",
                    endpointUri);
            }

            return new CreateReplicaSubmissionResult(
                Result: null,
                StatusUrl: statusUrl);
        }

        return new CreateReplicaSubmissionResult(
            MapCreateReplicaResult(root, endpointUri),
            StatusUrl: null);
    }

    /// <inheritdoc />
    public async Task<CreateReplicaJobStatus> GetCreateReplicaStatusAsync(
        Uri statusUrl,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(statusUrl);

        using var document = await GetCreateReplicaStatusDocumentAsync(statusUrl, cancellationToken);

        var dto = JsonSerializer.Deserialize<EsriCreateReplicaJobStatusDto>(
                      document.RootElement.GetRawText(),
                      JsonOptions)
                  ?? throw new FeatureServiceException(
                      "The createReplica status payload could not be deserialized.",
                      statusUrl);

        Uri? resultUrl = null;
        var rawResultUrl = dto.ResultUrl ?? dto.Url;

        if (!string.IsNullOrWhiteSpace(rawResultUrl) &&
            !Uri.TryCreate(rawResultUrl, UriKind.Absolute, out resultUrl)) {
            throw new FeatureServiceException(
                "The server returned an invalid resultUrl for createReplica.",
                statusUrl);
        }

        return new CreateReplicaJobStatus(
            Status: dto.Status ?? "Unknown",
            ReplicaName: dto.ReplicaName,
            ReplicaId: dto.ReplicaId,
            ResponseType: dto.ResponseType,
            TransportType: dto.TransportType,
            TargetType: dto.TargetType,
            ResultUrl: resultUrl,
            SubmissionTime: ReadOptionalCreateReplicaInt64(dto.SubmissionTime, statusUrl, "submissionTime"),
            LastUpdatedTime: ReadOptionalCreateReplicaInt64(dto.LastUpdatedTime, statusUrl, "lastUpdatedTime")) {
            ErrorCode = dto.Error?.Code,
            ErrorMessage = string.IsNullOrWhiteSpace(dto.Error?.Message) ? null : dto.Error.Message,
            ErrorDetails = dto.Error?.Details?
                               .Where(static detail => !string.IsNullOrWhiteSpace(detail))
                               .Select(static detail => detail!)
                               .ToArray()
                           ?? Array.Empty<string>()
        };
    }

    private async Task<JsonDocument> GetCreateReplicaStatusDocumentAsync(
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
                "The server returned an empty createReplica status payload.",
                statusUrl,
                statusCode: response.StatusCode);
        }

        JsonDocument document;

        try {
            document = JsonDocument.Parse(payload);
        }
        catch (JsonException) {
            throw new FeatureServiceException(
                "The server returned an invalid createReplica status JSON payload.",
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
    public async Task<CreateReplicaFileResult> DownloadCreateReplicaFileAsync(
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

        return new CreateReplicaFileResult(bytes, contentType, fileName, resultUrl);
    }

    private Dictionary<string, string?> BuildCreateReplicaParameters(CreateReplicaRequest request) {
        var parameters = new Dictionary<string, string?> {
            ["f"] = "json",
            ["replicaName"] = request.ReplicaName,
            ["layers"] = JsonSerializer.Serialize(request.Layers),
            ["transportType"] = MapCreateReplicaTransportType(request.TransportType),
            ["returnAttachments"] = request.ReturnAttachments ? "true" : "false",
            ["returnAttachmentsDataByUrl"] = request.ReturnAttachmentsDataByUrl ? "true" : "false",
            ["async"] = request.IsAsync ? "true" : "false",
            ["syncModel"] = MapCreateReplicaSyncModel(request.SyncModel),
            ["dataFormat"] = MapCreateReplicaDataFormat(request.DataFormat),
            ["targetType"] = MapCreateReplicaTargetType(request.TargetType)
        };

        if (request.LayerQueries is { Count: > 0 }) {
            parameters["layerQueries"] = SerializeCreateReplicaLayerQueries(request.LayerQueries);
        }

        if (request.SpatialFilter is not null) {
            parameters["geometry"] = request.SpatialFilter.GeometryJson;
            parameters["geometryType"] = request.SpatialFilter.GeometryType;

            if (request.SpatialFilter.InSrid.HasValue) {
                parameters["inSR"] = request.SpatialFilter.InSrid.Value.ToString(CultureInfo.InvariantCulture);
            }
        }

        if (request.ReplicaSrid.HasValue) {
            parameters["replicaSR"] = request.ReplicaSrid.Value.ToString(CultureInfo.InvariantCulture);
        }

        return parameters;
    }

    private static string SerializeCreateReplicaLayerQueries(
        IReadOnlyDictionary<int, CreateReplicaLayerQuery> layerQueries) {
        var payload = new Dictionary<string, Dictionary<string, object?>>();

        foreach (var pair in layerQueries) {
            var value = pair.Value;
            var item = new Dictionary<string, object?>();

            if (value.QueryOption != CreateReplicaLayerQueryOption.Default) {
                item["queryOption"] = MapCreateReplicaLayerQueryOption(value.QueryOption);
            }

            if (!string.IsNullOrWhiteSpace(value.Where)) {
                item["where"] = value.Where;
            }

            if (value.UseGeometry.HasValue) {
                item["useGeometry"] = value.UseGeometry.Value;
            }

            if (value.IncludeRelated.HasValue) {
                item["includeRelated"] = value.IncludeRelated.Value;
            }

            payload[pair.Key.ToString(CultureInfo.InvariantCulture)] = item;
        }

        return JsonSerializer.Serialize(payload);
    }

    private static string MapCreateReplicaLayerQueryOption(CreateReplicaLayerQueryOption value) {
        return value switch {
            CreateReplicaLayerQueryOption.None => "none",
            CreateReplicaLayerQueryOption.UseFilter => "useFilter",
            CreateReplicaLayerQueryOption.All => "all",
            CreateReplicaLayerQueryOption.Default => throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "The default createReplica query option should not be serialized."),
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
        };
    }

    private static string MapCreateReplicaDataFormat(CreateReplicaDataFormat value) {
        return value switch {
            CreateReplicaDataFormat.Json => "json",
            CreateReplicaDataFormat.Sqlite => "sqlite",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
        };
    }

    private static string MapCreateReplicaSyncModel(CreateReplicaSyncModel value) {
        return value switch {
            CreateReplicaSyncModel.PerReplica => "perReplica",
            CreateReplicaSyncModel.PerLayer => "perLayer",
            CreateReplicaSyncModel.None => "none",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
        };
    }

    private static string MapCreateReplicaTransportType(CreateReplicaTransportType value) {
        return value switch {
            CreateReplicaTransportType.Url => "esriTransportTypeURL",
            CreateReplicaTransportType.Embedded => "esriTransportTypeEmbedded",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
        };
    }

    private static string MapCreateReplicaTargetType(CreateReplicaTargetType value) {
        return value switch {
            CreateReplicaTargetType.Client => "client",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
        };
    }

    private static CreateReplicaResult MapCreateReplicaResult(
        JsonElement root,
        Uri endpointUri) {
        var dto = JsonSerializer.Deserialize<EsriCreateReplicaResponseDto>(
                      root.GetRawText(),
                      JsonOptions)
                  ?? throw new FeatureServiceException(
                      "The createReplica payload could not be deserialized.",
                      endpointUri);

        var resultUrl = ReadOptionalCreateReplicaUri(dto.ResultUrl ?? dto.Url, endpointUri, "resultUrl");

        return new CreateReplicaResult(
            ReplicaName: dto.ReplicaName,
            ReplicaId: dto.ReplicaId,
            TransportType: dto.TransportType,
            ResponseType: dto.ResponseType,
            SyncModel: dto.SyncModel,
            TargetType: dto.TargetType,
            ReplicaServerGen: ReadOptionalCreateReplicaInt64(dto.ReplicaServerGen, endpointUri, "replicaServerGen"),
            LayerServerGens: (dto.LayerServerGens ?? Enumerable.Empty<EsriCreateReplicaLayerServerGenDto?>())
                .Where(static layerServerGen => layerServerGen is not null)
                .Select(layerServerGen => MapCreateReplicaLayerServerGen(layerServerGen!, endpointUri))
                .ToArray(),
            ResultUrl: resultUrl,
            Status: dto.Status,
            SubmissionTime: ReadOptionalCreateReplicaInt64(dto.SubmissionTime, endpointUri, "submissionTime"),
            LastUpdatedTime: ReadOptionalCreateReplicaInt64(dto.LastUpdatedTime, endpointUri, "lastUpdatedTime"));
    }

    private static CreateReplicaLayerServerGen MapCreateReplicaLayerServerGen(
        EsriCreateReplicaLayerServerGenDto dto,
        Uri endpointUri) {
        var layerId = ReadRequiredCreateReplicaInt32(
            dto.Id,
            endpointUri,
            "layerServerGens",
            "layer ID");

        var serverGen = ReadRequiredCreateReplicaInt64(
            dto.ServerGen,
            endpointUri,
            "layerServerGens",
            "serverGen");

        if (layerId < 0) {
            throw new FeatureServiceException(
                "The createReplica payload returned a layerServerGens item with a negative layer ID.",
                endpointUri);
        }

        if (serverGen < 0) {
            throw new FeatureServiceException(
                "The createReplica payload returned a layerServerGens item with a negative serverGen value.",
                endpointUri);
        }

        return new CreateReplicaLayerServerGen(layerId, serverGen);
    }

    private static Uri? ReadOptionalCreateReplicaUri(
        string? rawUrl,
        Uri endpointUri,
        string propertyName) {
        if (string.IsNullOrWhiteSpace(rawUrl)) {
            return null;
        }

        if (!Uri.TryCreate(rawUrl, UriKind.Absolute, out var uri)) {
            throw new FeatureServiceException(
                $"The server returned an invalid {propertyName} for createReplica.",
                endpointUri);
        }

        return uri;
    }

    private static int ReadRequiredCreateReplicaInt32(
        JsonElement? element,
        Uri endpointUri,
        string collectionName,
        string propertyName) {
        if (!TryReadCreateReplicaInt64(element, out var value) ||
            value < int.MinValue ||
            value > int.MaxValue) {
            throw new FeatureServiceException(
                $"The createReplica payload returned a {collectionName} item without a valid {propertyName} value.",
                endpointUri);
        }

        return (int)value;
    }

    private static long ReadRequiredCreateReplicaInt64(
        JsonElement? element,
        Uri endpointUri,
        string collectionName,
        string propertyName) {
        if (!TryReadCreateReplicaInt64(element, out var value)) {
            throw new FeatureServiceException(
                $"The createReplica payload returned a {collectionName} item without a valid {propertyName} value.",
                endpointUri);
        }

        return value;
    }

    private static long? ReadOptionalCreateReplicaInt64(
        JsonElement? element,
        Uri endpointUri,
        string propertyName) {
        if (!element.HasValue ||
            element.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) {
            return null;
        }

        if (!TryReadCreateReplicaInt64(element, out var value)) {
            throw new FeatureServiceException(
                $"The server returned an invalid {propertyName} value for createReplica.",
                endpointUri);
        }

        if (value < 0) {
            throw new FeatureServiceException(
                $"The server returned a negative {propertyName} value for createReplica.",
                endpointUri);
        }

        return value;
    }

    private static bool TryReadCreateReplicaInt64(
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

    private static void EnsureCreateReplicaSupported(
        FeatureServiceMetadata metadata,
        CreateReplicaRequest request) {
        if (!metadata.Capabilities.SupportsSync && !metadata.Capabilities.SyncEnabled) {
            throw new FeatureServiceCapabilityException(
                "The feature service does not support sync, so createReplica is not available.");
        }

        var syncCapabilities = metadata.SyncCapabilities;
        if (syncCapabilities is null) {
            throw new FeatureServiceCapabilityException(
                "The feature service does not expose sync capabilities.");
        }

        if (request.IsAsync && !syncCapabilities.SupportsAsync) {
            throw new FeatureServiceCapabilityException(
                "The feature service does not support asynchronous createReplica requests.");
        }

        switch (request.SyncModel) {
            case CreateReplicaSyncModel.PerReplica when !syncCapabilities.SupportsPerReplicaSync:
                throw new FeatureServiceCapabilityException(
                    "The feature service does not support createReplica with the perReplica sync model.");
            case CreateReplicaSyncModel.PerLayer when !syncCapabilities.SupportsPerLayerSync:
                throw new FeatureServiceCapabilityException(
                    "The feature service does not support createReplica with the perLayer sync model.");
            case CreateReplicaSyncModel.None when !syncCapabilities.SupportsSyncModelNone:
                throw new FeatureServiceCapabilityException(
                    "The feature service does not support createReplica with syncModel none.");
        }

        if (request.SyncModel == CreateReplicaSyncModel.None &&
            metadata.SupportedExportFormats.Count > 0 &&
            !metadata.SupportedExportFormats.Contains(
                MapCreateReplicaDataFormat(request.DataFormat),
                StringComparer.OrdinalIgnoreCase)) {
            throw new FeatureServiceCapabilityException(
                $"The feature service does not advertise support for createReplica data format '{MapCreateReplicaDataFormat(request.DataFormat)}'.");
        }
    }
}