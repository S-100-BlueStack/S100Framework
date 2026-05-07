using System.Globalization;
using System.Text.Json;
using NetTopologySuite.Geometries;
using S100Framework.REST.Exceptions;
using S100Framework.REST.Internal.Dto;
using S100Framework.REST.Internal.EsriGeometry;
using S100Framework.REST.Internal.Http;
using S100Framework.REST.Internal.Json;
using S100Framework.REST.Models;

namespace S100Framework.REST.Clients;

/// <summary>
/// Provides <c>extractChanges</c> operations for feature service endpoints.
/// </summary>
public sealed partial class FeatureServiceClient
{
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
            if (statusUrlElement.ValueKind != JsonValueKind.String) {
                throw new FeatureServiceException(
                    "The server returned an invalid statusUrl for extractChanges.",
                    endpointUri);
            }

            var rawStatusUrl = statusUrlElement.GetString();

            if (string.IsNullOrWhiteSpace(rawStatusUrl)) {
                throw new FeatureServiceException(
                    "The server returned an empty statusUrl for extractChanges.",
                    endpointUri);
            }

            if (!Uri.TryCreate(rawStatusUrl, UriKind.Absolute, out var statusUrl)) {
                throw new FeatureServiceException(
                    "The server returned an invalid statusUrl for extractChanges.",
                    endpointUri);
            }

            return new ExtractChangesSubmissionResult(
                Result: null,
                StatusUrl: statusUrl);
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

        return new ExtractChangesFileResult(bytes, contentType, fileName, resultUrl);
    }

    /// <inheritdoc />
    public async Task<ExtractChangesJobStatus> GetExtractChangesStatusAsync(
        Uri statusUrl,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(statusUrl);

        var dto = await GetAsync<EsriExtractChangesJobStatusDto>(statusUrl, cancellationToken);

        Uri? resultUrl = null;

        if (!string.IsNullOrWhiteSpace(dto.ResultUrl) &&
            !Uri.TryCreate(dto.ResultUrl, UriKind.Absolute, out resultUrl)) {
            throw new FeatureServiceException(
                "The server returned an invalid resultUrl for extractChanges.",
                statusUrl);
        }

        return new ExtractChangesJobStatus(
            Status: dto.Status ?? "Unknown",
            ResponseType: dto.ResponseType,
            TransportType: dto.TransportType,
            ResultUrl: resultUrl,
            SubmissionTime: dto.SubmissionTime,
            LastUpdatedTime: dto.LastUpdatedTime);
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

        foreach (var editDto in dto.Edits ?? Enumerable.Empty<EsriExtractChangesLayerEditsDto?>()) {
            if (editDto is null) {
                continue;
            }

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
            (dto.LayerServerGens ?? Enumerable.Empty<EsriLayerServerGenDto?>())
                .Where(static layerServerGen => layerServerGen is not null)
                .Select(static layerServerGen => MapLayerServerGen(layerServerGen!))
                .ToArray(),
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
            new Envelope(
                dto.XMin.Value,
                dto.XMax.Value,
                dto.YMin.Value,
                dto.YMax.Value),
            srid);
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
        IEnumerable<EsriFeatureDto?>? features) {
        if (features is null) {
            return Array.Empty<FeatureRecord>();
        }

        return features
            .Where(static feature => feature is not null)
            .Select(feature => MapExtractChangesFeature(schema, feature!))
            .ToArray();
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
        return JsonAttributeValueReader.ReadAttributes(
            attributesElement,
            JsonAttributeNumberHandling.DoubleFallback);
    }

    private static IReadOnlyList<object?> ReadJsonValueList(List<JsonElement>? elements) {
        if (elements is null || elements.Count == 0) {
            return Array.Empty<object?>();
        }

        return elements
            .Select(static element => JsonAttributeValueReader.ConvertValue(
                element,
                JsonAttributeNumberHandling.DoubleFallback))
            .ToArray();
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