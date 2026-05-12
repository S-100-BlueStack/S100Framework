using S100Framework.REST.Internal.Replica;
using S100Framework.REST.Models;

namespace S100Framework.REST.Extensions;

/// <summary>
/// Provides helpers for reading downloaded JSON replica result files.
/// </summary>
public static class ReplicaJsonResultFileExtensions
{
    /// <summary>
    /// Parses a downloaded <c>createReplica</c> JSON result file.
    /// </summary>
    /// <param name="file">
    /// The downloaded create replica file result.
    /// </param>
    /// <returns>
    /// The parsed JSON result file metadata and edit result summary.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="file" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the file content is not valid JSON or contains invalid critical values.
    /// </exception>
    public static ReplicaJsonResultFile ReadJsonResultFile(this CreateReplicaFileResult file) {
        ArgumentNullException.ThrowIfNull(file);

        return ReplicaJsonResultFileReader.Read(
            file.Content,
            file.ResultUrl,
            "createReplica");
    }

    /// <summary>
    /// Parses a downloaded <c>synchronizeReplica</c> JSON result file.
    /// </summary>
    /// <param name="file">
    /// The downloaded synchronize replica file result.
    /// </param>
    /// <returns>
    /// The parsed JSON result file metadata and edit result summary.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="file" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the file content is not valid JSON or contains invalid critical values.
    /// </exception>
    public static ReplicaJsonResultFile ReadJsonResultFile(this SynchronizeReplicaFileResult file) {
        ArgumentNullException.ThrowIfNull(file);

        return ReplicaJsonResultFileReader.Read(
            file.Content,
            file.ResultUrl,
            "synchronizeReplica");
    }
}