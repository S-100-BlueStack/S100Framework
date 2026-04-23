namespace S100Framework.REST.Models;

/// <summary>
/// Represents a model that exposes a bag of named attribute values.
/// </summary>
public interface IAttributeRecord
{
    /// <summary>
    /// Gets the attribute values associated with the current record.
    /// </summary>
    IReadOnlyDictionary<string, object?> Attributes { get; }
}