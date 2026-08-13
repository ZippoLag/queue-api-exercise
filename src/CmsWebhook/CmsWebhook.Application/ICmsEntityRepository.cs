using CmsWebhook.Domain;

namespace CmsWebhook.Application;

/// <summary>
/// Port for reading and writing stored <see cref="CmsEntity"/> records.
/// </summary>
/// <remarks>
/// Clean architecture: the Application layer depends on this port, never on EF Core. Writes are applied
/// by the outbox worker inside the per-event transaction (design D5).
/// </remarks>
public interface ICmsEntityRepository
{
    /// <summary>
    /// Loads the stored entity for the given id, or <see langword="null"/> when none exists.
    /// </summary>
    /// <param name="id">The external entity's id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<CmsEntity?> GetByIdAsync(string id, CancellationToken cancellationToken);

    /// <summary>
    /// Inserts the entity or updates it in place when it already exists.
    /// </summary>
    /// <param name="entity">The entity state to persist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpsertAsync(CmsEntity entity, CancellationToken cancellationToken);

    /// <summary>
    /// Hard-deletes the stored entity for the given id; a missing entity is a no-op.
    /// </summary>
    /// <param name="id">The external entity's id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteAsync(string id, CancellationToken cancellationToken);
}
