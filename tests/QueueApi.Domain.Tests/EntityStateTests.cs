using QueueApi.Domain;
using Xunit;

namespace QueueApi.Domain.Tests;

public class EntityStateTests
{
    /// <summary>
    /// When a publish event arrives for an entity that does not yet exist, applying that event creates a published entity state with the correct id, payload, and version.
    /// </summary>
    [Fact]
    public void Publish_new_entity_creates_published_state()
    {
        var published = new EntityPublished(
            Id: "entity-1",
            PayloadJson: "{\"title\":\"Hello\"}",
            Version: 1,
            OccurredAt: DateTimeOffset.UtcNow
        );

        var state = EntityState.Apply(current: null, published);

        Assert.Equal("entity-1", state.Id);
        Assert.Equal(1, state.LatestVersion);
        Assert.Equal("{\"title\":\"Hello\"}", state.PayloadJson);
        Assert.True(state.IsPublished);
        Assert.False(state.IsAdminDisabled);
    }

    /// <summary>
    /// When two identical publish events arrive for an entity, the state remains unchanged.
    /// </summary>
    [Fact]
    public void Publish_event_idempotent()
    {
        var published = new EntityPublished(
            Id: "entity-1",
            PayloadJson: "{\"title\":\"Hello\"}",
            Version: 2,
            OccurredAt: DateTimeOffset.UtcNow.AddMinutes(5)
        );

        // First publish creates the state
        var firstState = EntityState.Apply(current: null, published);

        // Second identical publish should create same state (idempotent behavior)
        var secondState = EntityState.Apply(firstState, published);

        Assert.Equal(firstState.Id, secondState.Id);
        Assert.Equal(firstState.LatestVersion, secondState.LatestVersion);
        Assert.Equal(firstState.PayloadJson, secondState.PayloadJson);
    }
}
