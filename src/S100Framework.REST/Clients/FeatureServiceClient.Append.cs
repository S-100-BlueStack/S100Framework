using System.Globalization;
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

        var metadata = await GetMetadataAsync(cancellationToken);

        if (!metadata.Capabilities.SupportsAppend) {
            throw new FeatureServiceCapabilityException(
                "The feature service does not advertise append support.");
        }

        if (request.Upsert &&
            (metadata.Capabilities.SyncEnabled || metadata.Capabilities.SupportsChangeTracking)) {
            throw new FeatureServiceCapabilityException(
                "Append upsert is not supported when sync or change tracking is enabled on the feature service.");
        }

        var endpointUri = UriUtility.AppendPath(_serviceUri, "append");

        var dto = await PostFormAsync<EsriAppendSubmissionDto>(
            endpointUri,
            BuildAppendEditsParameters(request),
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

    private static Dictionary<string, string?> BuildAppendEditsParameters(
        FeatureServiceAppendEditsRequest request) {
        var parameters = new Dictionary<string, string?> {
            ["f"] = "json",
            ["layers"] = JsonSerializer.Serialize(request.Layers),
            ["edits"] = request.EditsJson,
            ["upsert"] = request.Upsert ? "true" : "false",
            ["useGlobalIds"] = request.UseGlobalIds ? "true" : "false",
            ["rollbackOnFailure"] = request.RollbackOnFailure ? "true" : "false"
        };

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

        return parameters;
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