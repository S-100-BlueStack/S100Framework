namespace S100Framework.REST.Models;

public sealed record FeatureField(
    string Name,
    string EsriType,
    string? Alias,
    bool Nullable,
    int? Length);