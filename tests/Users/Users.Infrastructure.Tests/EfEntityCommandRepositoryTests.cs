using CmsWebhook.Domain;
using FluentAssertions;

namespace Users.Infrastructure.Tests;

/// <summary>
/// Tests for <see cref="EfEntityCommandRepository"/>: loading and persisting visibility changes.
/// </summary>
public class EfEntityCommandRepositoryTests
{
    /// <summary>
    /// Verifies a missing entity resolves to null, which the handler maps to <c>404</c>.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "Administrator enables and disables entity visibility", scenario
    /// "Unknown entity id".
    /// </remarks>
    [Fact]
    public async Task GetByIdAsync_UnknownId_ReturnsNull()
    {
        using var database = new UsersTestDatabase();
        await using var context = database.CreateContext();
        var repository = new EfEntityCommandRepository(context);

        var entity = await repository.GetByIdAsync("missing", CancellationToken.None);

        entity.Should().BeNull();
    }

    /// <summary>
    /// Verifies the stored entity is loaded with its fields.
    /// </summary>
    [Fact]
    public async Task GetByIdAsync_ExistingEntity_ReturnsIt()
    {
        using var database = new UsersTestDatabase();
        await using var context = database.CreateContext();
        context.Entities.Add(Entity("entity-1", isVisibleByAdmin: false));
        await context.SaveChangesAsync();
        var repository = new EfEntityCommandRepository(context);

        var entity = await repository.GetByIdAsync("entity-1", CancellationToken.None);

        entity.Should().NotBeNull();
        entity!.Id.Should().Be("entity-1");
        entity.IsVisibleByAdmin.Should().BeFalse();
    }

    /// <summary>
    /// Verifies an update persists the visibility change to the shared table.
    /// </summary>
    /// <remarks>
    /// Source business rule: disabling an entity must survive so it stays hidden from regular users'
    /// listings (spec "Administrator enables and disables entity visibility").
    /// </remarks>
    [Fact]
    public async Task UpdateAsync_PersistsVisibilityChange()
    {
        using var database = new UsersTestDatabase();
        await using (var context = database.CreateContext())
        {
            context.Entities.Add(Entity("entity-1", isVisibleByAdmin: true));
            await context.SaveChangesAsync();
        }

        await using (var context = database.CreateContext())
        {
            var repository = new EfEntityCommandRepository(context);
            var entity = await repository.GetByIdAsync("entity-1", CancellationToken.None);
            entity!.IsVisibleByAdmin = false;

            await repository.UpdateAsync(entity, CancellationToken.None);
        }

        await using var readContext = database.CreateContext();
        var stored = await readContext.Entities.FindAsync("entity-1");
        stored!.IsVisibleByAdmin.Should().BeFalse();
    }

    private static CmsEntity Entity(string id, bool isVisibleByAdmin)
        => new()
        {
            Id = id,
            IsPublished = true,
            IsVisibleByAdmin = isVisibleByAdmin,
            LatestVersion = 1,
            Payload = "{}",
            UpdatedAt = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
        };
}
