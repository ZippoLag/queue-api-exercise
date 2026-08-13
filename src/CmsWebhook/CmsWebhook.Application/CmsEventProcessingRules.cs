using CmsWebhook.Domain;

namespace CmsWebhook.Application;

/// <summary>
/// The outcome of applying the event-processing rules to a stored entity.
/// </summary>
/// <param name="Kind">Which action the worker must take.</param>
/// <param name="Entity">The entity to upsert when <see cref="Kind"/> is <see cref="OutcomeKind.Upsert"/>.</param>
/// <param name="EntityId">The entity to delete when <see cref="Kind"/> is <see cref="OutcomeKind.Delete"/>.</param>
public sealed record ProcessingOutcome(OutcomeKind Kind, CmsEntity? Entity = null, string? EntityId = null)
{
    /// <summary>Creates a no-op outcome (stale event, identical re-delivery, or delete of an unknown entity).</summary>
    public static ProcessingOutcome NoOp() => new(OutcomeKind.NoOp);

    /// <summary>Creates an upsert outcome for the given entity state.</summary>
    public static ProcessingOutcome Upsert(CmsEntity entity) => new(OutcomeKind.Upsert, Entity: entity);

    /// <summary>Creates a hard-delete outcome for the given entity id.</summary>
    public static ProcessingOutcome Delete(string entityId) => new(OutcomeKind.Delete, EntityId: entityId);
}

/// <summary>
/// The action the outbox worker takes after applying the processing rules.
/// </summary>
public enum OutcomeKind
{
    /// <summary>Leave the entity store untouched.</summary>
    NoOp = 0,

    /// <summary>Insert or update the stored entity.</summary>
    Upsert = 1,

    /// <summary>Hard-delete the stored entity.</summary>
    Delete = 2,
}

/// <summary>
/// The architecture's event-processing rules, applied per recorded event.
/// </summary>
/// <remarks>
/// Source business rules (architecture "Event processing" and the event-ingestion spec):
/// <list type="bullet">
/// <item><c>publish</c>/<c>update</c>/<c>unPublish</c> on an unknown id create the entity;</item>
/// <item>an event whose id, version <b>and</b> type were already processed is a no-op (idempotent re-delivery);</item>
/// <item>a different type for the same id and version is applied (publish/unPublish flip the published flag);</item>
/// <item>an event older than the stored version is ignored as stale;</item>
/// <item><c>delete</c> hard-deletes; a delete of an unknown id does nothing;</item>
/// <item><c>unPublish</c> always applies, even without a prior <c>publish</c>, so the latest version is
/// never lost (initial requirements' corner case).</item>
/// </list>
/// </remarks>
public static class CmsEventProcessingRules
{
    /// <summary>
    /// Computes the outcome for the given event against the current stored entity state.
    /// </summary>
    /// <param name="event">The event to apply.</param>
    /// <param name="current">The stored entity, or <see langword="null"/> when none exists.</param>
    /// <param name="identicalTupleProcessed">
    /// Whether an event with the same entity id, type and version was already processed.
    /// </param>
    /// <returns>The outcome the worker must execute.</returns>
    /// <exception cref="InvalidOperationException">A non-delete event has no version.</exception>
    public static ProcessingOutcome Apply(CmsEvent @event, CmsEntity? current, bool identicalTupleProcessed)
    {
        if (@event.Type == CmsEventType.Delete)
        {
            return current is null
                ? ProcessingOutcome.NoOp()
                : ProcessingOutcome.Delete(@event.EntityId);
        }

        if (@event.Version is null)
        {
            // Validation guarantees a version for non-delete events; a versionless non-delete row in the
            // outbox is a data anomaly and must surface as a visible failure instead of silently corrupting
            // the latest-version tracking.
            throw new InvalidOperationException(
                $"Event {@event.Id} of type {@event.Type} for entity {@event.EntityId} has no version.");
        }

        if (current is null)
        {
            // Create on unknown id. publish marks it published; update and unPublish create it unpublished
            // (nothing published it — including the requirements' corner case for unPublish).
            var created = new CmsEntity
            {
                Id = @event.EntityId,
                LatestVersion = @event.Version.Value,
                Payload = @event.Payload ?? string.Empty,
                IsPublished = @event.Type == CmsEventType.Publish,
                UpdatedAt = @event.Timestamp,
            };
            return ProcessingOutcome.Upsert(created);
        }

        if (@event.Version < current.LatestVersion)
        {
            // Out-of-order delivery of an older version; the stored latest version must not regress.
            return ProcessingOutcome.NoOp();
        }

        if (@event.Version == current.LatestVersion && identicalTupleProcessed)
        {
            // Identical re-delivery of an event that was already applied at this version.
            return ProcessingOutcome.NoOp();
        }

        current.LatestVersion = @event.Version.Value;
        current.Payload = @event.Payload ?? string.Empty;
        current.UpdatedAt = @event.Timestamp;
        if (@event.Type == CmsEventType.Publish)
        {
            current.IsPublished = true;
        }
        else if (@event.Type == CmsEventType.UnPublish)
        {
            current.IsPublished = false;
        }
        // update leaves the published flag untouched.

        return ProcessingOutcome.Upsert(current);
    }
}
