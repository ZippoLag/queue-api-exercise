using CmsWebhook.Domain;
using Microsoft.EntityFrameworkCore;
using Users.Application;

namespace Users.Infrastructure;

/// <summary>
/// EF Core write-side repository over the shared <c>cms_entities</c> table.
/// </summary>
/// <remarks>
/// Single-writer semantics (design decision 2 of the users-api-vertical change): visibility commands load
/// a tracked row and persist it in place. Reads (listing) live on <see cref="IEntityQueryRepository"/>.
/// </remarks>
public class EfEntityCommandRepository : IEntityCommandRepository
{
    private readonly UsersDbContext _dbContext;

    /// <summary>
    /// Creates the repository over the given context.
    /// </summary>
    /// <param name="dbContext">The context exposing the entity store table.</param>
    public EfEntityCommandRepository(UsersDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc/>
    public async Task<CmsEntity?> GetByIdAsync(string id, CancellationToken cancellationToken)
        => await _dbContext.Entities.FindAsync(new object[] { id }, cancellationToken);

    /// <inheritdoc/>
    public async Task UpdateAsync(CmsEntity entity, CancellationToken cancellationToken)
        => await _dbContext.SaveChangesAsync(cancellationToken);
}
