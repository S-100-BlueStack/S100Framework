namespace S100Framework.REST.Models;

public sealed record TopFeaturesCountResult(
    long Count,
    FeatureExtent? Extent);