using CmsWebhook.Application;
using CmsWebhook.Domain;
using FluentAssertions;

namespace CmsWebhook.Application.Tests;

/// <summary>
/// Unit tests for <see cref="CmsEventProcessingRules"/>, covering every rule of the event-ingestion spec:
/// create on unknown id, idempotent re-delivery, same-version flag flips, stale events, hard delete, and
/// the never-published unpublish corner case.
/// </summary>
public class CmsEventProcessingRulesTests
{
    /// <summary>
    /// Verifies publish on an unknown id creates a published entity.
    /// </summary>
    /// <remarks>Source business rule: spec scenario "Publish creates a new entity".</remarks>
    [Fact]
    public void Apply_PublishOnUnknownId_CreatesPublishedEntity()
    {
        var @event = Event(CmsEventType.Publish, "entity-1", version: 2, payload: """{"v":2}""");

        var outcome = CmsEventProcessingRules.Apply(@event, current: null, identicalTupleProcessed: false);

        outcome.Kind.Should().Be(OutcomeKind.Upsert);
        outcome.Entity!.Id.Should().Be("entity-1");
        outcome.Entity.LatestVersion.Should().Be(2);
        outcome.Entity.Payload.Should().Be("""{"v":2}""");
        outcome.Entity.IsPublished.Should().BeTrue();
        outcome.Entity.IsVisibleByAdmin.Should().BeTrue();
    }

    /// <summary>
    /// Verifies update on an unknown id creates an unpublished entity.
    /// </summary>
    /// <remarks>Source business rule: <c>update</c> never publishes; nothing published it, so it stays unpublished.</remarks>
    [Fact]
    public void Apply_UpdateOnUnknownId_CreatesUnpublishedEntity()
    {
        var @event = Event(CmsEventType.Update, "entity-1", version: 1, payload: "{}");

        var outcome = CmsEventProcessingRules.Apply(@event, current: null, identicalTupleProcessed: false);

        outcome.Entity!.IsPublished.Should().BeFalse();
    }

    /// <summary>
    /// Verifies unPublish on an unknown id creates an unpublished entity (the corner case).
    /// </summary>
    /// <remarks>Source business rule: spec scenario "Unpublish without a prior publish" — the latest version
    /// is stored even though the entity was never published.</remarks>
    [Fact]
    public void Apply_UnPublishOnUnknownId_StoresLatestVersionUnpublished()
    {
        var @event = Event(CmsEventType.UnPublish, "entity-1", version: 3, payload: """{"v":3}""");

        var outcome = CmsEventProcessingRules.Apply(@event, current: null, identicalTupleProcessed: false);

        outcome.Kind.Should().Be(OutcomeKind.Upsert);
        outcome.Entity!.LatestVersion.Should().Be(3);
        outcome.Entity.Payload.Should().Be("""{"v":3}""");
        outcome.Entity.IsPublished.Should().BeFalse();
    }

    /// <summary>
    /// Verifies an identical re-delivered event is a no-op.
    /// </summary>
    /// <remarks>Source business rule: spec scenario "Identical re-delivered event is a no-op".</remarks>
    [Fact]
    public void Apply_IdenticalTupleAlreadyProcessed_ReturnsNoOp()
    {
        var @event = Event(CmsEventType.Publish, "entity-1", version: 2, payload: """{"v":2}""");
        var current = Entity("entity-1", latestVersion: 2, payload: """{"v":2}""", isPublished: true);

        var outcome = CmsEventProcessingRules.Apply(@event, current, identicalTupleProcessed: true);

        outcome.Kind.Should().Be(OutcomeKind.NoOp);
    }

    /// <summary>
    /// Verifies a publish followed by an unPublish of the same version flips the entity to not published.
    /// </summary>
    /// <remarks>Source business rule: spec scenario "Publish then unpublish of the same version".</remarks>
    [Fact]
    public void Apply_PublishThenUnPublishSameVersion_FlipsToUnpublished()
    {
        var @event = Event(CmsEventType.UnPublish, "entity-1", version: 2, payload: """{"v":2}""");
        var current = Entity("entity-1", latestVersion: 2, payload: """{"v":2}""", isPublished: true);

        var outcome = CmsEventProcessingRules.Apply(@event, current, identicalTupleProcessed: false);

        outcome.Kind.Should().Be(OutcomeKind.Upsert);
        outcome.Entity!.IsPublished.Should().BeFalse();
        outcome.Entity.LatestVersion.Should().Be(2);
    }

    /// <summary>
    /// Verifies an unPublish followed by a publish of the same version flips the entity to published.
    /// </summary>
    /// <remarks>Source business rule: spec scenario "Unpublish then publish of the same version".</remarks>
    [Fact]
    public void Apply_UnPublishThenPublishSameVersion_FlipsToPublished()
    {
        var @event = Event(CmsEventType.Publish, "entity-1", version: 2, payload: """{"v":2}""");
        var current = Entity("entity-1", latestVersion: 2, payload: """{"v":2}""", isPublished: false);

        var outcome = CmsEventProcessingRules.Apply(@event, current, identicalTupleProcessed: false);

        outcome.Kind.Should().Be(OutcomeKind.Upsert);
        outcome.Entity!.IsPublished.Should().BeTrue();
    }

    /// <summary>
    /// Verifies update of the same version replaces content without touching the published flag.
    /// </summary>
    /// <remarks>Source business rule: <c>update</c> replaces content without modifying the published flag.</remarks>
    [Fact]
    public void Apply_UpdateSameVersion_KeepsPublishedFlag()
    {
        var @event = Event(CmsEventType.Update, "entity-1", version: 2, payload: """{"v":2,"revised":true}""");
        var current = Entity("entity-1", latestVersion: 2, payload: """{"v":2}""", isPublished: true);

        var outcome = CmsEventProcessingRules.Apply(@event, current, identicalTupleProcessed: false);

        outcome.Entity!.IsPublished.Should().BeTrue();
        outcome.Entity.Payload.Should().Be("""{"v":2,"revised":true}""");
    }

    /// <summary>
    /// Verifies a newer version replaces the stored payload and version.
    /// </summary>
    /// <remarks>Source business rule: spec "The entity store tracks the latest version".</remarks>
    [Fact]
    public void Apply_NewerVersion_ReplacesPayloadAndVersion()
    {
        var @event = Event(CmsEventType.Publish, "entity-1", version: 3, payload: """{"v":3}""");
        var current = Entity("entity-1", latestVersion: 2, payload: """{"v":2}""", isPublished: true);

        var outcome = CmsEventProcessingRules.Apply(@event, current, identicalTupleProcessed: false);

        outcome.Entity!.LatestVersion.Should().Be(3);
        outcome.Entity.Payload.Should().Be("""{"v":3}""");
        outcome.Entity.IsPublished.Should().BeTrue();
    }

    /// <summary>
    /// Verifies an older (stale) version is ignored.
    /// </summary>
    /// <remarks>Source business rule: spec scenario "Stale event is ignored" — the stored latest version must not regress.</remarks>
    [Fact]
    public void Apply_StaleVersion_ReturnsNoOp()
    {
        var @event = Event(CmsEventType.Publish, "entity-1", version: 3, payload: """{"v":3}""");
        var current = Entity("entity-1", latestVersion: 5, payload: """{"v":5}""", isPublished: true);

        var outcome = CmsEventProcessingRules.Apply(@event, current, identicalTupleProcessed: false);

        outcome.Kind.Should().Be(OutcomeKind.NoOp);
    }

    /// <summary>
    /// Verifies delete on an existing entity hard-deletes it.
    /// </summary>
    /// <remarks>Source business rule: spec scenario "Delete removes the entity".</remarks>
    [Fact]
    public void Apply_DeleteOnExistingEntity_ReturnsDelete()
    {
        var @event = Event(CmsEventType.Delete, "entity-1");
        var current = Entity("entity-1", latestVersion: 2, payload: "{}", isPublished: true);

        var outcome = CmsEventProcessingRules.Apply(@event, current, identicalTupleProcessed: false);

        outcome.Kind.Should().Be(OutcomeKind.Delete);
        outcome.EntityId.Should().Be("entity-1");
    }

    /// <summary>
    /// Verifies delete on an unknown entity does nothing.
    /// </summary>
    /// <remarks>Source business rule: spec scenario "Delete of an unknown id does nothing".</remarks>
    [Fact]
    public void Apply_DeleteOnUnknownEntity_ReturnsNoOp()
    {
        var @event = Event(CmsEventType.Delete, "entity-1");

        var outcome = CmsEventProcessingRules.Apply(@event, current: null, identicalTupleProcessed: false);

        outcome.Kind.Should().Be(OutcomeKind.NoOp);
    }

    /// <summary>
    /// Verifies a versionless non-delete event is a visible failure, not a silent corruption.
    /// </summary>
    /// <remarks>The validation contract guarantees a version for non-delete events; the guard surfaces a
    /// data anomaly in the outbox instead of corrupting latest-version tracking.</remarks>
    [Fact]
    public void Apply_VersionlessNonDelete_Throws()
    {
        var @event = Event(CmsEventType.Publish, "entity-1");

        var act = () => CmsEventProcessingRules.Apply(@event, current: null, identicalTupleProcessed: false);

        act.Should().Throw<InvalidOperationException>();
    }

    private static CmsEvent Event(CmsEventType type, string entityId, int? version = null, string? payload = null)
        => new()
        {
            Id = Random.Shared.NextInt64(),
            EntityId = entityId,
            Type = type,
            Version = version,
            Payload = payload,
            Timestamp = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            Status = CmsEventStatus.Pending,
        };

    private static CmsEntity Entity(string id, int latestVersion, string payload, bool isPublished)
        => new()
        {
            Id = id,
            LatestVersion = latestVersion,
            Payload = payload,
            IsPublished = isPublished,
        };
}
