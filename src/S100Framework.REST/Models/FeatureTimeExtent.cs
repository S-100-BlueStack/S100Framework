namespace S100Framework.REST.Models;

/// <summary>
/// Represents a temporal extent used by feature service queries.
/// </summary>
/// <param name="Start">
/// The start instant of the extent. When <see langword="null"/>, the extent is open-ended at the start.
/// </param>
/// <param name="End">
/// The end instant of the extent. When <see langword="null"/>, the extent is open-ended at the end.
/// </param>
public sealed record FeatureTimeExtent(
    DateTimeOffset? Start,
    DateTimeOffset? End);