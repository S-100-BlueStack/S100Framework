namespace S100Framework.REST.Configuration;

public sealed class FeatureServiceClientOptions
{
    public Uri? ServiceUri { get; set; }

    public int? DefaultPageSize { get; set; }

    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(100);

    public bool FixInvalidGeometries { get; set; }

    public bool PreferLatestWkid { get; set; } = true;

    public bool ReturnTrueCurves { get; set; }

    public TrueCurveHandling TrueCurveHandling { get; set; } = TrueCurveHandling.Throw;

    public int CircularArcSegmentCount { get; set; } = 16;

    public QueryRequestMethodPreference QueryRequestMethodPreference { get; set; } =
    QueryRequestMethodPreference.Auto;

    public int AutoPostQueryLengthThreshold { get; set; } = 1800;

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

        if (CircularArcSegmentCount < 2) {
            throw new InvalidOperationException("CircularArcSegmentCount must be greater than or equal to 2.");
        }

        if (AutoPostQueryLengthThreshold <= 0) {
            throw new InvalidOperationException("AutoPostQueryLengthThreshold must be greater than zero.");
        }
    }
}