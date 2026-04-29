namespace S100Framework.REST.Models;

/// <summary>
/// Defines the filter and returned fields for one layer or table in a service-level query.
/// </summary>
public sealed record FeatureServiceLayerQueryDefinition
{
    /// <summary>
    /// Gets the layer or table ID included in the service-level query.
    /// </summary>
    public int LayerId { get; init; }

    /// <summary>
    /// Gets the SQL WHERE clause used for this layer or table.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>1=1</c>, which matches all rows.
    /// </remarks>
    public string Where { get; init; } = "1=1";

    /// <summary>
    /// Gets the output fields requested for this layer or table.
    /// </summary>
    /// <remarks>
    /// When omitted or empty, <c>*</c> is sent for this layer definition.
    /// </remarks>
    public IReadOnlyList<string>? OutFields { get; init; }

    /// <summary>
    /// Validates the layer definition before it is sent to the service.
    /// </summary>
    public void Validate()
    {
        if (LayerId < 0)
        {
            throw new InvalidOperationException("LayerId must not be negative.");
        }

        if (Where is not null && string.IsNullOrWhiteSpace(Where))
        {
            throw new InvalidOperationException("Where must not be empty when provided.");
        }

        if (OutFields?.Any(static field => string.IsNullOrWhiteSpace(field)) == true)
        {
            throw new InvalidOperationException("OutFields must not contain null, empty, or whitespace-only values.");
        }
    }
}