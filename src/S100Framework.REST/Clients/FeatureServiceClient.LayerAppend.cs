using System.Globalization;
using System.Text.Json;
using S100Framework.REST.Exceptions;
using S100Framework.REST.Internal.Dto;
using S100Framework.REST.Internal.Http;
using S100Framework.REST.Models;

namespace S100Framework.REST.Clients;

/// <summary>
/// Provides layer-level append operations for feature layer endpoints.
/// </summary>
public sealed partial class FeatureServiceClient
{
    internal async Task<FeatureServiceAppendSubmissionResult> SubmitLayerAppendAsync(
        int layerId,
        FeatureLayerAppendEditsRequest request,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);

        request.Validate();

        await EnsureLayerAppendSupportedAsync(layerId, request.Upsert, cancellationToken);

        var endpointUri = CreateLayerAppendEndpointUri(layerId);

        var dto = await PostFormAsync<EsriAppendSubmissionDto>(
            endpointUri,
            BuildLayerAppendEditsParameters(request),
            cancellationToken);

        return MapLayerAppendSubmissionResult(layerId, dto, endpointUri);
    }

    internal async Task<FeatureServiceAppendSubmissionResult> SubmitLayerAppendAsync(
        int layerId,
        FeatureLayerAppendItemRequest request,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);

        request.Validate();

        await EnsureLayerAppendSupportedAsync(layerId, request.Upsert, cancellationToken);

        var endpointUri = CreateLayerAppendEndpointUri(layerId);

        var dto = await PostFormAsync<EsriAppendSubmissionDto>(
            endpointUri,
            BuildLayerAppendItemParameters(request),
            cancellationToken);

        return MapLayerAppendSubmissionResult(layerId, dto, endpointUri);
    }

    internal async Task<FeatureServiceAppendSubmissionResult> SubmitLayerAppendAsync(
        int layerId,
        FeatureLayerAppendUploadRequest request,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);

        request.Validate();

        await EnsureLayerAppendSupportedAsync(layerId, request.Upsert, cancellationToken);

        var endpointUri = CreateLayerAppendEndpointUri(layerId);

        var dto = await PostFormAsync<EsriAppendSubmissionDto>(
            endpointUri,
            BuildLayerAppendUploadParameters(request),
            cancellationToken);

        return MapLayerAppendSubmissionResult(layerId, dto, endpointUri);
    }

    internal async Task<FeatureServiceAppendJobStatus> GetLayerAppendStatusAsync(
        Uri statusUrl,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(statusUrl);

        var dto = await GetAsync<EsriAppendJobStatusDto>(statusUrl, cancellationToken);

        return MapAppendJobStatus(dto, statusUrl);
    }

    internal async Task<FeatureServiceAppendJobStatus> WaitForLayerAppendCompletionAsync(
        int layerId,
        FeatureLayerAppendEditsRequest request,
        AppendWaitOptions? options = null,
        CancellationToken cancellationToken = default) {
        var submission = await SubmitLayerAppendAsync(layerId, request, cancellationToken);

        return await WaitForLayerAppendSubmissionAsync(layerId, submission, options, cancellationToken);
    }

    internal async Task<FeatureServiceAppendJobStatus> WaitForLayerAppendCompletionAsync(
        int layerId,
        FeatureLayerAppendItemRequest request,
        AppendWaitOptions? options = null,
        CancellationToken cancellationToken = default) {
        var submission = await SubmitLayerAppendAsync(layerId, request, cancellationToken);

        return await WaitForLayerAppendSubmissionAsync(layerId, submission, options, cancellationToken);
    }

    internal async Task<FeatureServiceAppendJobStatus> WaitForLayerAppendCompletionAsync(
        int layerId,
        FeatureLayerAppendUploadRequest request,
        AppendWaitOptions? options = null,
        CancellationToken cancellationToken = default) {
        var submission = await SubmitLayerAppendAsync(layerId, request, cancellationToken);

        return await WaitForLayerAppendSubmissionAsync(layerId, submission, options, cancellationToken);
    }

    internal async Task<FeatureServiceAppendJobStatus> WaitForLayerAppendCompletionAsync(
        Uri statusUrl,
        AppendWaitOptions? options = null,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(statusUrl);

        options ??= new AppendWaitOptions();

        if (options.PollInterval <= TimeSpan.Zero) {
            throw new InvalidOperationException("PollInterval must be greater than zero.");
        }

        if (options.Timeout.HasValue && options.Timeout.Value <= TimeSpan.Zero) {
            throw new InvalidOperationException("Timeout must be greater than zero when provided.");
        }

        var startedAt = DateTimeOffset.UtcNow;

        while (true) {
            cancellationToken.ThrowIfCancellationRequested();

            var status = await GetLayerAppendStatusAsync(statusUrl, cancellationToken);

            if (status.IsTerminal) {
                return status;
            }

            if (options.Timeout.HasValue &&
                DateTimeOffset.UtcNow - startedAt >= options.Timeout.Value) {
                throw new TimeoutException(
                    $"The append job did not reach a terminal state within {options.Timeout.Value}.");
            }

            await Task.Delay(options.PollInterval, cancellationToken);
        }
    }

    private async Task EnsureLayerAppendSupportedAsync(
        int layerId,
        bool upsert,
        CancellationToken cancellationToken) {
        if (layerId < 0) {
            throw new ArgumentOutOfRangeException(nameof(layerId), layerId, "Layer ID must not be negative.");
        }

        var metadata = await GetMetadataAsync(cancellationToken);

        if (upsert &&
            (metadata.Capabilities.SyncEnabled || metadata.Capabilities.SupportsChangeTracking)) {
            throw new FeatureServiceCapabilityException(
                "Append upsert is not supported when sync or change tracking is enabled for the feature service.");
        }

        var schema = await GetLayerSchemaAsync(layerId, cancellationToken);

        if (!schema.Capabilities.SupportsAppend) {
            throw new FeatureServiceCapabilityException(
                $"Layer {layerId.ToString(CultureInfo.InvariantCulture)} does not advertise append support.");
        }
    }

    private async Task<FeatureServiceAppendJobStatus> WaitForLayerAppendSubmissionAsync(
        int layerId,
        FeatureServiceAppendSubmissionResult submission,
        AppendWaitOptions? options,
        CancellationToken cancellationToken) {
        if (submission.IsTerminal) {
            return new FeatureServiceAppendJobStatus(
                submission.Status,
                LayerName: null,
                RecordCount: null,
                SubmissionTime: null,
                LastUpdatedTime: null,
                EditMoment: submission.EditMoment);
        }

        if (submission.StatusUrl is null) {
            throw new FeatureServiceException(
                "The append submission did not return a status URL for a non-terminal job.",
                CreateLayerAppendEndpointUri(layerId));
        }

        var result = await WaitForLayerAppendCompletionAsync(
            submission.StatusUrl,
            options,
            cancellationToken);

        if (!result.IsCompleted) {
            throw new FeatureServiceException(
                $"The append job ended with status '{result.Status}'.",
                submission.StatusUrl);
        }

        return result;
    }

    private Uri CreateLayerAppendEndpointUri(int layerId) {
        return UriUtility.AppendPath(
            UriUtility.AppendPath(_serviceUri, layerId.ToString(CultureInfo.InvariantCulture)),
            "append");
    }

    private static Dictionary<string, string?> BuildLayerAppendEditsParameters(
        FeatureLayerAppendEditsRequest request) {
        var parameters = BuildLayerAppendCommonParameters(request);
        parameters["edits"] = request.EditsJson;

        return parameters;
    }

    private static Dictionary<string, string?> BuildLayerAppendItemParameters(
        FeatureLayerAppendItemRequest request) {
        var parameters = BuildLayerAppendCommonParameters(request);
        parameters["appendItemId"] = request.AppendItemId;

        if (request.AppendUploadFormat.HasValue) {
            parameters["appendUploadFormat"] = MapAppendSourceFormat(request.AppendUploadFormat.Value);
        }

        return parameters;
    }

    private static Dictionary<string, string?> BuildLayerAppendUploadParameters(
        FeatureLayerAppendUploadRequest request) {
        var parameters = BuildLayerAppendCommonParameters(request);
        parameters["appendUploadId"] = request.AppendUploadId;
        parameters["appendUploadFormat"] = MapAppendSourceFormat(request.AppendUploadFormat!.Value);

        return parameters;
    }

    private static Dictionary<string, string?> BuildLayerAppendCommonParameters(
        FeatureLayerAppendRequestBase request) {
        var parameters = new Dictionary<string, string?> {
            ["f"] = "json",
            ["sourceTableName"] = request.SourceTableName,
            ["upsert"] = request.Upsert ? "true" : "false",
            ["useGlobalIds"] = request.UseGlobalIds ? "true" : "false",
            ["rollbackOnFailure"] = request.RollbackOnFailure ? "true" : "false"
        };

        if (request.FieldMappings is { Count: > 0 }) {
            parameters["fieldMappings"] = SerializeFieldMappings(request.FieldMappings);
        }

        if (!string.IsNullOrWhiteSpace(request.AppendSourceInfoJson)) {
            parameters["appendSourceInfo"] = request.AppendSourceInfoJson;
        }

        if (!string.IsNullOrWhiteSpace(request.AppendSourceFilterJson)) {
            parameters["appendSourceFilter"] = request.AppendSourceFilterJson;
        }

        if (request.SkipInserts) {
            parameters["skipInserts"] = "true";
        }

        if (request.SkipUpdates) {
            parameters["skipUpdates"] = "true";
        }

        if (request.TruncateExisting) {
            parameters["truncateExisting"] = "true";
        }

        if (request.UpdateGeometry.HasValue) {
            parameters["updateGeometry"] = request.UpdateGeometry.Value ? "true" : "false";
        }

        if (request.AppendFields is { Count: > 0 }) {
            parameters["appendFields"] = JsonSerializer.Serialize(request.AppendFields, JsonOptions);
        }

        if (!string.IsNullOrWhiteSpace(request.UpsertMatchingField)) {
            parameters["upsertMatchingField"] = request.UpsertMatchingField;
        }

        if (!string.IsNullOrWhiteSpace(request.GdbVersion)) {
            parameters["gdbVersion"] = request.GdbVersion;
        }

        if (request.ReturnEditMoment) {
            parameters["returnEditMoment"] = "true";
        }

        if (request.TrueCurveClient) {
            parameters["trueCurveClient"] = "true";
        }

        if (request.TimeReferenceUnknownClient) {
            parameters["timeReferenceUnknownClient"] = "true";
        }

        if (!string.IsNullOrWhiteSpace(request.AppendAttachmentsInfoJson)) {
            parameters["appendAttachmentsInfo"] = request.AppendAttachmentsInfoJson;
        }

        if (request.LayerMappings is { Count: > 0 }) {
            parameters["layerMappings"] = SerializeLayerMappings(request.LayerMappings);
        }

        return parameters;
    }

    private static string SerializeFieldMappings(
        IReadOnlyList<FeatureServiceAppendFieldMapping> fieldMappings) {
        var payload = fieldMappings.Select(static fieldMapping => new Dictionary<string, string> {
            ["name"] = fieldMapping.Name,
            ["source"] = fieldMapping.Source
        });

        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    private FeatureServiceAppendSubmissionResult MapLayerAppendSubmissionResult(
        int layerId,
        EsriAppendSubmissionDto dto,
        Uri endpointUri) {
        var status = dto.Status;

        if (string.IsNullOrWhiteSpace(status)) {
            throw new FeatureServiceException(
                "The server returned an append response without a status value.",
                endpointUri);
        }

        var jobId = TryExtractAppendJobId(dto.StatusMessage);

        Uri? statusUrl = null;

        if (!string.IsNullOrWhiteSpace(jobId)) {
            statusUrl = UriUtility.WithQuery(
                UriUtility.AppendPath(CreateLayerAppendEndpointUri(layerId), $"jobs/{jobId}"),
                new Dictionary<string, string?> {
                    ["f"] = "json"
                });
        }

        return new FeatureServiceAppendSubmissionResult(
            Status: status,
            StatusMessage: dto.StatusMessage,
            ItemId: dto.ItemId,
            JobId: jobId,
            StatusUrl: statusUrl,
            EditMoment: dto.EditMoment);
    }
}