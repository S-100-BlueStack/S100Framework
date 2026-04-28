using System.Text.Json;
using S100Framework.REST.Exceptions;
using S100Framework.REST.Internal.Http;
using S100Framework.REST.Models;

namespace S100Framework.REST.Clients;

/// <summary>
/// Provides <c>queryContingentValues</c> operations for feature service endpoints.
/// </summary>
public sealed partial class FeatureServiceClient
{
    /// <inheritdoc />
    public async Task<FeatureServiceContingentValuesResult> QueryContingentValuesAsync(
        QueryContingentValuesRequest request,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);

        request.Validate();

        var metadata = await GetMetadataAsync(cancellationToken);

        if (!metadata.Capabilities.SupportsQueryContingentValues) {
            throw new FeatureServiceCapabilityException(
                "The feature service does not advertise queryContingentValues support.");
        }

        var parameters = new Dictionary<string, string?> {
            ["f"] = "json"
        };

        if (request.LayerIds is { Count: > 0 }) {
            parameters["layers"] = JsonSerializer.Serialize(request.LayerIds, JsonOptions);
        }

        if (request.CompactFormat.HasValue) {
            parameters["compactFormat"] = request.CompactFormat.Value ? "true" : "false";
        }

        if (request.DomainDictionaries.HasValue) {
            parameters["domainDictionaries"] = MapContingentValuesDomainDictionaries(
                request.DomainDictionaries.Value);
        }

        var uri = UriUtility.WithQuery(
            UriUtility.AppendPath(_serviceUri, "queryContingentValues"),
            parameters);

        using var document = await GetAsync<JsonDocument>(uri, cancellationToken);

        return new FeatureServiceContingentValuesResult(
            document.RootElement.Clone());
    }

    private static string MapContingentValuesDomainDictionaries(
        QueryContingentValuesDomainDictionaries value) {
        return value switch {
            QueryContingentValuesDomainDictionaries.None => "none",
            QueryContingentValuesDomainDictionaries.Complete => "complete",
            QueryContingentValuesDomainDictionaries.Trimmed => "trimmed",
            _ => throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "Unsupported contingent values domain dictionary option.")
        };
    }
}