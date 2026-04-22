using System.Net;

namespace S100Framework.REST.Exceptions;

/// <summary>
/// Represents a failure while acquiring, refreshing, or exchanging authentication tokens.
/// </summary>
/// <remarks>
/// This exception is intended for authentication-specific flows such as token generation
/// or portal-to-server token exchange.
/// </remarks>
public sealed class FeatureServiceAuthenticationException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FeatureServiceAuthenticationException" /> class.
    /// </summary>
    /// <param name="message">
    /// A human-readable error message.
    /// </param>
    /// <param name="endpointUri">
    /// The authentication endpoint URI associated with the failure, when available.
    /// </param>
    /// <param name="statusCode">
    /// The HTTP status code, when available.
    /// </param>
    /// <param name="innerException">
    /// The underlying exception, when available.
    /// </param>
    public FeatureServiceAuthenticationException(
        string message,
        Uri? endpointUri = null,
        HttpStatusCode? statusCode = null,
        Exception? innerException = null)
        : base(message, innerException) {
        EndpointUri = endpointUri;
        StatusCode = statusCode;
    }

    /// <summary>
    /// Gets the authentication endpoint URI associated with the failure, when available.
    /// </summary>
    public Uri? EndpointUri { get; }

    /// <summary>
    /// Gets the HTTP status code, when available.
    /// </summary>
    public HttpStatusCode? StatusCode { get; }
}