using System.Net;

namespace S100Framework.REST.Exceptions;

/// <summary>
/// Represents an error returned by a Feature Service request.
/// </summary>
/// <remarks>
/// This exception is used when the service returns an ArcGIS error payload or when an
/// HTTP response indicates that the request failed in a Feature Service-specific way.
/// </remarks>
public sealed class FeatureServiceException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FeatureServiceException" /> class.
    /// </summary>
    /// <param name="message">
    /// A human-readable error message.
    /// </param>
    /// <param name="requestUri">
    /// The request URI associated with the failure.
    /// </param>
    /// <param name="errorCode">
    /// The ArcGIS error code, when available.
    /// </param>
    /// <param name="details">
    /// Additional ArcGIS error details, when available.
    /// </param>
    /// <param name="statusCode">
    /// The HTTP status code, when available.
    /// </param>
    /// <param name="innerException">
    /// The underlying exception, when available.
    /// </param>
    public FeatureServiceException(
        string message,
        Uri requestUri,
        int? errorCode = null,
        IReadOnlyList<string>? details = null,
        HttpStatusCode? statusCode = null,
        Exception? innerException = null)
        : base(CreateMessage(message, requestUri, errorCode, details, statusCode), innerException) {
        RequestUri = requestUri;
        ErrorCode = errorCode;
        StatusCode = statusCode;
        Details = details ?? Array.Empty<string>();
    }

    /// <summary>
    /// Gets the request URI associated with the failure.
    /// </summary>
    public Uri RequestUri { get; }

    /// <summary>
    /// Gets the ArcGIS error code, when available.
    /// </summary>
    public int? ErrorCode { get; }

    /// <summary>
    /// Gets the HTTP status code, when available.
    /// </summary>
    public HttpStatusCode? StatusCode { get; }

    /// <summary>
    /// Gets additional ArcGIS error details, when available.
    /// </summary>
    public IReadOnlyList<string> Details { get; }

    private static string CreateMessage(
        string message,
        Uri requestUri,
        int? errorCode,
        IReadOnlyList<string>? details,
        HttpStatusCode? statusCode) {
        var parts = new List<string>();

        if (statusCode is not null) {
            parts.Add($"HTTP {(int)statusCode} ({statusCode})");
        }

        if (errorCode is not null) {
            parts.Add($"Esri error code {errorCode}");
        }

        parts.Add(message);
        parts.Add($"Request: {requestUri}");

        if (details is { Count: > 0 }) {
            parts.Add($"Details: {string.Join(" | ", details)}");
        }

        return string.Join(". ", parts);
    }
}