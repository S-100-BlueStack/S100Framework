using S100Framework.REST.Exceptions;
using S100Framework.REST.Internal.Dto;
using S100Framework.REST.Models;

namespace S100Framework.REST.Clients;

/// <summary>
/// Provides layer-level analytic query wrapper methods for <see cref="FeatureLayerClient" />.
/// </summary>
public sealed partial class FeatureLayerClient
{
    /// <inheritdoc />
    public async Task<QueryAnalyticResult> QueryAnalyticAsync(
        QueryAnalyticRequest request,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);

        request.Validate();

        var schema = await GetSchemaAsync(cancellationToken);
        EnsureQueryAnalyticSupported(schema);

        var response = await _serviceClient.QueryAnalyticAsync(_layerId, request, cancellationToken);

        return MapQueryAnalyticResult(schema, response);
    }

    /// <inheritdoc />
    public async Task<QueryAnalyticSubmissionResult> SubmitQueryAnalyticAsync(
        QueryAnalyticRequest request,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);

        request.Validate();

        var schema = await GetSchemaAsync(cancellationToken);
        EnsureQueryAnalyticSupported(schema);
        EnsureAsyncQueryAnalyticSupported(schema);

        var submission = await _serviceClient.SubmitQueryAnalyticAsync(
            _layerId,
            request,
            cancellationToken);

        return new QueryAnalyticSubmissionResult(
            submission.Result is null
                ? null
                : MapQueryAnalyticResult(schema, submission.Result),
            submission.StatusUrl);
    }

    /// <inheritdoc />
    public Task<QueryAnalyticJobStatus> GetQueryAnalyticStatusAsync(
        Uri statusUrl,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(statusUrl);

        return _serviceClient.GetQueryAnalyticStatusAsync(statusUrl, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<QueryAnalyticResult> GetQueryAnalyticResultAsync(
        Uri resultUrl,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(resultUrl);

        var schema = await GetSchemaAsync(cancellationToken);
        var response = await _serviceClient.GetQueryAnalyticResultAsync(resultUrl, cancellationToken);

        return MapQueryAnalyticResult(schema, response);
    }

    /// <inheritdoc />
    public async Task<QueryAnalyticResult> WaitForQueryAnalyticCompletionAsync(
        QueryAnalyticRequest request,
        QueryAnalyticWaitOptions? options = null,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);

        var submission = await SubmitQueryAnalyticAsync(request, cancellationToken);

        if (submission.Result is not null) {
            return submission.Result;
        }

        if (submission.StatusUrl is null) {
            throw new FeatureServiceException(
                "The queryAnalytic submission did not return a status URL or a result payload.",
                new Uri(_serviceClient.Options.ServiceUri!, $"{_layerId}/queryAnalytic"));
        }

        return await WaitForQueryAnalyticCompletionAsync(
            submission.StatusUrl,
            options,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<QueryAnalyticResult> WaitForQueryAnalyticCompletionAsync(
        Uri statusUrl,
        QueryAnalyticWaitOptions? options = null,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(statusUrl);

        var effectiveOptions = GetValidatedQueryAnalyticWaitOptions(options);
        var startedAt = DateTimeOffset.UtcNow;

        while (true) {
            cancellationToken.ThrowIfCancellationRequested();

            var status = await GetQueryAnalyticStatusAsync(statusUrl, cancellationToken);

            if (status.IsCompleted) {
                if (status.ResultUrl is null) {
                    throw new FeatureServiceException(
                        "The queryAnalytic job completed without a result URL.",
                        statusUrl);
                }

                return await GetQueryAnalyticResultAsync(status.ResultUrl, cancellationToken);
            }

            if (status.IsTerminal) {
                throw new FeatureServiceException(
                    $"The queryAnalytic job ended with terminal status '{status.Status}'.",
                    statusUrl);
            }

            if (effectiveOptions.Timeout is { } timeout &&
                DateTimeOffset.UtcNow - startedAt >= timeout) {
                throw new TimeoutException(
                    $"The queryAnalytic job did not complete within {timeout}.");
            }

            if (effectiveOptions.PollInterval > TimeSpan.Zero) {
                await Task.Delay(effectiveOptions.PollInterval, cancellationToken);
            }
            else {
                await Task.Yield();
            }
        }
    }

    private QueryAnalyticResult MapQueryAnalyticResult(
        FeatureLayerSchema schema,
        EsriQueryResponseDto response) {
        return new QueryAnalyticResult(
            (response.Features ?? Enumerable.Empty<EsriFeatureDto?>())
                .Where(static feature => feature is not null)
                .Select(feature => MapFeature(schema, feature!))
                .ToArray(),
            response.ExceededTransferLimit);
    }

    private static void EnsureQueryAnalyticSupported(FeatureLayerSchema schema) {
        if (!schema.Capabilities.SupportsQueryAnalytic) {
            throw new FeatureServiceCapabilityException(
                $"Layer '{schema.Name}' ({schema.LayerId}) does not advertise queryAnalytic support.");
        }
    }

    private static void EnsureAsyncQueryAnalyticSupported(FeatureLayerSchema schema) {
        if (!schema.Capabilities.SupportsAsyncQueryAnalytic) {
            throw new FeatureServiceCapabilityException(
                $"Layer '{schema.Name}' ({schema.LayerId}) does not advertise asynchronous queryAnalytic support.");
        }
    }

    private static QueryAnalyticWaitOptions GetValidatedQueryAnalyticWaitOptions(
        QueryAnalyticWaitOptions? options) {
        var effectiveOptions = options ?? new QueryAnalyticWaitOptions();

        if (effectiveOptions.PollInterval < TimeSpan.Zero) {
            throw new ArgumentOutOfRangeException(
                nameof(QueryAnalyticWaitOptions.PollInterval),
                "PollInterval cannot be negative.");
        }

        if (effectiveOptions.Timeout is { } timeout && timeout < TimeSpan.Zero) {
            throw new ArgumentOutOfRangeException(
                nameof(QueryAnalyticWaitOptions.Timeout),
                "Timeout cannot be negative.");
        }

        return effectiveOptions;
    }
}