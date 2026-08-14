using CmsWebhook.Application;
using CmsWebhook.Domain;
using Microsoft.EntityFrameworkCore;

namespace CmsWebhook.Infrastructure;    /// <summary>
    /// EF Core implementation of <see cref="ICmsEntityRepository"/> over the <c>cms_entities</c> table.
    /// </summary>
    /// <remarks>
    /// Writes participate in the outbox worker's per-event transaction (design D5). The upsert loads the
    /// tracked row when present so EF updates it in place instead of erroring on a duplicate key. When the
    /// row exists, the administrator's visibility override (<see cref="CmsEntity.IsVisibleByAdmin"/>) is
    /// copied from the stored row onto the incoming entity before overwriting, so a processed CMS event
    /// can never silently re-enable an entity the administrator disabled (users-api-vertical design
    /// decision 3).
    /// </remarks>
public class EfCmsEntityRepository : ICmsEntityRepository
{
    private readonly CmsDbContext _dbContext;

    /// <summary>
    /// Creates the repository over the given context.
    /// </summary>
    /// <param name="dbContext">The context exposing the entity store table.</param>
    public EfCmsEntityRepository(CmsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc/>
    public async Task<CmsEntity?> GetByIdAsync(string id, CancellationToken cancellationToken)
        => await _dbContext.Entities.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

    /// <inheritdoc/>
    public async Task UpsertAsync(CmsEntity entity, CancellationToken cancellationToken)
    {
        var existing = await _dbContext.Entities.FindAsync(new object[] { entity.Id }, cancellationToken);
        if (existing is null)
        {
            _dbContext.Entities.Add(entity);
        }
        else
        {
            // Carry the administrator's visibility override forward: the incoming entity is built by the
            // event processor, which knows nothing about the Users-domain flag. Without this defensive
            // copy, any producer that constructs a fresh entity (the create path does) would reset a
            // disabled entity to visible.
            entity.IsVisibleByAdmin = existing.IsVisibleByAdmin;
            _dbContext.Entry(existing).CurrentValues.SetValues(entity);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(string id, CancellationToken cancellationToken)
    {
        var existing = await _dbContext.Entities.FindAsync(new object[] { id }, cancellationToken);
        if (existing is not null)
        {
            _dbContext.Entities.Remove(existing);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
