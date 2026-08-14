using CmsWebhook.Domain;
using FluentAssertions;
using Moq;
using Users.Application;

namespace Users.Application.Tests;

/// <summary>
/// Unit tests for <see cref="SetEntityVisibilityCommandHandler"/>: enabling/disabling, idempotency and
/// the unknown-id outcome.
/// </summary>
public class SetEntityVisibilityCommandHandlerTests
{
    private readonly Mock<IEntityCommandRepository> _repository = new();
    private readonly SetEntityVisibilityCommandHandler _handler;

    /// <summary>
    /// Creates the handler over a mocked write repository.
    /// </summary>
    public SetEntityVisibilityCommandHandlerTests()
    {
        _handler = new SetEntityVisibilityCommandHandler(_repository.Object);
    }

    /// <summary>
    /// Verifies disabling flips the flag off and persists the change.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "Administrator enables and disables entity visibility", scenario
    /// "Administrator disables an entity".
    /// </remarks>
    [Fact]
    public async Task HandleAsync_Disable_SetsFlagFalseAndPersists()
    {
        var entity = Entity(visible: true);
        _repository.Setup(item => item.GetByIdAsync("entity-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        var updated = await _handler.HandleAsync(
            new SetEntityVisibilityCommand("entity-1", IsVisibleByAdmin: false), CancellationToken.None);

        updated.Should().BeTrue();
        entity.IsVisibleByAdmin.Should().BeFalse();
        _repository.Verify(item => item.UpdateAsync(entity, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Verifies enabling flips the flag on and persists the change.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "Administrator enables and disables entity visibility", scenario
    /// "Administrator enables a disabled entity".
    /// </remarks>
    [Fact]
    public async Task HandleAsync_Enable_SetsFlagTrueAndPersists()
    {
        var entity = Entity(visible: false);
        _repository.Setup(item => item.GetByIdAsync("entity-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        var updated = await _handler.HandleAsync(
            new SetEntityVisibilityCommand("entity-1", IsVisibleByAdmin: true), CancellationToken.None);

        updated.Should().BeTrue();
        entity.IsVisibleByAdmin.Should().BeTrue();
        _repository.Verify(item => item.UpdateAsync(entity, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Verifies toggling an already-disabled entity still succeeds (idempotent disable).
    /// </summary>
    /// <remarks>
    /// Source business rule: spec scenario "Disabling is idempotent" — writing the same value back is a
    /// normal success, never an error.
    /// </remarks>
    [Fact]
    public async Task HandleAsync_DisableAlreadyDisabled_StillSucceeds()
    {
        var entity = Entity(visible: false);
        _repository.Setup(item => item.GetByIdAsync("entity-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        var updated = await _handler.HandleAsync(
            new SetEntityVisibilityCommand("entity-1", IsVisibleByAdmin: false), CancellationToken.None);

        updated.Should().BeTrue();
        _repository.Verify(item => item.UpdateAsync(entity, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Verifies an unknown entity id reports failure so the API can answer <c>404</c>.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "Administrator enables and disables entity visibility", scenario
    /// "Unknown entity id" — the handler does not persist anything for a missing entity.
    /// </remarks>
    [Fact]
    public async Task HandleAsync_UnknownId_ReturnsFalseAndDoesNotPersist()
    {
        _repository.Setup(item => item.GetByIdAsync("missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((CmsEntity?)null);

        var updated = await _handler.HandleAsync(
            new SetEntityVisibilityCommand("missing", IsVisibleByAdmin: false), CancellationToken.None);

        updated.Should().BeFalse();
        _repository.Verify(item => item.UpdateAsync(It.IsAny<CmsEntity>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static CmsEntity Entity(bool visible)
        => new()
        {
            Id = "entity-1",
            IsVisibleByAdmin = visible,
            LatestVersion = 1,
            Payload = "{}",
            IsPublished = true,
            UpdatedAt = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
        };
}
