using System.Text.Json;
using CmsWebhook.Application;
using CmsWebhook.Domain;
using FluentAssertions;
using Moq;

namespace CmsWebhook.Application.Tests;

/// <summary>
/// Unit tests for <see cref="IngestCmsEventsCommandHandler"/>: single/batch acceptance, all-or-nothing
/// batch atomicity, and the outbox notification.
/// </summary>
public class IngestCmsEventsCommandHandlerTests
{
    private readonly Mock<ICmsEventLogRepository> _eventLog = new();
    private readonly Mock<IOutboxNotifier> _notifier = new();

    /// <summary>
    /// Verifies a single valid request is recorded and notifies the worker.
    /// </summary>
    /// <remarks>Source business rule: spec "Events are recorded before processing" — accepted events are
    /// recorded as pending and the worker is signalled.</remarks>
    [Fact]
    public async Task HandleAsync_SingleValidRequest_RecordsEventAndNotifies()
    {
        using var payload = JsonDocument.Parse("""{"title":"hello"}""");
        var handler = CreateHandler();
        var request = new CmsRequest { Type = "publish", Id = "entity-1", Payload = payload.RootElement, Version = 1, Timestamp = "2024-01-01T00:00:00Z" };

        var result = await handler.HandleAsync(new[] { request }, CancellationToken.None);

        result.Success.Should().BeTrue();
        _eventLog.Verify(repo => repo.AddAsync(
            It.Is<IReadOnlyCollection<CmsEvent>>(events => events.Count == 1 && events.Single().EntityId == "entity-1"),
            It.IsAny<CancellationToken>()), Times.Once);
        _notifier.Verify(notifier => notifier.Notify(), Times.Once);
    }

    /// <summary>
    /// Verifies a fully valid batch is persisted in a single atomic write.
    /// </summary>
    /// <remarks>Source business rule: spec "Batch of events accepted" — every event is recorded on success.</remarks>
    [Fact]
    public async Task HandleAsync_ValidBatch_RecordsAllEventsInOneWrite()
    {
        using var payload = JsonDocument.Parse("{}");
        var handler = CreateHandler();
        var batch = new[]
        {
            new CmsRequest { Type = "publish", Id = "a", Payload = payload.RootElement, Version = 1, Timestamp = "2024-01-01T00:00:00Z" },
            new CmsRequest { Type = "update", Id = "b", Payload = payload.RootElement, Version = 2, Timestamp = "2024-01-01T00:00:00Z" },
            new CmsRequest { Type = "delete", Id = "c", Timestamp = "2024-01-01T00:00:00Z" },
        };

        var result = await handler.HandleAsync(batch, CancellationToken.None);

        result.Success.Should().BeTrue();
        _eventLog.Verify(repo => repo.AddAsync(
            It.Is<IReadOnlyCollection<CmsEvent>>(events => events.Count == 3),
            It.IsAny<CancellationToken>()), Times.Once);
        _notifier.Verify(notifier => notifier.Notify(), Times.Once);
    }

    /// <summary>
    /// Verifies a batch containing an invalid event is rejected without recording anything.
    /// </summary>
    /// <remarks>Source business rule: spec "Batch recording is atomic" — all-or-nothing; validation runs
    /// before any persistence.</remarks>
    [Fact]
    public async Task HandleAsync_BatchWithInvalidEvent_RejectsWithoutRecording()
    {
        using var payload = JsonDocument.Parse("{}");
        var handler = CreateHandler();
        var batch = new[]
        {
            new CmsRequest { Type = "publish", Id = "a", Payload = payload.RootElement, Version = 1, Timestamp = "2024-01-01T00:00:00Z" },
            new CmsRequest { Type = "deploy", Id = "b", Payload = payload.RootElement, Version = 1, Timestamp = "2024-01-01T00:00:00Z" },
        };

        var result = await handler.HandleAsync(batch, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNullOrWhiteSpace();
        _eventLog.Verify(repo => repo.AddAsync(It.IsAny<IReadOnlyCollection<CmsEvent>>(), It.IsAny<CancellationToken>()), Times.Never);
        _notifier.Verify(notifier => notifier.Notify(), Times.Never);
    }

    /// <summary>
    /// Verifies a single invalid request is rejected.
    /// </summary>
    /// <remarks>Source business rule: spec "Validates and sanitizes events" — invalid requests are rejected
    /// with nothing recorded.</remarks>
    [Fact]
    public async Task HandleAsync_InvalidRequest_Rejects()
    {
        var handler = CreateHandler();
        var invalid = new CmsRequest { Type = "publish", Id = "a", Payload = null, Version = 0, Timestamp = "2024-01-01T00:00:00Z" };

        var result = await handler.HandleAsync(new[] { invalid }, CancellationToken.None);

        result.Success.Should().BeFalse();
        _eventLog.Verify(repo => repo.AddAsync(It.IsAny<IReadOnlyCollection<CmsEvent>>(), It.IsAny<CancellationToken>()), Times.Never);
        _notifier.Verify(notifier => notifier.Notify(), Times.Never);
    }

    private IngestCmsEventsCommandHandler CreateHandler()
        => new(_eventLog.Object, _notifier.Object);
}
