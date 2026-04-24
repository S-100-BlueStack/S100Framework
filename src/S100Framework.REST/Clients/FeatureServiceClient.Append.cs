using System.Text.Json;
using System.Text.RegularExpressions;
using S100Framework.REST.Exceptions;
using S100Framework.REST.Internal.Dto;
using S100Framework.REST.Internal.Http;
using S100Framework.REST.Models;

namespace S100Framework.REST.Clients;

/// <summary>
/// Provides append-specific operations for a feature service root endpoint.
/// </summary>
public sealed partial class FeatureServiceClient
{
    private static readonly Regex AppendJobIdRegex = new(
        @"jobId:\s*(?<jobId>[A-Za-z0-9\-]+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <inheritdoc />
    public async Task<FeatureServiceAppendSubmissionResult> SubmitAppendAsync(
        FeatureServiceAppendEditsRequest request,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);

        request.Validate();

        await EnsureAppendSupportedAsync(request.Upsert, cancellationToken);

        var endpointUri = UriUtility.AppendPath(_serviceUri, "append");

        var dto = await PostFormAsync<EsriAppendSubmissionDto>(
            endpointUri,
            BuildAppendEditsParameters(request),
            cancellationToken);

        return MapAppendSubmissionResult(dto, endpointUri);
    }

    /// <summary>
    /// Submits a service-level <c>append</c> request using a portal item as the source.
    /// </summary>
    /// <param name="request">
    /// The append request to submit.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The append submission response.
    /// </returns>
    public async Task<FeatureServiceAppendSubmissionResult> SubmitAppendAsync(
        FeatureServiceAppendItemRequest request,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);

        request.Validate();

        await EnsureAppendSupportedAsync(request.Upsert, cancellationToken);

        var endpointUri = UriUtility.AppendPath(_serviceUri, "append");

        var dto = await PostFormAsync<EsriAppendSubmissionDto>(
            endpointUri,
            BuildAppendItemParameters(request),
            cancellationToken);

        return MapAppendSubmissionResult(dto, endpointUri);
    }

    /// <inheritdoc />
    public async Task<FeatureServiceAppendJobStatus> GetAppendStatusAsync(
        Uri statusUrl,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(statusUrl);

        var dto = await GetAsync<EsriAppendJobStatusDto>(statusUrl, cancellationToken);

        return MapAppendJobStatus(dto, statusUrl);
    }

    /// <inheritdoc />
    public async Task<FeatureServiceAppendJobStatus> WaitForAppendCompletionAsync(
        FeatureServiceAppendEditsRequest request,
        AppendWaitOptions? options = null,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);

        var submission = await SubmitAppendAsync(request, cancellationToken);

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
                UriUtility.AppendPath(_serviceUri, "append"));
        }

        return await WaitForAppendCompletionAsync(
            submission.StatusUrl,
            options,
            cancellationToken);
    }

    /// <summary>
    /// Submits a service-level <c>append</c> item request and waits until the job reaches a terminal state.
    /// </summary>
    /// <param name="request">
    /// The append request to submit.
    /// </param>
    /// <param name="options">
    /// Polling options. When <see langword="null"/>, default polling behavior is used.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The terminal append job status.
    /// </returns>
    public async Task<FeatureServiceAppendJobStatus> WaitForAppendCompletionAsync(
        FeatureServiceAppendItemRequest request,
        AppendWaitOptions? options = null,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);

        var submission = await SubmitAppendAsync(request, cancellationToken);

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
                UriUtility.AppendPath(_serviceUri, "append"));
        }

        return await WaitForAppendCompletionAsync(
            submission.StatusUrl,
            options,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<FeatureServiceAppendJobStatus> WaitForAppendCompletionAsync(
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

            var status = await GetAppendStatusAsync(statusUrl, cancellationToken);

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

    private async Task EnsureAppendSupportedAsync(
        bool upsert,
        CancellationToken cancellationToken) {
        var metadata = await GetMetadataAsync(cancellationToken);

        if (!metadata.Capabilities.SupportsAppend) {
            throw new FeatureServiceCapabilityException(
                "The feature service does not advertise append support.");
        }

        if (upsert &&
            (metadata.Capabilities.SyncEnabled || metadata.Capabilities.SupportsChangeTracking)) {
            throw new FeatureServiceCapabilityException(
                "Append upsert is not supported when sync or change tracking is enabled on the feature service.");
        }
    }

    private static Dictionary<string, string?> BuildAppendEditsParameters(
        FeatureServiceAppendEditsRequest request) {
        var parameters = BuildAppendCommonParameters(
            request.Layers,
            request.Upsert,
            request.UseGlobalIds,
            request.RollbackOnFailure,
            request.GdbVersion,
            request.ReturnEditMoment,
            request.TrueCurveClient,
            request.TimeReferenceUnknownClient);

        parameters["edits"] = request.EditsJson;

        return parameters;
    }

    private static Dictionary<string, string?> BuildAppendItemParameters(
        FeatureServiceAppendItemRequest request) {
        var parameters = BuildAppendCommonParameters(
            request.Layers,
            request.Upsert,
            request.UseGlobalIds,
            request.RollbackOnFailure,
            request.GdbVersion,
            request.ReturnEditMoment,
            request.TrueCurveClient,
            request.TimeReferenceUnknownClient);

        parameters["appendItemId"] = request.AppendItemId;

        if (request.AppendUploadFormat.HasValue) {
            parameters["appendUploadFormat"] = MapAppendSourceFormat(request.AppendUploadFormat.Value);
        }

        if (request.LayerMappings is { Count: > 0 }) {
            parameters["layerMappings"] = SerializeLayerMappings(request.LayerMappings);
        }

        return parameters;
    }

    private static Dictionary<string, string?> BuildAppendCommonParameters(
        IReadOnlyList<int> layers,
        bool upsert,
        bool useGlobalIds,
        bool rollbackOnFailure,
        string? gdbVersion,
        bool returnEditMoment,
        bool trueCurveClient,
        bool timeReferenceUnknownClient) {
        var parameters = new Dictionary<string, string?> {
            ["f"] = "json",
            ["layers"] = JsonSerializer.Serialize(layers),
            ["upsert"] = upsert ? "true" : "false",
            ["useGlobalIds"] = useGlobalIds ? "true" : "false",
            ["rollbackOnFailure"] = rollbackOnFailure ? "true" : "false"
        };

        if (!string.IsNullOrWhiteSpace(gdbVersion)) {
            parameters["gdbVersion"] = gdbVersion;
        }

        if (returnEditMoment) {
            parameters["returnEditMoment"] = "true";
        }

        if (trueCurveClient) {
            parameters["trueCurveClient"] = "true";
        }

        if (timeReferenceUnknownClient) {
            parameters["timeReferenceUnknownClient"] = "true";
        }

        return parameters;
    }

    private static string SerializeLayerMappings(
        IReadOnlyList<FeatureServiceAppendLayerMapping> layerMappings) {
        ArgumentNullException.ThrowIfNull(layerMappings);

        var payload = new List<Dictionary<string, object?>>(layerMappings.Count);

        foreach (var mapping in layerMappings) {
            ArgumentNullException.ThrowIfNull(mapping);

            var entry = new Dictionary<string, object?> {
                ["id"] = mapping.Id
            };

            if (mapping.SourceId.HasValue) {
                entry["sourceId"] = mapping.SourceId.Value;
            }

            if (!string.IsNullOrWhiteSpace(mapping.SourceTableName)) {
                entry["sourceTableName"] = mapping.SourceTableName;
            }

            if (mapping.FieldMappings is { Count: > 0 }) {
                entry["fieldMappings"] = mapping.FieldMappings
                    .Select(static fieldMapping => new Dictionary<string, string> {
                        ["name"] = fieldMapping.Name,
                        ["source"] = fieldMapping.Source
                    })
                    .ToArray();
            }

            payload.Add(entry);
        }

        return JsonSerializer.Serialize(payload);
    }

    private static string MapAppendSourceFormat(FeatureServiceAppendSourceFormat value) {
        return value switch {
            FeatureServiceAppendSourceFormat.Sqlite => "sqlite",
            FeatureServiceAppendSourceFormat.GeoPackage => "gpkg",
            FeatureServiceAppendSourceFormat.ShapeFile => "shapefile",
            FeatureServiceAppendSourceFormat.FileGeodatabase => "filegdb",
            FeatureServiceAppendSourceFormat.FeatureCollection => "feature Collection",
            FeatureServiceAppendSourceFormat.GeoJson => "geojson",
            FeatureServiceAppendSourceFormat.Csv => "csv",
            FeatureServiceAppendSourceFormat.Excel => "excel",
            FeatureServiceAppendSourceFormat.FeatureService => "feature Service",
            FeatureServiceAppendSourceFormat.Pbf => "pbf",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
        };
    }

    private FeatureServiceAppendSubmissionResult MapAppendSubmissionResult(
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
                UriUtility.AppendPath(_serviceUri, $"append/jobs/{jobId}"),
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

    private static FeatureServiceAppendJobStatus MapAppendJobStatus(
        EsriAppendJobStatusDto dto,
        Uri statusUrl) {
        var status = dto.Status;

        if (string.IsNullOrWhiteSpace(status)) {
            throw new FeatureServiceException(
                "The server returned an append job status payload without a status value.",
                statusUrl);
        }

        return new FeatureServiceAppendJobStatus(
            Status: status,
            LayerName: dto.LayerName,
            RecordCount: dto.RecordCount,
            SubmissionTime: dto.SubmissionTime,
            LastUpdatedTime: dto.LastUpdatedTime,
            EditMoment: dto.EditMoment);
    }

    private static string? TryExtractAppendJobId(string? statusMessage) {
        if (string.IsNullOrWhiteSpace(statusMessage)) {
            return null;
        }

        var match = AppendJobIdRegex.Match(statusMessage);

        return match.Success
            ? match.Groups["jobId"].Value
            : null;
    }
}