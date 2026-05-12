using S100Framework.REST.Abstractions;
using S100Framework.REST.Models;

namespace S100Framework.REST.Extensions;

/// <summary>
/// Provides lifecycle helpers for persisted feature service replica state.
/// </summary>
public static class FeatureServiceClientReplicaLifecycleExtensions
{
    /// <summary>
    /// Unregisters the replica represented by persisted synchronization state.
    /// </summary>
    /// <param name="client">
    /// The feature service client.
    /// </param>
    /// <param name="state">
    /// The persisted replica synchronization state.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// The unregister operation result.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="client" /> or <paramref name="state" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the state is invalid or contains the wildcard replica ID.
    /// </exception>
    public static Task<UnregisterReplicaResult> UnregisterReplicaStateAsync(
        this IFeatureServiceClient client,
        ReplicaSynchronizationState state,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(state);

        state.Validate();

        if (string.Equals(state.ReplicaId, "*", StringComparison.Ordinal)) {
            throw new InvalidOperationException(
                "UnregisterReplicaStateAsync requires a concrete replica ID. Use UnregisterReplicaAsync directly when unregistering all replicas with '*'.");
        }

        return client.UnregisterReplicaAsync(
            new UnregisterReplicaRequest {
                ReplicaId = state.ReplicaId
            },
            cancellationToken);
    }
}