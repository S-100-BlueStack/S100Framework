namespace S100Framework.REST.Models;

public interface IAttributeRecord
{
    IReadOnlyDictionary<string, object?> Attributes { get; }
}