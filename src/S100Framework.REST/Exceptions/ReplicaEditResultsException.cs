using S100Framework.REST.Models;

namespace S100Framework.REST.Exceptions;

/// <summary>
/// Represents failed replica edit results returned by a replica JSON result file.
/// </summary>
public sealed class ReplicaEditResultsException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReplicaEditResultsException" /> class.
    /// </summary>
    /// <param name="message">
    /// The exception message.
    /// </param>
    /// <param name="errors">
    /// The failed replica edit results.
    /// </param>
    public ReplicaEditResultsException(
        string message,
        IReadOnlyList<ReplicaLayerEditResult> errors)
        : base(message) {
        Errors = errors;
    }

    /// <summary>
    /// Gets the failed replica edit results returned by the service.
    /// </summary>
    public IReadOnlyList<ReplicaLayerEditResult> Errors { get; }
}