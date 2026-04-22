namespace S100Framework.REST.Configuration;

/// <summary>
/// Configures a <c>FeatureServiceClient</c> instance.
/// </summary>
public sealed class FeatureServiceClientOptions
{
    /// <summary>
    /// Gets or sets the root <c>FeatureServer</c> endpoint URI.
    /// </summary>
    public Uri? ServiceUri { get; set; }

    /// <summary>
    /// Gets or sets the default page size used for paged queries.
    /// </summary>
    /// <remarks>
    /// When not specified, the client falls back to service-driven paging behavior.
    /// </remarks>
    public int? DefaultPageSize { get; set; }

    /// <summary>
    /// Gets or sets the per-request timeout applied by the library.
    /// </summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(100);

    /// <summary>
    /// Gets or sets a value indicating whether invalid geometries should be repaired when possible.
    /// </summary>
    public bool FixInvalidGeometries { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the latest WKID should be preferred when both
    /// legacy and latest WKID values are available.
    /// </summary>
    public bool PreferLatestWkid { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether queries should request true curves from the service.
    /// </summary>
    public bool ReturnTrueCurves { get; set; }

    /// <summary>
    /// Gets or sets how returned true curves should be handled by the client.
    /// </summary>
    public TrueCurveHandling TrueCurveHandling { get; set; } = TrueCurveHandling.Throw;

    /// <summary>
    /// Gets or sets the number of line segments used when circular arcs are linearized.
    /// </summary>
    /// <remarks>
    /// This setting is only used when <see cref="TrueCurveHandling"/> is
    /// <see cref="Configuration.TrueCurveHandling.LinearizeCircularArcs"/>.
    /// </remarks>
    public int CircularArcSegmentCount { get; set; } = 16;

    /// <summary>
    /// Gets or sets the preferred HTTP method strategy for query operations.
    /// </summary>
    public QueryRequestMethodPreference QueryRequestMethodPreference { get; set; } =
        QueryRequestMethodPreference.Auto;

    /// <summary>
    /// Gets or sets the URL length threshold used when
    /// <see cref="QueryRequestMethodPreference"/> is <see cref="Configuration.QueryRequestMethodPreference.Auto"/>.
    /// </summary>
    /// <remarks>
    /// When the generated GET query string exceeds this threshold, the client switches to POST.
    /// </remarks>
    public int AutoPostQueryLengthThreshold { get; set; } = 1800;

    /// <summary>
    /// Validates the configured options.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when one or more option values are missing or invalid.
    /// </exception>
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