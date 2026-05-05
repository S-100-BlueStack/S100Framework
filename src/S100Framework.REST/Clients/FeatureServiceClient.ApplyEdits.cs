using System.Globalization;
using System.Text.Json;
using S100Framework.REST.Exceptions;
using S100Framework.REST.Internal.Dto;
using S100Framework.REST.Internal.EsriGeometry;
using S100Framework.REST.Internal.Http;
using S100Framework.REST.Models;

namespace S100Framework.REST.Clients;

/// <summary>
/// Provides layer-level and service-level <c>applyEdits</c> operations for feature service endpoints.
/// </summary>
public sealed partial class FeatureServiceClient
{
    internal async Task<ApplyEditsResult> WaitForLayerApplyEditsCompletionAsync(
        int layerId,
        FeatureEdits edits,
        ApplyEditsWaitOptions? options = null,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(edits);

        var submission = await SubmitApplyEditsAsync(layerId, edits, cancellationToken);

        if (submission.Result is not null) {
            return submission.Result;
        }

        if (submission.StatusUrl is null) {
            throw new FeatureServiceException(
                "The applyEdits submission did not return a statusUrl or a result payload.",
                UriUtility.AppendPath(
                    _serviceUri,
                    $"{layerId.ToString(CultureInfo.InvariantCulture)}/applyEdits"));
        }

        return await WaitForLayerApplyEditsCompletionAsync(
            submission.StatusUrl,
            options,
            cancellationToken);
    }

    internal Task<ApplyEditsResult> WaitForLayerApplyEditsCompletionAsync(
        Uri statusUrl,
        ApplyEditsWaitOptions? options = null,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(statusUrl);

        return WaitForApplyEditsJobCompletionAsync(
            statusUrl,
            options,
            GetLayerApplyEditsStatusAsync,
            GetLayerApplyEditsResultAsync,
            cancellationToken);
    }

    internal async Task<ApplyEditsResult> ApplyEditsAsync(
        int layerId,
        FeatureEdits edits,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(edits);

        edits.Validate();

        var endpointUri = UriUtility.AppendPath(
            _serviceUri,
            $"{layerId.ToString(CultureInfo.InvariantCulture)}/applyEdits");

        var dto = await PostFormAsync<EsriApplyEditsResponseDto>(
            endpointUri,
            BuildLayerApplyEditsParameters(edits, applyAsync: false),
            cancellationToken);

        return MapApplyEditsResult(dto);
    }

    internal async Task<ApplyEditsSubmissionResult> SubmitApplyEditsAsync(
        int layerId,
        FeatureEdits edits,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(edits);

        edits.Validate();

        var endpointUri = UriUtility.AppendPath(
            _serviceUri,
            $"{layerId.ToString(CultureInfo.InvariantCulture)}/applyEdits");

        var document = await PostFormAsync<JsonDocument>(
            endpointUri,
            BuildLayerApplyEditsParameters(edits, applyAsync: true),
            cancellationToken);

        var root = document.RootElement;

        if (root.TryGetProperty("statusUrl", out var statusUrlElement)) {
            var rawStatusUrl = statusUrlElement.GetString();

            if (string.IsNullOrWhiteSpace(rawStatusUrl)) {
                throw new FeatureServiceException(
                    "The server returned an empty statusUrl for applyEdits.",
                    endpointUri);
            }

            return new ApplyEditsSubmissionResult(
                Result: null,
                StatusUrl: new Uri(rawStatusUrl, UriKind.Absolute));
        }

        return new ApplyEditsSubmissionResult(
            Result: MapApplyEditsResult(root, endpointUri),
            StatusUrl: null);
    }

    internal async Task<ApplyEditsJobStatus> GetLayerApplyEditsStatusAsync(
        Uri statusUrl,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(statusUrl);

        var document = await GetAsync<JsonDocument>(statusUrl, cancellationToken);
        var root = document.RootElement;

        return new ApplyEditsJobStatus(
            Status: root.TryGetProperty("status", out var statusElement)
                ? statusElement.GetString() ?? "Unknown"
                : "Unknown",
            ResultUrl: root.TryGetProperty("resultUrl", out var resultUrlElement) &&
                       !string.IsNullOrWhiteSpace(resultUrlElement.GetString())
                ? new Uri(resultUrlElement.GetString()!, UriKind.Absolute)
                : null,
            SubmissionTime: root.TryGetProperty("submissionTime", out var submissionTimeElement) &&
                            submissionTimeElement.TryGetInt64(out var submissionTime)
                ? submissionTime
                : null,
            LastUpdatedTime: root.TryGetProperty("lastUpdatedTime", out var lastUpdatedTimeElement) &&
                             lastUpdatedTimeElement.TryGetInt64(out var lastUpdatedTime)
                ? lastUpdatedTime
                : null);
    }

    internal async Task<ApplyEditsResult> GetLayerApplyEditsResultAsync(
        Uri resultUrl,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(resultUrl);

        var document = await GetAsync<JsonDocument>(resultUrl, cancellationToken);

        return MapApplyEditsResult(document.RootElement, resultUrl);
    }

    private static Dictionary<string, string?> BuildLayerApplyEditsParameters(
        FeatureEdits edits,
        bool applyAsync) {
        return new Dictionary<string, string?> {
            ["f"] = "json",
            ["rollbackOnFailure"] = edits.RollbackOnFailure ? "true" : "false",
            ["useGlobalIds"] = edits.UseGlobalIds ? "true" : "false",
            ["async"] = applyAsync ? "true" : null,
            ["adds"] = edits.Adds is { Count: > 0 }
                ? EsriEditGeometryWriter.WriteFeatures(edits.Adds)
                : null,
            ["updates"] = edits.Updates is { Count: > 0 }
                ? EsriEditGeometryWriter.WriteFeatures(edits.Updates)
                : null,
            ["deletes"] = edits.Deletes is { Count: > 0 }
                ? string.Join(",", edits.Deletes)
                : null
        };
    }

    private async Task<TResult> WaitForApplyEditsJobCompletionAsync<TResult>(
        Uri statusUrl,
        ApplyEditsWaitOptions? options,
        Func<Uri, CancellationToken, Task<ApplyEditsJobStatus>> getStatusAsync,
        Func<Uri, CancellationToken, Task<TResult>> getResultAsync,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(statusUrl);
        ArgumentNullException.ThrowIfNull(getStatusAsync);
        ArgumentNullException.ThrowIfNull(getResultAsync);

        var effectiveOptions = GetValidatedApplyEditsWaitOptions(options);
        var startedAt = DateTimeOffset.UtcNow;

        while (true) {
            cancellationToken.ThrowIfCancellationRequested();

            var status = await getStatusAsync(statusUrl, cancellationToken);

            if (status.IsCompleted) {
                if (status.ResultUrl is null) {
                    throw new FeatureServiceException(
                        "The applyEdits job completed without a resultUrl.",
                        statusUrl);
                }

                return await getResultAsync(status.ResultUrl, cancellationToken);
            }

            if (status.IsTerminal) {
                throw new FeatureServiceException(
                    $"The applyEdits job ended with terminal status '{status.Status}'.",
                    statusUrl);
            }

            if (effectiveOptions.Timeout is { } timeout &&
                DateTimeOffset.UtcNow - startedAt >= timeout) {
                throw new TimeoutException(
                    $"The applyEdits job did not complete within {timeout}.");
            }

            if (effectiveOptions.PollInterval > TimeSpan.Zero) {
                await Task.Delay(effectiveOptions.PollInterval, cancellationToken);
            }
            else {
                await Task.Yield();
            }
        }
    }

    private static ApplyEditsWaitOptions GetValidatedApplyEditsWaitOptions(
        ApplyEditsWaitOptions? options) {
        var effectiveOptions = options ?? new ApplyEditsWaitOptions();

        if (effectiveOptions.PollInterval < TimeSpan.Zero) {
            throw new ArgumentOutOfRangeException(
                nameof(ApplyEditsWaitOptions.PollInterval),
                "PollInterval cannot be negative.");
        }

        if (effectiveOptions.Timeout is { } timeout && timeout < TimeSpan.Zero) {
            throw new ArgumentOutOfRangeException(
                nameof(ApplyEditsWaitOptions.Timeout),
                "Timeout cannot be negative.");
        }

        return effectiveOptions;
    }

    private static ApplyEditsResult MapApplyEditsResult(
        JsonElement root,
        Uri endpointUri) {
        var dto = root.Deserialize<EsriApplyEditsResponseDto>(JsonOptions)
            ?? throw new FeatureServiceException(
                "The applyEdits payload could not be deserialized.",
                endpointUri);

        return MapApplyEditsResult(dto);
    }

    private static ApplyEditsResult MapApplyEditsResult(
     EsriApplyEditsResponseDto dto) {
        return new ApplyEditsResult(
            MapApplyEditsResults(dto.AddResults),
            MapApplyEditsResults(dto.UpdateResults),
            MapApplyEditsResults(dto.DeleteResults));
    }

    private static IReadOnlyList<EditResult> MapApplyEditsResults(
     IEnumerable<EsriEditResultDto?>? results) {
        if (results is null) {
            return Array.Empty<EditResult>();
        }

        var mappedResults = new List<EditResult>();

        foreach (var result in results) {
            if (result is null) {
                continue;
            }

            mappedResults.Add(MapEditResult(result));
        }

        return mappedResults;
    }

    private static EditResult MapEditResult(EsriEditResultDto dto) {
        return new EditResult(
            dto.Success,
            dto.ObjectId,
            dto.GlobalId,
            dto.Error?.Code,
            dto.Error?.Description);
    }

    /// <inheritdoc />
    public async Task<FeatureServiceApplyEditsResult> ApplyEditsAsync(
        FeatureServiceEdits edits,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(edits);

        edits.Validate();

        var endpointUri = UriUtility.AppendPath(_serviceUri, "applyEdits");
        var dto = await PostFormAsync<List<EsriServiceLayerEditResultsDto>>(
            endpointUri,
            BuildServiceApplyEditsParameters(edits, applyAsync: false),
            cancellationToken);

        return MapServiceApplyEditsResult(dto);
    }

    /// <inheritdoc />
    public async Task<FeatureServiceApplyEditsSubmissionResult> SubmitApplyEditsAsync(
        FeatureServiceEdits edits,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(edits);

        edits.Validate();

        var metadata = await GetMetadataAsync(cancellationToken);

        if (!metadata.Capabilities.SupportsAsyncApplyEdits) {
            throw new FeatureServiceCapabilityException(
                "The feature service does not support asynchronous applyEdits.");
        }

        var endpointUri = UriUtility.AppendPath(_serviceUri, "applyEdits");
        var document = await PostFormAsync<JsonDocument>(
            endpointUri,
            BuildServiceApplyEditsParameters(edits, applyAsync: true),
            cancellationToken);

        var root = document.RootElement;

        if (root.ValueKind == JsonValueKind.Array) {
            return new FeatureServiceApplyEditsSubmissionResult(
                Result: MapServiceApplyEditsResult(root, endpointUri),
                StatusUrl: null);
        }

        if (root.ValueKind != JsonValueKind.Object) {
            throw new FeatureServiceException(
                "The applyEdits payload had an unexpected JSON shape.",
                endpointUri);
        }

        if (root.TryGetProperty("statusUrl", out var statusUrlElement)) {
            var rawStatusUrl = statusUrlElement.GetString();

            if (string.IsNullOrWhiteSpace(rawStatusUrl)) {
                throw new FeatureServiceException(
                    "The server returned an empty statusUrl for applyEdits.",
                    endpointUri);
            }

            return new FeatureServiceApplyEditsSubmissionResult(
                Result: null,
                StatusUrl: new Uri(rawStatusUrl, UriKind.Absolute));
        }

        return new FeatureServiceApplyEditsSubmissionResult(
            Result: MapServiceApplyEditsResult(root, endpointUri),
            StatusUrl: null);
    }

    /// <inheritdoc />
    public async Task<ApplyEditsJobStatus> GetApplyEditsStatusAsync(
        Uri statusUrl,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(statusUrl);

        var document = await GetAsync<JsonDocument>(statusUrl, cancellationToken);
        var root = document.RootElement;

        return new ApplyEditsJobStatus(
            Status: root.TryGetProperty("status", out var statusElement)
                ? statusElement.GetString() ?? "Unknown"
                : "Unknown",
            ResultUrl: root.TryGetProperty("resultUrl", out var resultUrlElement) &&
                       !string.IsNullOrWhiteSpace(resultUrlElement.GetString())
                ? new Uri(resultUrlElement.GetString()!, UriKind.Absolute)
                : null,
            SubmissionTime: root.TryGetProperty("submissionTime", out var submissionTimeElement) &&
                            submissionTimeElement.TryGetInt64(out var submissionTime)
                ? submissionTime
                : null,
            LastUpdatedTime: root.TryGetProperty("lastUpdatedTime", out var lastUpdatedTimeElement) &&
                             lastUpdatedTimeElement.TryGetInt64(out var lastUpdatedTime)
                ? lastUpdatedTime
                : null);
    }

    /// <inheritdoc />
    public async Task<FeatureServiceApplyEditsResult> GetApplyEditsResultAsync(
        Uri resultUrl,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(resultUrl);

        var document = await GetAsync<JsonDocument>(resultUrl, cancellationToken);

        return MapServiceApplyEditsResult(document.RootElement, resultUrl);
    }

    private static Dictionary<string, string?> BuildServiceApplyEditsParameters(
        FeatureServiceEdits edits,
        bool applyAsync) {
        return new Dictionary<string, string?> {
            ["f"] = "json",
            ["rollbackOnFailure"] = edits.RollbackOnFailure ? "true" : "false",
            ["useGlobalIds"] = edits.UseGlobalIds ? "true" : "false",
            ["async"] = applyAsync ? "true" : null,
            ["edits"] = SerializeServiceEdits(edits)
        };
    }

    private static FeatureServiceApplyEditsResult MapServiceApplyEditsResult(
        JsonElement root,
        Uri endpointUri) {
        var dto = root.Deserialize<List<EsriServiceLayerEditResultsDto>>(JsonOptions)
            ?? throw new FeatureServiceException(
                "The applyEdits payload could not be deserialized.",
                endpointUri);

        return MapServiceApplyEditsResult(dto);
    }

    private static FeatureServiceApplyEditsResult MapServiceApplyEditsResult(
        IReadOnlyList<EsriServiceLayerEditResultsDto>? dto) {
        return new FeatureServiceApplyEditsResult(
            dto?.Select(MapServiceLayerEditResults).ToArray() ?? Array.Empty<ServiceLayerEditResults>());
    }

    private static string SerializeServiceEdits(FeatureServiceEdits edits) {
        var payload = edits.Layers
            .Select(layer => SerializeServiceLayerEdits(layer, edits.UseGlobalIds))
            .ToArray();

        return JsonSerializer.Serialize(payload);
    }

    private static Dictionary<string, object?> SerializeServiceLayerEdits(
        ServiceLayerEdits layer,
        bool useGlobalIds) {
        var payload = new Dictionary<string, object?> {
            ["id"] = layer.LayerId
        };

        if (layer.Adds is { Count: > 0 }) {
            payload["adds"] = JsonSerializer.Deserialize<JsonElement>(
                EsriEditGeometryWriter.WriteFeatures(layer.Adds));
        }

        if (layer.Updates is { Count: > 0 }) {
            payload["updates"] = JsonSerializer.Deserialize<JsonElement>(
                EsriEditGeometryWriter.WriteFeatures(layer.Updates));
        }

        if (useGlobalIds) {
            if (layer.DeleteGlobalIds is { Count: > 0 }) {
                payload["deletes"] = layer.DeleteGlobalIds;
            }
        }
        else {
            if (layer.DeleteObjectIds is { Count: > 0 }) {
                payload["deletes"] = layer.DeleteObjectIds;
            }
        }

        return payload;
    }

    private static ServiceLayerEditResults MapServiceLayerEditResults(
    EsriServiceLayerEditResultsDto dto) {
        return new ServiceLayerEditResults(
            dto.Id,
            MapApplyEditsResults(dto.AddResults),
            MapApplyEditsResults(dto.UpdateResults),
            MapApplyEditsResults(dto.DeleteResults));
    }

    /// <inheritdoc />
    public async Task<FeatureServiceApplyEditsResult> WaitForApplyEditsCompletionAsync(
        FeatureServiceEdits edits,
        ApplyEditsWaitOptions? options = null,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(edits);

        var submission = await SubmitApplyEditsAsync(edits, cancellationToken);

        if (submission.Result is not null) {
            return submission.Result;
        }

        if (submission.StatusUrl is null) {
            throw new FeatureServiceException(
                "The applyEdits submission did not return a statusUrl or a result payload.",
                UriUtility.AppendPath(_serviceUri, "applyEdits"));
        }

        return await WaitForApplyEditsCompletionAsync(
            submission.StatusUrl,
            options,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<FeatureServiceApplyEditsResult> WaitForApplyEditsCompletionAsync(
        Uri statusUrl,
        ApplyEditsWaitOptions? options = null,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(statusUrl);

        return WaitForApplyEditsJobCompletionAsync(
            statusUrl,
            options,
            GetApplyEditsStatusAsync,
            GetApplyEditsResultAsync,
            cancellationToken);
    }
}