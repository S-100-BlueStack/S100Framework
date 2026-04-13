namespace S100Framework.REST.Configuration;

public sealed class FeatureServiceClientOptions
{
    public required Uri ServiceUri { get; init; }

    public int? DefaultPageSize { get; init; }

    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(100);

    public bool FixInvalidGeometries { get; init; }

    public bool PreferLatestWkid { get; init; } = true;

    public void Validate() {
        if (!ServiceUri.IsAbsoluteUri) {
            throw new InvalidOperationException("ServiceUri must be an absolute URI.");
        }

        if (!ServiceUri.AbsolutePath.EndsWith("/FeatureServer", StringComparison.OrdinalIgnoreCase)) {
            throw new InvalidOperationException(
                "ServiceUri must point to the FeatureServer root endpoint.");
        }

        if (DefaultPageSize is <= 0) {
            throw new InvalidOperationException("DefaultPageSize must be greater than zero when specified.");
        }

        if (RequestTimeout <= TimeSpan.Zero) {
            throw new InvalidOperationException("RequestTimeout must be greater than zero.");
        }
    }
}