using CmsWebhook.Application;
using CmsWebhook.Domain;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CmsWebhook.Infrastructure.Tests;

/// <summary>
/// Tests for <see cref="CmsEventProcessorWorker"/>'s sweep: pending rows are processed into the entity
/// store and a failing event does not stop the queue.
/// </summary>
public class CmsEventProcessorWorkerTests
{
    /// <summary>
    /// Verifies the sweep processes pending events into the entity store and marks them processed.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "Events are processed asynchronously" — events left pending (e.g. from a
    /// restart) are recovered by the worker's sweep.
    /// </remarks>
    [Fact]
    public async Task ProcessPendingAsync_ProcessesPendingEventsIntoEntityStore()
    {
        using var database = new CmsTestDatabase();
        using var provider = BuildProvider(database.ConnectionString);

        using (var scope = provider.CreateScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<ICmsEventLogRepository>();
            await repository.AddAsync(new[] { PublishEvent("entity-1", version: 1) }, CancellationToken.None);
        }

        var worker = CreateWorker(provider);
        await worker.ProcessPendingAsync(CancellationToken.None);

        using var context = database.CreateContext();
        var entity = await context.Entities.SingleAsync();
        entity.Id.Should().Be("entity-1");
        entity.LatestVersion.Should().Be(1);
        (await context.Events.SingleAsync()).Status.Should().Be(CmsEventStatus.Processed);
    }

    /// <summary>
    /// Verifies a failing event is marked failed and the remaining pending events still process.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "A failing event is marked failed and processing continues"; the
    /// malformed (versionless) event fails, the valid one still produces its entity.
    /// </remarks>
    [Fact]
    public async Task ProcessPendingAsync_ContinuesAfterFailingEvent()
    {
        using var database = new CmsTestDatabase();
        using var provider = BuildProvider(database.ConnectionString);

        using (var scope = provider.CreateScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<ICmsEventLogRepository>();
            await repository.AddAsync(
                new[]
                {
                    PublishEvent("entity-1", version: null),
                    PublishEvent("entity-2", version: 1),
                },
                CancellationToken.None);
        }

        var worker = CreateWorker(provider);
        await worker.ProcessPendingAsync(CancellationToken.None);

        using var context = database.CreateContext();
        var events = await context.Events.OrderBy(item => item.Id).ToListAsync();
        events[0].Status.Should().Be(CmsEventStatus.Failed);
        events[0].Error.Should().NotBeNullOrWhiteSpace();
        events[1].Status.Should().Be(CmsEventStatus.Processed);
        (await context.Entities.CountAsync()).Should().Be(1);
    }

    private static ServiceProvider BuildProvider(string connectionString)
    {
        var services = new ServiceCollection();
        services.AddCmsWebhookInfrastructure(connectionString);
        services.AddLogging();
        return services.BuildServiceProvider();
    }

    private static CmsEventProcessorWorker CreateWorker(ServiceProvider provider)
    {
        // AddHostedService only registers the worker as IHostedService, so the sweep method under test is
        // exercised through a directly constructed instance sharing the provider's services.
        var outbox = provider.GetRequiredService<OutboxChannel>();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        return new CmsEventProcessorWorker(outbox, scopeFactory, NullLogger<CmsEventProcessorWorker>.Instance);
    }

    private static CmsEvent PublishEvent(string entityId, int? version)
        => new()
        {
            EntityId = entityId,
            Type = CmsEventType.Publish,
            Version = version,
            Payload = version is null ? null : "{}",
            Timestamp = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            ReceivedAt = DateTimeOffset.UtcNow,
            Status = CmsEventStatus.Pending,
        };
}
