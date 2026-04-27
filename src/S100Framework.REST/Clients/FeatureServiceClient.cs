using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using S100Framework.REST.Abstractions;
using S100Framework.REST.Configuration;

namespace S100Framework.REST.Clients;

/// <summary>
/// Provides the core state and construction logic for ArcGIS Feature Service REST clients.
/// </summary>
/// <remarks>
/// Operation-specific members are implemented in partial class files to keep each Feature Service operation group
/// isolated and easier to maintain.
/// </remarks>
public sealed partial class FeatureServiceClient : IFeatureServiceClient
{
    private static readonly JsonSerializerOptions JsonOptions = new() {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly FeatureServiceClientOptions _options;
    private readonly IFeatureServiceRequestAuthorizer? _authorizer;
    private readonly Uri _serviceUri;

    /// <summary>
    /// Initializes a new instance of the <see cref="FeatureServiceClient"/> class with an HTTP client and validated options.
    /// </summary>
    /// <param name="httpClient">
    /// The HTTP client used to call the feature service.
    /// </param>
    /// <param name="options">
    /// The configured client options.
    /// </param>
    [ActivatorUtilitiesConstructor]
    public FeatureServiceClient(
        HttpClient httpClient,
        FeatureServiceClientOptions options)
        : this(httpClient, options, authorizer: null) {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FeatureServiceClient"/> class with an HTTP client, validated options,
    /// and an optional request authorizer.
    /// </summary>
    /// <param name="httpClient">
    /// The HTTP client used to call the feature service.
    /// </param>
    /// <param name="options">
    /// The configured client options.
    /// </param>
    /// <param name="authorizer">
    /// An optional request authorizer that can add authentication to outgoing requests.
    /// </param>
    public FeatureServiceClient(
        HttpClient httpClient,
        FeatureServiceClientOptions options,
        IFeatureServiceRequestAuthorizer? authorizer) {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _authorizer = authorizer;

        _options.Validate();
        _serviceUri = _options.ServiceUri ?? throw new InvalidOperationException("ServiceUri must be configured.");
    }

    internal FeatureServiceClientOptions Options => _options;
}