using CmsWebhook.Application;
using CmsWebhook.Domain;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CmsWebhook.Infrastructure.Tests;

/// <summary>
/// Integration tests for <see cref="EfCmsEventProcessor"/>: processing rules applied end-to-end over the
/// entity store with real repositories and transactions.
/// </summary>
public class EfCmsEventProcessorTests
{
    /// <summary>
    /// Verifies a publish event creates a published entity and is marked processed.
    /// </summary>
    /// <remarks>Source business rule: spec scenarios "Publish creates a new entity" and "Event is processed after acceptance".</remarks>
    [Fact]
    public async Task Process_Publish_CreatesPublishedEntityAndMarksProcessed()
    {
        using var database = new CmsTestDatabase();
        await using var context = database.CreateContext();
        var processor = CreateProcessor(context);
        var @event = Event(CmsEventType.Publish, "entity-1", version: 1, payload: """{"v":1}""");
        await AddEventAsync(context, @event);

        await processor.ProcessAsync(@event, CancellationToken.None);

        var entity = await context.Entities.SingleAsync();
        entity.LatestVersion.Should().Be(1);
        entity.Payload.Should().Be("""{"v":1}""");
        entity.IsPublished.Should().BeTrue();

        var storedEvent = await context.Events.SingleAsync();
        storedEvent.Status.Should().Be(CmsEventStatus.Processed);
        storedEvent.ProcessedAt.Should().NotBeNull();
    }

    /// <summary>
    /// Verifies an identical re-delivered event leaves the entity unchanged and both events are processed.
    /// </summary>
    /// <remarks>Source business rule: spec scenario "Identical re-delivered event is a no-op".</remarks>
    [Fact]
    public async Task Process_IdenticalRedelivery_LeavesEntityUnchanged()
    {
        using var database = new CmsTestDatabase();
        await using var context = database.CreateContext();
        var processor = CreateProcessor(context);
        var first = Event(CmsEventType.Publish, "entity-1", version: 1, payload: """{"v":1}""");
        var duplicate = Event(CmsEventType.Publish, "entity-1", version: 1, payload: """{"v":1}""");
        await AddEventAsync(context, first, duplicate);

        await processor.ProcessAsync(first, CancellationToken.None);
        await processor.ProcessAsync(duplicate, CancellationToken.None);

        var entity = await context.Entities.SingleAsync();
        entity.LatestVersion.Should().Be(1);

        var events = await context.Events.OrderBy(item => item.Id).ToListAsync();
        events.Should().OnlyContain(item => item.Status == CmsEventStatus.Processed);
    }

    /// <summary>
    /// Verifies publish followed by unPublish of the same version flips the entity to not published.
    /// </summary>
    /// <remarks>Source business rule: spec scenario "Publish then unpublish of the same version".</remarks>
    [Fact]
    public async Task Process_PublishThenUnPublishSameVersion_Unpublishes()
    {
        using var database = new CmsTestDatabase();
        await using var context = database.CreateContext();
        var processor = CreateProcessor(context);
        var publish = Event(CmsEventType.Publish, "entity-1", version: 2, payload: """{"v":2}""");
        var unPublish = Event(CmsEventType.UnPublish, "entity-1", version: 2, payload: """{"v":2}""");
        await AddEventAsync(context, publish, unPublish);

        await processor.ProcessAsync(publish, CancellationToken.None);
        await processor.ProcessAsync(unPublish, CancellationToken.None);

        var entity = await context.Entities.SingleAsync();
        entity.IsPublished.Should().BeFalse();
        entity.LatestVersion.Should().Be(2);
    }

    /// <summary>
    /// Verifies delete hard-removes the entity and its event is processed.
    /// </summary>
    /// <remarks>Source business rule: spec scenario "Delete removes the entity".</remarks>
    [Fact]
    public async Task Process_Delete_RemovesEntity()
    {
        using var database = new CmsTestDatabase();
        await using var context = database.CreateContext();
        var processor = CreateProcessor(context);
        var publish = Event(CmsEventType.Publish, "entity-1", version: 1, payload: "{}");
        var delete = Event(CmsEventType.Delete, "entity-1");
        await AddEventAsync(context, publish, delete);

        await processor.ProcessAsync(publish, CancellationToken.None);
        await processor.ProcessAsync(delete, CancellationToken.None);

        (await context.Entities.CountAsync()).Should().Be(0);
        (await context.Events.CountAsync(item => item.Status == CmsEventStatus.Processed)).Should().Be(2);
    }

    /// <summary>
    /// Verifies a stale event (older version) is ignored.
    /// </summary>
    /// <remarks>Source business rule: spec scenario "Stale event is ignored".</remarks>
    [Fact]
    public async Task Process_StaleEvent_LeavesEntityUnchanged()
    {
        using var database = new CmsTestDatabase();
        await using var context = database.CreateContext();
        var processor = CreateProcessor(context);
        var newer = Event(CmsEventType.Publish, "entity-1", version: 5, payload: """{"v":5}""");
        var stale = Event(CmsEventType.Publish, "entity-1", version: 3, payload: """{"v":3}""");
        await AddEventAsync(context, newer, stale);

        await processor.ProcessAsync(newer, CancellationToken.None);
        await processor.ProcessAsync(stale, CancellationToken.None);

        var entity = await context.Entities.SingleAsync();
        entity.LatestVersion.Should().Be(5);
        entity.Payload.Should().Be("""{"v":5}""");
    }

    /// <summary>
    /// Verifies the never-published unpublish corner case stores the latest version unpublished.
    /// </summary>
    /// <remarks>Source business rule: spec scenario "Unpublish without a prior publish".</remarks>
    [Fact]
    public async Task Process_UnPublishWithoutPriorPublish_StoresLatestVersionUnpublished()
    {
        using var database = new CmsTestDatabase();
        await using var context = database.CreateContext();
        var processor = CreateProcessor(context);
        var unPublish = Event(CmsEventType.UnPublish, "entity-1", version: 3, payload: """{"v":3}""");
        await AddEventAsync(context, unPublish);

        await processor.ProcessAsync(unPublish, CancellationToken.None);

        var entity = await context.Entities.SingleAsync();
        entity.LatestVersion.Should().Be(3);
        entity.Payload.Should().Be("""{"v":3}""");
        entity.IsPublished.Should().BeFalse();
    }

    /// <summary>
    /// Verifies a failing event is marked failed and later events still process.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "A failing event is marked failed and processing continues". The
    /// versionless non-delete event violates the domain contract and fails visibly instead of corrupting
    /// the store.
    /// </remarks>
    [Fact]
    public async Task Process_FailingEvent_IsMarkedFailedAndProcessingContinues()
    {
        using var database = new CmsTestDatabase();
        await using var context = database.CreateContext();
        var processor = CreateProcessor(context);
        var malformed = Event(CmsEventType.Publish, "entity-1"); // versionless non-delete → throws
        var valid = Event(CmsEventType.Publish, "entity-2", version: 1, payload: "{}");
        await AddEventAsync(context, malformed, valid);

        await processor.ProcessAsync(malformed, CancellationToken.None);
        await processor.ProcessAsync(valid, CancellationToken.None);

        (await context.Entities.CountAsync()).Should().Be(1);
        var failed = await context.Events.SingleAsync(item => item.Id == malformed.Id);
        failed.Status.Should().Be(CmsEventStatus.Failed);
        failed.Error.Should().NotBeNullOrWhiteSpace();
        var processed = await context.Events.SingleAsync(item => item.Id == valid.Id);
        processed.Status.Should().Be(CmsEventStatus.Processed);
    }

    /// <summary>
    /// Verifies a failure to even record the failure is logged and swallowed, not propagated.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "A failing event is marked failed and processing continues"; when the
    /// store itself is unreachable the best-effort failure recording fails too, and the processor must
    /// still not throw — the worker's per-event guard depends on that (design D5: "Marking the event
    /// failed is best-effort"). A disposed context makes both the transaction and the failure-marking fail.
    /// </remarks>
    [Fact]
    public async Task Process_WhenFailureRecordingAlsoFails_DoesNotThrow()
    {
        using var database = new CmsTestDatabase();
        var context = database.CreateContext();
        var processor = CreateProcessor(context);
        var malformed = Event(CmsEventType.Publish, "entity-1"); // versionless non-delete → throws in rules
        context.Events.Add(malformed);
        await context.SaveChangesAsync();
        await context.DisposeAsync();

        var act = () => processor.ProcessAsync(malformed, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    private static EfCmsEventProcessor CreateProcessor(CmsDbContext context)
        => new(
            context,
            new EfCmsEntityRepository(context),
            new EfCmsEventLogRepository(context),
            NullLogger<EfCmsEventProcessor>.Instance);

    private static async Task AddEventAsync(CmsDbContext context, params CmsEvent[] events)
    {
        context.Events.AddRange(events);
        await context.SaveChangesAsync();
    }

    private static CmsEvent Event(CmsEventType type, string entityId, int? version = null, string? payload = null)
        => new()
        {
            EntityId = entityId,
            Type = type,
            Version = version,
            Payload = payload,
            Timestamp = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            ReceivedAt = DateTimeOffset.UtcNow,
            Status = CmsEventStatus.Pending,
        };
}
