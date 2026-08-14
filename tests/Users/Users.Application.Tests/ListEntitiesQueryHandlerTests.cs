using CmsWebhook.Domain;
using FluentAssertions;
using Moq;
using Users.Application;

namespace Users.Application.Tests;

/// <summary>
/// Unit tests for <see cref="ListEntitiesQueryHandler"/>: the role-based visibility rule over the
/// published entities returned by the read repository.
/// </summary>
public class ListEntitiesQueryHandlerTests
{
    private readonly Mock<IEntityQueryRepository> _repository = new();
    private readonly ListEntitiesQueryHandler _handler;

    /// <summary>
    /// Creates the handler over a mocked read repository.
    /// </summary>
    public ListEntitiesQueryHandlerTests()
    {
        _handler = new ListEntitiesQueryHandler(_repository.Object);
    }

    /// <summary>
    /// Verifies a regular user sees only published entities that are not disabled by an administrator.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "Entities are listed by published status and administrator visibility",
    /// scenario "Regular user sees only published, enabled entities".
    /// </remarks>
    [Fact]
    public async Task HandleAsync_ForRegularUser_ReturnsOnlyEnabledEntities()
    {
        _repository.Setup(item => item.ListPublishedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                Entity("enabled-1", visible: true),
                Entity("disabled-1", visible: false),
                Entity("enabled-2", visible: true),
            ]);

        var result = await _handler.HandleAsync(new ListEntitiesQuery(IsAdministrator: false), CancellationToken.None);

        result.Select(item => item.Id).Should().Equal("enabled-1", "enabled-2");
    }

    /// <summary>
    /// Verifies the administrator sees every published entity, including disabled ones.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "Entities are listed by published status and administrator visibility",
    /// scenario "Administrator sees all published entities".
    /// </remarks>
    [Fact]
    public async Task HandleAsync_ForAdministrator_ReturnsAllPublishedEntities()
    {
        _repository.Setup(item => item.ListPublishedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                Entity("enabled-1", visible: true),
                Entity("disabled-1", visible: false),
            ]);

        var result = await _handler.HandleAsync(new ListEntitiesQuery(IsAdministrator: true), CancellationToken.None);

        result.Select(item => item.Id).Should().Equal("enabled-1", "disabled-1");
    }

    /// <summary>
    /// Verifies each response item carries the id, visibility flag, version, update time and payload.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec scenario "Response items include the entity id and visibility flag" —
    /// the item shape is uniform for both roles, so the administrator can target enable/disable calls.
    /// </remarks>
    [Fact]
    public async Task HandleAsync_WhenAdminRequests_PreservesItemFields()
    {
        var updatedAt = new DateTimeOffset(2024, 5, 6, 7, 8, 9, TimeSpan.Zero);
        _repository.Setup(item => item.ListPublishedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([Entity("entity-1", visible: false, version: 3, updatedAt, """{"title":"x"}""")]);

        var result = await _handler.HandleAsync(new ListEntitiesQuery(IsAdministrator: true), CancellationToken.None);

        var item = result.Single();
        item.Should().Be(new EntityListItem("entity-1", IsVisibleByAdmin: false, 3, updatedAt, """{"title":"x"}"""));
        item.IsVisibleByAdmin.Should().BeFalse();
    }

    /// <summary>
    /// Verifies a disabled entity keeps its flag in a regular user's response items (it is filtered out,
    /// so this only pins the mapping for entities that do pass the filter).
    /// </summary>
    /// <remarks>
    /// Source business rule: a regular user only ever receives enabled items, so their visibility flag is
    /// always true in that view (design decision 4 of the users-api-vertical change).
    /// </remarks>
    [Fact]
    public async Task HandleAsync_ForRegularUser_ReturnsEnabledItemsWithFlagTrue()
    {
        _repository.Setup(item => item.ListPublishedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([Entity("enabled-1", visible: true)]);

        var result = await _handler.HandleAsync(new ListEntitiesQuery(IsAdministrator: false), CancellationToken.None);

        result.Single().IsVisibleByAdmin.Should().BeTrue();
    }

    private static CmsEntity Entity(
        string id,
        bool visible,
        int version = 1,
        DateTimeOffset? updatedAt = null,
        string payload = "{}")
        => new()
        {
            Id = id,
            IsVisibleByAdmin = visible,
            LatestVersion = version,
            UpdatedAt = updatedAt ?? new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            Payload = payload,
            IsPublished = true,
        };
}
