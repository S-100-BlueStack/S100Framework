namespace S100Framework.REST.Models;

public enum ExtractChangesLayerQueryOption
{
    None = 0,
    UseFilter = 1,
    All = 2
}

public sealed record ExtractChangesLayerQuery
{
    public ExtractChangesLayerQueryOption QueryOption { get; init; } =
        ExtractChangesLayerQueryOption.UseFilter;

    public string? Where { get; init; }

    public bool UseGeometry { get; init; } = true;

    public bool IncludeRelated { get; init; } = true;

    public void Validate() {
        // ArcGIS accepts combinations here and ignores some values depending on queryOption.
        // The client keeps validation intentionally light to avoid rejecting valid server-side cases.
    }
}