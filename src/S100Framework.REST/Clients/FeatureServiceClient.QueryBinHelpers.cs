using S100Framework.REST.Models;

namespace S100Framework.REST.Clients;

/// <summary>
/// Provides shared helpers for bin query operations.
/// </summary>
public sealed partial class FeatureServiceClient
{
    private static string MapQueryBinsOrder(QueryBinsOrder value) {
        return value switch {
            QueryBinsOrder.Ascending => "ASC",
            QueryBinsOrder.Descending => "DESC",
            _ => throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "Unsupported bin order.")
        };
    }
}