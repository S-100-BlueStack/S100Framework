using S100Framework.REST.Exceptions;
using S100Framework.REST.Models;

namespace S100Framework.REST.Clients;

/// <summary>
/// Provides layer-level calculate wrapper methods for <see cref="FeatureLayerClient" />.
/// </summary>
public sealed partial class FeatureLayerClient
{
    /// <inheritdoc />
    public async Task<CalculateResult> CalculateAsync(
        CalculateRequest request,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);

        request.Validate();

        var schema = await GetSchemaAsync(cancellationToken);
        EnsureCalculateSupported(schema);
        EnsureCalculateSqlFormatSupported(schema, request);

        return await _serviceClient.CalculateAsync(
            _layerId,
            request,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<CalculateSubmissionResult> SubmitCalculateAsync(
        CalculateRequest request,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);

        request.Validate();

        var schema = await GetSchemaAsync(cancellationToken);
        EnsureCalculateSupported(schema);
        EnsureAsyncCalculateSupported(schema);
        EnsureCalculateSqlFormatSupported(schema, request);

        return await _serviceClient.SubmitCalculateAsync(
            _layerId,
            request,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<CalculateJobStatus> GetCalculateStatusAsync(
        Uri statusUrl,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(statusUrl);

        return _serviceClient.GetCalculateStatusAsync(statusUrl, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<CalculateResult> WaitForCalculateCompletionAsync(
        CalculateRequest request,
        CalculateWaitOptions? options = null,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);

        var submission = await SubmitCalculateAsync(request, cancellationToken);

        if (submission.Result is not null) {
            return submission.Result;
        }

        if (submission.StatusUrl is null) {
            throw new FeatureServiceException(
                "The calculate submission did not return a status URL or a result payload.",
                new Uri(_serviceClient.Options.ServiceUri!, $"{_layerId}/calculate"));
        }

        return await WaitForCalculateCompletionAsync(
            submission.StatusUrl,
            options,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<CalculateResult> WaitForCalculateCompletionAsync(
        Uri statusUrl,
        CalculateWaitOptions? options = null,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(statusUrl);

        var effectiveOptions = GetValidatedCalculateWaitOptions(options);
        var startedAt = DateTimeOffset.UtcNow;

        while (true) {
            cancellationToken.ThrowIfCancellationRequested();

            var status = await GetCalculateStatusAsync(statusUrl, cancellationToken);

            if (status.IsCompleted) {
                return new CalculateResult(
                    Success: true,
                    UpdatedFeatureCount: status.RecordCount,
                    EditMoment: null);
            }

            if (status.IsTerminal) {
                throw new FeatureServiceException(
                    $"The calculate job ended with terminal status '{status.Status}'.",
                    statusUrl);
            }

            if (effectiveOptions.Timeout is { } timeout &&
                DateTimeOffset.UtcNow - startedAt >= timeout) {
                throw new TimeoutException(
                    $"The calculate job did not complete within {timeout}.");
            }

            if (effectiveOptions.PollInterval > TimeSpan.Zero) {
                await Task.Delay(effectiveOptions.PollInterval, cancellationToken);
            }
            else {
                await Task.Yield();
            }
        }
    }

    private static void EnsureCalculateSupported(FeatureLayerSchema schema) {
        if (!schema.Capabilities.SupportsCalculate) {
            throw new FeatureServiceCapabilityException(
                $"Layer '{schema.Name}' ({schema.LayerId}) does not advertise calculate support.");
        }
    }

    private static void EnsureAsyncCalculateSupported(FeatureLayerSchema schema) {
        if (!schema.Capabilities.SupportsAsyncCalculate) {
            throw new FeatureServiceCapabilityException(
                $"Layer '{schema.Name}' ({schema.LayerId}) does not advertise asynchronous calculate support.");
        }
    }

    private static void EnsureCalculateSqlFormatSupported(
        FeatureLayerSchema schema,
        CalculateRequest request) {
        if (request.SqlFormat is not { } sqlFormat || sqlFormat == FeatureQuerySqlFormat.None) {
            return;
        }

        var supportedSqlFormats = schema.Capabilities.SupportedSqlFormatsInCalculate;

        if (supportedSqlFormats.Count == 0 || supportedSqlFormats.Contains(sqlFormat)) {
            return;
        }

        throw new FeatureServiceCapabilityException(
            $"Layer '{schema.Name}' ({schema.LayerId}) does not advertise calculate SQL format " +
            $"'{FormatCalculateSqlFormat(sqlFormat)}'. Supported formats: " +
            $"{string.Join(", ", supportedSqlFormats.Select(FormatCalculateSqlFormat))}.");
    }

    private static string FormatCalculateSqlFormat(FeatureQuerySqlFormat value) {
        return value switch {
            FeatureQuerySqlFormat.Standard => "standard",
            FeatureQuerySqlFormat.Native => "native",
            FeatureQuerySqlFormat.None => "none",
            _ => value.ToString()
        };
    }

    private static CalculateWaitOptions GetValidatedCalculateWaitOptions(
        CalculateWaitOptions? options) {
        var effectiveOptions = options ?? new CalculateWaitOptions();

        if (effectiveOptions.PollInterval < TimeSpan.Zero) {
            throw new ArgumentOutOfRangeException(
                nameof(CalculateWaitOptions.PollInterval),
                "PollInterval cannot be negative.");
        }

        if (effectiveOptions.Timeout is { } timeout && timeout < TimeSpan.Zero) {
            throw new ArgumentOutOfRangeException(
                nameof(CalculateWaitOptions.Timeout),
                "Timeout cannot be negative.");
        }

        return effectiveOptions;
    }
}