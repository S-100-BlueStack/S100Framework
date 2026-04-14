namespace S100Framework.REST.Models;

public sealed record TopFeaturesFilter
{
    public IReadOnlyList<string> GroupByFields { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> OrderByFields { get; init; } = Array.Empty<string>();

    public int TopCount { get; init; }

    public void Validate() {
        if (GroupByFields.Count == 0) {
            throw new InvalidOperationException("At least one group-by field must be provided.");
        }

        if (OrderByFields.Count == 0) {
            throw new InvalidOperationException("At least one order-by field must be provided.");
        }

        if (TopCount <= 0) {
            throw new InvalidOperationException("TopCount must be greater than zero.");
        }
    }
}