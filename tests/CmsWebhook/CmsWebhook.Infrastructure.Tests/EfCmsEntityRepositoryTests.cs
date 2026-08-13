using CmsWebhook.Domain;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CmsWebhook.Infrastructure.Tests;

/// <summary>
/// Tests for <see cref="EfCmsEntityRepository"/>: read, upsert and hard delete over the entity store.
/// </summary>
public class EfCmsEntityRepositoryTests
{
    /// <summary>
    /// Verifies a missing entity resolves to null.
    /// </summary>
    [Fact]
    public async Task GetByIdAsync_UnknownId_ReturnsNull()
    {
        using var database = new CmsTestDatabase();
        await using var context = database.CreateContext();
        var repository = new EfCmsEntityRepository(context);

        var entity = await repository.GetByIdAsync("entity-1", CancellationToken.None);

        entity.Should().BeNull();
    }

    /// <summary>
    /// Verifies the upsert inserts then updates the stored entity in place.
    /// </summary>
    /// <remarks>Source business rule: processing a newer version replaces the stored payload (spec
    /// "The entity store tracks the latest version").</remarks>
    [Fact]
    public async Task UpsertAsync_InsertsThenUpdates()
    {
        using var database = new CmsTestDatabase();
        await using var context = database.CreateContext();
        var repository = new EfCmsEntityRepository(context);

        await repository.UpsertAsync(Entity("entity-1", 1, """{"v":1}"""), CancellationToken.None);
        await repository.UpsertAsync(Entity("entity-1", 2, """{"v":2}"""), CancellationToken.None);

        var stored = await context.Entities.SingleAsync();
        stored.LatestVersion.Should().Be(2);
        stored.Payload.Should().Be("""{"v":2}""");
    }

    /// <summary>
    /// Verifies the delete hard-removes an existing entity and is a no-op for a missing one.
    /// </summary>
    /// <remarks>Source business rule: spec scenario "Delete removes the entity"; deletes of unknown ids do nothing.</remarks>
    [Fact]
    public async Task DeleteAsync_RemovesExistingAndIgnoresMissing()
    {
        using var database = new CmsTestDatabase();
        await using var context = database.CreateContext();
        var repository = new EfCmsEntityRepository(context);
        await repository.UpsertAsync(Entity("entity-1", 1, "{}"), CancellationToken.None);

        await repository.DeleteAsync("entity-1", CancellationToken.None);
        await repository.DeleteAsync("missing", CancellationToken.None);

        (await context.Entities.CountAsync()).Should().Be(0);
    }

    private static CmsEntity Entity(string id, int latestVersion, string payload)
        => new() { Id = id, LatestVersion = latestVersion, Payload = payload, IsPublished = true };
}
