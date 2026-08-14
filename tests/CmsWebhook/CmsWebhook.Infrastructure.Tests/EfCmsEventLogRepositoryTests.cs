using CmsWebhook.Domain;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CmsWebhook.Infrastructure.Tests;

/// <summary>
/// Tests for <see cref="EfCmsEventLogRepository"/>: outbox insert, pending queries, and status transitions.
/// </summary>
public class EfCmsEventLogRepositoryTests
{
    /// <summary>
    /// Verifies recorded events are persisted with status pending.
    /// </summary>
    /// <remarks>Source business rule: spec "Events are recorded before processing".</remarks>
    [Fact]
    public async Task AddAsync_InsertsPendingEvents()
    {
        using var database = new CmsTestDatabase();
        await using var context = database.CreateContext();
        var repository = new EfCmsEventLogRepository(context);

        await repository.AddAsync(new[] { Event(1, "entity-1"), Event(2, "entity-2") }, CancellationToken.None);

        var stored = await context.Events.OrderBy(item => item.Id).ToListAsync();
        stored.Should().HaveCount(2);
        stored.Should().OnlyContain(item => item.Status == CmsEventStatus.Pending);
    }

    /// <summary>
    /// Verifies the pending query returns only unprocessed events, oldest first.
    /// </summary>
    /// <remarks>Source business rule: the outbox worker only re-processes pending rows.</remarks>
    [Fact]
    public async Task GetPendingAsync_ReturnsOnlyPendingInOrder()
    {
        using var database = new CmsTestDatabase();
        await using var context = database.CreateContext();
        var repository = new EfCmsEventLogRepository(context);
        await repository.AddAsync(new[] { Event(1, "entity-1"), Event(2, "entity-2") }, CancellationToken.None);
        await repository.MarkProcessedAsync(1, DateTimeOffset.UtcNow, CancellationToken.None);

        var pending = await repository.GetPendingAsync(CancellationToken.None);

        pending.Should().ContainSingle();
        pending[0].Id.Should().Be(2);
    }

    /// <summary>
    /// Verifies the identical-tuple query only matches previously processed events.
    /// </summary>
    /// <remarks>Source business rule: spec "Identical re-delivered event is a no-op".</remarks>
    [Fact]
    public async Task ExistsProcessedAsync_MatchesOnlyProcessedTuples()
    {
        using var database = new CmsTestDatabase();
        await using var context = database.CreateContext();
        var repository = new EfCmsEventLogRepository(context);
        await repository.AddAsync(new[] { Event(1, "entity-1") }, CancellationToken.None);

        (await repository.ExistsProcessedAsync("entity-1", CmsEventType.Publish, 1, CancellationToken.None))
            .Should().BeFalse();

        await repository.MarkProcessedAsync(1, DateTimeOffset.UtcNow, CancellationToken.None);

        (await repository.ExistsProcessedAsync("entity-1", CmsEventType.Publish, 1, CancellationToken.None))
            .Should().BeTrue();
        (await repository.ExistsProcessedAsync("entity-1", CmsEventType.Update, 1, CancellationToken.None))
            .Should().BeFalse();
        (await repository.ExistsProcessedAsync("entity-1", CmsEventType.Publish, 2, CancellationToken.None))
            .Should().BeFalse();
    }

    /// <summary>
    /// Verifies marking processed sets the status and timestamp.
    /// </summary>
    /// <remarks>Source business rule: processed events are marked so the sweeps skip them.</remarks>
    [Fact]
    public async Task MarkProcessed_SetsStatusAndTimestamp()
    {
        using var database = new CmsTestDatabase();
        await using var context = database.CreateContext();
        var repository = new EfCmsEventLogRepository(context);
        await repository.AddAsync(new[] { Event(1, "entity-1") }, CancellationToken.None);
        var processedAt = new DateTimeOffset(2024, 1, 2, 0, 0, 0, TimeSpan.Zero);

        await repository.MarkProcessedAsync(1, processedAt, CancellationToken.None);

        var stored = await context.Events.SingleAsync();
        stored.Status.Should().Be(CmsEventStatus.Processed);
        stored.ProcessedAt.Should().Be(processedAt);
        stored.Error.Should().BeNull();
    }

    /// <summary>
    /// Verifies marking failed records the error.
    /// </summary>
    /// <remarks>Source business rule: spec "A failing event is marked failed" — the error is preserved for investigation.</remarks>
    [Fact]
    public async Task MarkFailed_SetsStatusErrorAndTimestamp()
    {
        using var database = new CmsTestDatabase();
        await using var context = database.CreateContext();
        var repository = new EfCmsEventLogRepository(context);
        await repository.AddAsync(new[] { Event(1, "entity-1") }, CancellationToken.None);

        await repository.MarkFailedAsync(1, "boom", DateTimeOffset.UtcNow, CancellationToken.None);

        var stored = await context.Events.SingleAsync();
        stored.Status.Should().Be(CmsEventStatus.Failed);
        stored.Error.Should().Be("boom");
    }

    /// <summary>
    /// Verifies a status update for an unknown event id is a silent no-op.
    /// </summary>
    /// <remarks>
    /// The event log is append-only from the ingest side and status updates always target rows recorded
    /// earlier in the same transaction; a missing row therefore must not throw (e.g. when the event was
    /// removed by a manual store edit) — the update is simply skipped.
    /// </remarks>
    [Fact]
    public async Task MarkProcessed_WithUnknownEventId_DoesNothing()
    {
        using var database = new CmsTestDatabase();
        await using var context = database.CreateContext();
        var repository = new EfCmsEventLogRepository(context);
        await repository.AddAsync(new[] { Event(1, "entity-1") }, CancellationToken.None);

        await repository.MarkProcessedAsync(999, DateTimeOffset.UtcNow, CancellationToken.None);

        var stored = await context.Events.SingleAsync();
        stored.Status.Should().Be(CmsEventStatus.Pending);
    }

    private static CmsEvent Event(long id, string entityId)
        => new()
        {
            Id = id,
            EntityId = entityId,
            Type = CmsEventType.Publish,
            Version = 1,
            Payload = "{}",
            Timestamp = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            ReceivedAt = DateTimeOffset.UtcNow,
            Status = CmsEventStatus.Pending,
        };
}
