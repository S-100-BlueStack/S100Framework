namespace S100Framework.REST.Models;

/// <summary>
/// Represents a single unique ID value returned from a feature layer query.
/// </summary>
public sealed record FeatureUniqueId
{
    /// <summary>
    /// Initializes a unique ID value.
    /// </summary>
    /// <param name="components">
    /// The ordered unique ID components. Simple unique IDs contain a single component.
    /// </param>
    public FeatureUniqueId(IReadOnlyList<string> components) {
        ArgumentNullException.ThrowIfNull(components);

        Components = components;
    }

    /// <summary>
    /// Gets the ordered unique ID components.
    /// </summary>
    public IReadOnlyList<string> Components { get; init; }

    /// <summary>
    /// Gets the single unique ID value when the identifier is simple.
    /// </summary>
    public string? SingleValue => Components.Count == 1 ? Components[0] : null;
}