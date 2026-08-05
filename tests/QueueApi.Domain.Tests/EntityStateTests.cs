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
}
