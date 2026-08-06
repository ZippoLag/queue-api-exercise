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

    /// <summary>
    /// When a delete event arrives for an entity, applying that event should return null to indicate the entity is hard-deleted and removed from the system.
    /// According to requirements: "Deleted entities should be removed (hard-delete)" and the schema example shows delete events with only id and timestamp.
    /// </summary>
    [Fact]
    public void Delete_entity_returns_null_for_hard_delete()
    {
        // Arrange: Create a published entity
        var initialPublish = new EntityPublished(
            Id: "entity-1",
            PayloadJson: "{\"title\":\"Hello\"}",
            Version: 2,
            OccurredAt: DateTimeOffset.UtcNow.AddHours(-1)
        );

        var publishedState = EntityState.Apply(current: null, initialPublish);

        // Act: Apply delete event
        var deleted = new EntityDeleted(
            Id: "entity-1",
            OccurredAt: DateTimeOffset.UtcNow
        );

        var resultState = EntityState.Apply(publishedState, deleted);

        // Assert: Should return null for hard-delete
        Assert.Null(resultState);
    }

    /// <summary>
    /// When an admin disables an entity via the API, the entity state preserves all data but marks it as admin-disabled.
    /// According to requirements: "Data can not be updated by any kind of users, but an admin can disable them from the API - this will not affect the CMS, it's an overwrite that does not affect CMS data!".
    /// This is distinct from unpublish which comes from the CMS — admin disable is an API-level overwrite that doesn't affect the underlying CMS data.
    /// </summary>
    [Fact]
    public void Admin_disable_preserves_entity_data_but_marks_as_admin_disabled()
    {
        // Arrange: Create a published entity
        var initialPublish = new EntityPublished(
            Id: "entity-1",
            PayloadJson: "{\"title\":\"Hello\"}",
            Version: 2,
            OccurredAt: DateTimeOffset.UtcNow.AddHours(-1)
        );

        var publishedState = EntityState.Apply(current: null, initialPublish);

        // Act: Admin disables the entity - this will fail as EntityAdminDisabled and corresponding Apply don't exist yet
        var adminDisable = new EntityAdminDisabled(
            Id: "entity-1",
            OccurredAt: DateTimeOffset.UtcNow
        );

        var resultState = EntityState.Apply(publishedState, adminDisable);

        // Assert: Data is preserved but admin-disabled flag is set
        Assert.Equal("entity-1", resultState.Id);
        Assert.Equal(2, resultState.LatestVersion);
        Assert.Equal("{\"title\":\"Hello\"}", resultState.PayloadJson);
        Assert.True(resultState.IsPublished); // Still published (admin disable is independent of publish state)
        Assert.True(resultState.IsAdminDisabled); // Now admin-disabled
    }
}