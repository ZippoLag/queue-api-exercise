using CmsWebhook.Domain;

namespace Users.Application;

/// <summary>
/// Port for the read side of the shared entity store: lists currently published entities.
/// </summary>
/// <remarks>
/// Strict CQRS: this port serves the Users API's <c>GET /entities</c> query only and never mutates state.
/// The implementation is read-optimized (<c>AsNoTracking</c>, see design decision 2 of the
/// users-api-vertical change). Role filtering is a query-handler concern: the repository returns every
/// published entity unfiltered and the handler applies the administrator-visibility rule.
/// </remarks>
public interface IEntityQueryRepository
{
    /// <summary>
    /// Lists every currently published entity, unfiltered by administrator visibility.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>All published entities currently in the store.</returns>
    Task<IReadOnlyList<CmsEntity>> ListPublishedAsync(CancellationToken cancellationToken);
}
