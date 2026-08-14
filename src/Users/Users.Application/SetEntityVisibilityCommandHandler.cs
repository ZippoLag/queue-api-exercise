namespace Users.Application;

/// <summary>
/// Command (write side) that flips an entity's administrator-visibility flag.
/// </summary>
/// <param name="EntityId">The external entity's id.</param>
/// <param name="IsVisibleByAdmin">
/// The new flag value: <see langword="false"/> for <c>disable</c>, <see langword="true"/> for <c>enable</c>.
/// </param>
public sealed record SetEntityVisibilityCommand(string EntityId, bool IsVisibleByAdmin);

/// <summary>
/// Command handler (write side) for the administrator's enable/disable endpoints.
/// </summary>
public interface ISetEntityVisibilityCommandHandler
{
    /// <summary>
    /// Applies the visibility change, reporting whether the entity existed.
    /// </summary>
    /// <param name="command">The target entity id and flag value.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// <see langword="true"/> when the entity was found and updated; <see langword="false"/> when it does
    /// not exist, which the API maps to <c>404 Not Found</c>.
    /// </returns>
    Task<bool> HandleAsync(SetEntityVisibilityCommand command, CancellationToken cancellationToken);
}

/// <summary>
/// Sets the visibility flag idempotently: writing the current value back still succeeds.
/// </summary>
/// <remarks>
/// Spec "Administrator enables and disables entity visibility": the endpoints take no request body and
/// return an empty success response, and toggling an already-disabled or already-enabled entity still
/// succeeds. Unknown ids are reported to the API layer, which maps them to <c>404 Not Found</c>.
/// </remarks>
public sealed class SetEntityVisibilityCommandHandler : ISetEntityVisibilityCommandHandler
{
    private readonly IEntityCommandRepository _repository;

    /// <summary>
    /// Creates the handler with the given write repository.
    /// </summary>
    /// <param name="repository">The write-side entity port.</param>
    public SetEntityVisibilityCommandHandler(IEntityCommandRepository repository)
    {
        _repository = repository;
    }

    /// <inheritdoc/>
    public async Task<bool> HandleAsync(
        SetEntityVisibilityCommand command,
        CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(command.EntityId, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        entity.IsVisibleByAdmin = command.IsVisibleByAdmin;
        await _repository.UpdateAsync(entity, cancellationToken);
        return true;
    }
}
