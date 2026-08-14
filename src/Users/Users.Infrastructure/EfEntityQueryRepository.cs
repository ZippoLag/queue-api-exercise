using CmsWebhook.Domain;
using Microsoft.EntityFrameworkCore;
using Users.Application;

namespace Users.Infrastructure;

/// <summary>
/// EF Core read-side repository over the shared <c>cms_entities</c> table.
/// </summary>
/// <remarks>
/// Read-optimized per CQRS: every query is <c>AsNoTracking</c>, so listings never hydrate the change
/// tracker (design decision 2 of the users-api-vertical change). Role filtering stays in the Application
/// layer; this repository returns all published entities unfiltered.
/// </remarks>
public class EfEntityQueryRepository : IEntityQueryRepository
{
    private readonly UsersDbContext _dbContext;

    /// <summary>
    /// Creates the repository over the given context.
    /// </summary>
    /// <param name="dbContext">The context exposing the entity store table.</param>
    public EfEntityQueryRepository(UsersDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CmsEntity>> ListPublishedAsync(CancellationToken cancellationToken)
        => await _dbContext.Entities
            .AsNoTracking()
            .Where(entity => entity.IsPublished)
            .ToListAsync(cancellationToken);
}
