namespace Users.Application;

/// <summary>
/// Query (read side) for listing the currently published entities visible to the caller.
/// </summary>
/// <remarks>
/// The caller's role is resolved by the API layer from the authenticated principal against the
/// configured administrator username; the Application layer applies the visibility rule without needing
/// to know concrete usernames (design decision 1 of the users-api-vertical change).
/// </remarks>
/// <param name="IsAdministrator">Whether the caller is the administrator; regular users get the filtered view.</param>
public sealed record ListEntitiesQuery(bool IsAdministrator);

/// <summary>
/// Query handler (read side) that applies the administrator-visibility rule to the published entities.
/// </summary>
public interface IListEntitiesQueryHandler
{
    /// <summary>
    /// Lists the published entities visible to the caller, ordered as the repository returns them.
    /// </summary>
    /// <param name="query">The role of the caller.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The visible entities as response items.</returns>
    Task<IReadOnlyList<EntityListItem>> HandleAsync(ListEntitiesQuery query, CancellationToken cancellationToken);
}

/// <summary>
/// Lists published entities, hiding disabled ones from everyone except the administrator.
/// </summary>
/// <remarks>
/// Spec "Entities are listed by published status and administrator visibility": the repository returns
/// every published entity; the administrator sees all of them, a regular user only those not disabled by
/// an administrator. Unpublished entities never reach the response because the repository filters them.
/// </remarks>
public sealed class ListEntitiesQueryHandler : IListEntitiesQueryHandler
{
    private readonly IEntityQueryRepository _repository;

    /// <summary>
    /// Creates the handler with the given read repository.
    /// </summary>
    /// <param name="repository">The read-side entity port.</param>
    public ListEntitiesQueryHandler(IEntityQueryRepository repository)
    {
        _repository = repository;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<EntityListItem>> HandleAsync(
        ListEntitiesQuery query,
        CancellationToken cancellationToken)
    {
        var entities = await _repository.ListPublishedAsync(cancellationToken);

        return entities
            .Where(entity => query.IsAdministrator || entity.IsVisibleByAdmin)
            .Select(entity => new EntityListItem(
                entity.Id,
                entity.IsVisibleByAdmin,
                entity.LatestVersion,
                entity.UpdatedAt,
                entity.Payload))
            .ToList();
    }
}
