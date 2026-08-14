using CmsWebhook.Application;
using CmsWebhook.Domain;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

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

    /// <summary>
    /// Verifies a sweep failure inside the loop is logged and the worker retries on the next cycle.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "Events are processed asynchronously" — a transient store failure must
    /// not kill the worker; the sweep is retried on the next cycle (the timer is the durability safety net).
    /// The mocked repository fails only on the second sweep, then recovers; two channel notifications wake
    /// the loop without waiting for the 5-second sweep timer.
    /// </remarks>
    [Fact]
    public async Task ExecuteAsync_WhenSweepFails_LogsAndContinuesLooping()
    {
        var repository = new Mock<ICmsEventLogRepository>();
        var processor = new Mock<ICmsEventProcessor>();
        var callCount = 0;
        repository.Setup(x => x.GetPendingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 2)
                {
                    throw new InvalidOperationException("sweep boom");
                }

                return Array.Empty<CmsEvent>();
            });
        var (worker, outbox) = CreateWorkerWithMocks(repository, processor);
        var services = new ServiceCollection();
        services.AddSingleton<CmsEventProcessorWorker>(worker);
        services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<CmsEventProcessorWorker>());
        using var provider = services.BuildServiceProvider();
        var hosted = provider.GetRequiredService<IHostedService>();

        await hosted.StartAsync(CancellationToken.None);
        outbox.Notify();
        outbox.Notify();
        await WaitUntilAsync(() => callCount >= 3);
        await hosted.StopAsync(CancellationToken.None);

        repository.Verify(x => x.GetPendingAsync(It.IsAny<CancellationToken>()), Times.AtLeast(3));
    }

    /// <summary>
    /// Verifies an event whose processing throws does not stop the sweep and is logged as pending.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "A failing event is marked failed and processing continues"; the
    /// per-event guard in the worker keeps the queue moving even when the processor itself throws
    /// (as opposed to marking the event failed, which is the processor's own contract).
    /// </remarks>
    [Fact]
    public async Task ProcessPendingAsync_WhenProcessorThrows_LogsAndContinues()
    {
        var @event = PublishEvent("entity-1", version: 1);
        var repository = new Mock<ICmsEventLogRepository>();
        repository.Setup(x => x.GetPendingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { @event });
        var processor = new Mock<ICmsEventProcessor>();
        processor.Setup(x => x.ProcessAsync(@event, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("processing boom"));
        var (worker, _) = CreateWorkerWithMocks(repository, processor);

        var act = () => worker.ProcessPendingAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
        processor.Verify(x => x.ProcessAsync(@event, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Verifies a cancellation observed mid-processing stops the sweep silently.
    /// </summary>
    /// <remarks>
    /// The per-event guard distinguishes a cooperative shutdown (<see cref="OperationCanceledException"/>
    /// with the cancellation requested) from a real failure: shutdown stops the sweep, failure is logged
    /// and the queue keeps moving.
    /// </remarks>
    [Fact]
    public async Task ProcessPendingAsync_WhenCancelledDuringProcessing_StopsWithoutThrowing()
    {
        var @event = PublishEvent("entity-1", version: 1);
        var repository = new Mock<ICmsEventLogRepository>();
        repository.Setup(x => x.GetPendingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { @event });
        var processor = new Mock<ICmsEventProcessor>();
        processor.Setup(x => x.ProcessAsync(@event, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var (worker, _) = CreateWorkerWithMocks(repository, processor);

        var act = () => worker.ProcessPendingAsync(cts.Token);

        await act.Should().NotThrowAsync();
    }

    private static (CmsEventProcessorWorker Worker, OutboxChannel Outbox) CreateWorkerWithMocks(
        Mock<ICmsEventLogRepository> repository,
        Mock<ICmsEventProcessor> processor)
    {
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(x => x.GetService(typeof(ICmsEventLogRepository))).Returns(repository.Object);
        serviceProvider.Setup(x => x.GetService(typeof(ICmsEventProcessor))).Returns(processor.Object);
        var scope = new Mock<IServiceScope>();
        scope.Setup(x => x.ServiceProvider).Returns(serviceProvider.Object);
        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(x => x.CreateScope()).Returns(scope.Object);

        var outbox = new OutboxChannel();
        var worker = new CmsEventProcessorWorker(outbox, scopeFactory.Object, NullLogger<CmsEventProcessorWorker>.Instance);
        return (worker, outbox);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(50);
        }

        condition().Should().BeTrue("the expected worker cycle should have happened within the timeout");
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
