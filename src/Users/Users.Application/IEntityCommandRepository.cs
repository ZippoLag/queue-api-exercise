using CmsWebhook.Domain;

namespace Users.Application;

/// <summary>
/// Port for the write side of the shared entity store: single-writer visibility commands.
/// </summary>
/// <remarks>
/// Strict CQRS: this port exists for the administrator's enable/disable commands only. Writes use a
/// tracking context (single-writer semantics); reads live on <see cref="IEntityQueryRepository"/> and
/// never share this path.
/// </remarks>
public interface IEntityCommandRepository
{
    /// <summary>
    /// Loads the stored entity for the given id, or <see langword="null"/> when none exists.
    /// </summary>
    /// <param name="id">The external entity's id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<CmsEntity?> GetByIdAsync(string id, CancellationToken cancellationToken);

    /// <summary>
    /// Persists changes made to a loaded entity (e.g. its visibility flag).
    /// </summary>
    /// <param name="entity">The tracked entity to persist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdateAsync(CmsEntity entity, CancellationToken cancellationToken);
}
