namespace S100Framework.REST.Exceptions;

/// <summary>
/// Represents an error where the target feature service or layer does not support
/// the requested capability.
/// </summary>
/// <remarks>
/// This exception is typically thrown when a request depends on optional server
/// capabilities such as attachments, change tracking, or asynchronous operations,
/// but the target service metadata indicates that the capability is unavailable.
/// </remarks>
public sealed class FeatureServiceCapabilityException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FeatureServiceCapabilityException" /> class.
    /// </summary>
    /// <param name="message">
    /// A human-readable error message describing the unsupported capability.
    /// </param>
    public FeatureServiceCapabilityException(string message)
        : base(message) {
    }
}