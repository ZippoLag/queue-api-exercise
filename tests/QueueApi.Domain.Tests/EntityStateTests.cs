using QueueApi.Domain;
using Xunit;

namespace QueueApi.Domain.Tests;

/// <summary>
/// Contains Unit Tests related to the EntityState used by the CMS to track the current state of Entities and their changes.
/// </summary>
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

    /// <summary>
    /// When an unpublish event arrives for a published entity, applying that event preserves the entity data but marks it as unpublished.
    /// According to requirements: "unpublish should still keep the data in your persistence layer" and "an 'unpublish' event will contain the entity fields, as well as the version that is being unpublished".
    /// </summary>
    [Fact]
    public void Unpublish_existing_published_entity_preserves_data_but_marks_as_unpublished()
    {
        // Arrange: Create a published entity
        var initialPublish = new EntityPublished(
            Id: "entity-1",
            PayloadJson: "{\"title\":\"Hello\"}",
            Version: 3,
            OccurredAt: DateTimeOffset.UtcNow.AddHours(-1)
        );

        var publishedState = EntityState.Apply(current: null, initialPublish);

        // Act: Apply unpublish event
        var unpublished = new EntityUnpublished(
            Id: "entity-1",
            Version: 3,
            OccurredAt: DateTimeOffset.UtcNow
        );

        var resultState = EntityState.Apply(publishedState, unpublished);

        // Assert: Data is preserved but entity is marked as unpublished
        Assert.Equal("entity-1", resultState.Id);
        Assert.Equal(3, resultState.LatestVersion);
        Assert.Equal("{\"title\":\"Hello\"}", resultState.PayloadJson); // Payload preserved
        Assert.False(resultState.IsPublished); // Now unpublished
        Assert.False(resultState.IsAdminDisabled);
    }
}