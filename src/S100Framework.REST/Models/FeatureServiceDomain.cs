namespace S100Framework.REST.Models;

/// <summary>
/// Represents a domain returned from the feature service <c>queryDomains</c> operation.
/// </summary>
/// <param name="Type">
/// The Esri domain type, for example <c>range</c>, <c>codedValue</c>, or <c>inherited</c>.
/// </param>
/// <param name="Name">
/// The domain name when provided by the service.
/// </param>
/// <param name="FieldType">
/// The Esri field type associated with the domain when provided by the service.
/// </param>
/// <param name="MergePolicy">
/// The merge policy reported by the service when present.
/// </param>
/// <param name="SplitPolicy">
/// The split policy reported by the service when present.
/// </param>
/// <param name="Range">
/// The domain range for range domains.
/// </param>
/// <param name="CodedValues">
/// The coded values for coded-value domains.
/// </param>
public sealed record FeatureServiceDomain(
    string Type,
    string? Name,
    string? FieldType,
    string? MergePolicy,
    string? SplitPolicy,
    FeatureServiceDomainRange? Range,
    IReadOnlyList<FeatureServiceCodedValue> CodedValues)
{
    /// <summary>
    /// Gets a value indicating whether this is a range domain.
    /// </summary>
    public bool IsRange =>
        string.Equals(Type, "range", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Gets a value indicating whether this is a coded-value domain.
    /// </summary>
    public bool IsCodedValue =>
        string.Equals(Type, "codedValue", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Gets a value indicating whether this is an inherited domain.
    /// </summary>
    public bool IsInherited =>
        string.Equals(Type, "inherited", StringComparison.OrdinalIgnoreCase);
}