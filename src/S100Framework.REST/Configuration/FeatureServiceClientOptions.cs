namespace S100Framework.REST.Configuration;

public sealed class FeatureServiceClientOptions
{
    public Uri? ServiceUri { get; set; }

    public int? DefaultPageSize { get; set; }

    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(100);

    public bool FixInvalidGeometries { get; set; }

    public bool PreferLatestWkid { get; set; } = true;

    public bool ReturnTrueCurves { get; set; }

    public void Validate() {
        if (ServiceUri is null) {
            throw new InvalidOperationException("ServiceUri must be configured.");
        }

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