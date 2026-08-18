using CmsWebhook.Domain;
using FluentAssertions;

namespace Users.Infrastructure.Tests;

/// <summary>
/// Tests for <see cref="EfEntityQueryRepository"/>: the read-optimized listing of published entities.
/// </summary>
public class EfEntityQueryRepositoryTests
{
    /// <summary>
    /// Verifies the repository lists only published entities.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "Entities are listed by published status and administrator visibility" —
    /// unpublished entities are never listed for any user; the published filter is applied at the storage
    /// boundary so no role can see them.
    /// </remarks>
    [Fact]
    public async Task ListPublishedAsync_ReturnsOnlyPublishedEntities()
    {
        using var database = new UsersTestDatabase();
        await using var context = database.CreateContext();
        context.Entities.AddRange(
            Entity("published-1", isPublished: true),
            Entity("unpublished-1", isPublished: false),
            Entity("published-2", isPublished: true));
        await context.SaveChangesAsync();
        var repository = new EfEntityQueryRepository(context);

        var result = await repository.ListPublishedAsync(CancellationToken.None);

        result.Select(item => item.Id).Should().Equal("published-1", "published-2");
    }

    /// <summary>
    /// Verifies the repository returns disabled entities too — role filtering is the handler's job.
    /// </summary>
    /// <remarks>
    /// The read port is unfiltered by administrator visibility (design decision 2 of the
    /// users-api-vertical change): the query handler decides which roles see disabled entities, so the
    /// administrator's listing must find them here.
    /// </remarks>
    [Fact]
    public async Task ListPublishedAsync_IncludesDisabledEntities()
    {
        using var database = new UsersTestDatabase();
        await using var context = database.CreateContext();
        context.Entities.Add(Entity("disabled-1", isPublished: true, isVisibleByAdmin: false));
        await context.SaveChangesAsync();
        var repository = new EfEntityQueryRepository(context);

        var result = await repository.ListPublishedAsync(CancellationToken.None);

        var entity = result.Single();
        entity.Id.Should().Be("disabled-1");
        entity.IsVisibleByAdmin.Should().BeFalse();
    }

    /// <summary>
    /// Verifies an empty store yields an empty listing.
    /// </summary>
    [Fact]
    public async Task ListPublishedAsync_OnEmptyStore_ReturnsEmpty()
    {
        using var database = new UsersTestDatabase();
        await using var context = database.CreateContext();
        var repository = new EfEntityQueryRepository(context);

        var result = await repository.ListPublishedAsync(CancellationToken.None);

        result.Should().BeEmpty();
    }

    /// <summary>
    /// Verifies the listing query leaves EF's change tracker empty.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "Read and write paths use separated configurations" — the listing
    /// runs on a read-only configuration, so the returned entities are not tracked and the request
    /// cannot mutate the store.
    /// </remarks>
    [Fact]
    public async Task ListPublishedAsync_LeavesChangeTrackerEmpty()
    {
        using var database = new UsersTestDatabase();
        await using (var seedContext = database.CreateContext())
        {
            seedContext.Entities.Add(Entity("tracked-1", isPublished: true));
            await seedContext.SaveChangesAsync();
        }

        await using var context = database.CreateContext();
        var repository = new EfEntityQueryRepository(context);

        var result = await repository.ListPublishedAsync(CancellationToken.None);

        result.Should().HaveCount(1);
        context.ChangeTracker.Entries().Should().BeEmpty();
    }

    private static CmsEntity Entity(string id, bool isPublished, bool isVisibleByAdmin = true)
        => new()
        {
            Id = id,
            IsPublished = isPublished,
            IsVisibleByAdmin = isVisibleByAdmin,
            LatestVersion = 1,
            Payload = "{}",
            UpdatedAt = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
        };
}
