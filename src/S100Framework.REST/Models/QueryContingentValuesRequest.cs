namespace S100Framework.REST.Models;

/// <summary>
/// Represents a feature service <c>queryContingentValues</c> request.
/// </summary>
public sealed record QueryContingentValuesRequest
{
    /// <summary>
    /// Gets the layer or table IDs to include.
    /// </summary>
    /// <remarks>
    /// When omitted, the service may return contingent values for all layers and tables with contingent value definitions.
    /// </remarks>
    public IReadOnlyList<int>? LayerIds { get; init; }

    /// <summary>
    /// Gets a value indicating whether hosted feature services should return compact contingent value codes.
    /// </summary>
    public bool? CompactFormat { get; init; }

    /// <summary>
    /// Gets how hosted feature services should include domain dictionaries.
    /// </summary>
    public QueryContingentValuesDomainDictionaries? DomainDictionaries { get; init; }

    /// <summary>
    /// Validates the contingent values query request before it is sent.
    /// </summary>
    public void Validate() {
        if (LayerIds is not null) {
            if (LayerIds.Count == 0) {
                throw new InvalidOperationException("LayerIds must not be empty when provided.");
            }

            if (LayerIds.Any(static layerId => layerId < 0)) {
                throw new InvalidOperationException("LayerIds must not contain negative values.");
            }

            if (LayerIds.Distinct().Count() != LayerIds.Count) {
                throw new InvalidOperationException("LayerIds must not contain duplicate values.");
            }
        }

        if (DomainDictionaries.HasValue && !Enum.IsDefined(DomainDictionaries.Value)) {
            throw new InvalidOperationException("DomainDictionaries must be a supported contingent values domain dictionary option.");
        }
    }
}