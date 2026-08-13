using System.Text.Json;
using CmsWebhook.Domain;
using FluentAssertions;

namespace CmsWebhook.Domain.Tests;

/// <summary>
/// Unit tests for <see cref="CmsRequestValidator"/>, covering every validation scenario of the
/// event-ingestion spec ("Validates and sanitizes events").
/// </summary>
public class CmsRequestValidatorTests
{
    /// <summary>
    /// Verifies a valid publish request produces a pending event with sanitized fields.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "Validates and sanitizes events"; the id is trimmed as part of
    /// sanitization and the first version is <c>1</c>.
    /// </remarks>
    [Fact]
    public void TryValidate_ValidPublish_CreatesPendingEvent()
    {
        using var payload = JsonDocument.Parse("""{"title":"hello"}""");
        var request = new CmsRequest
        {
            Type = "publish",
            Id = "  entity-1  ",
            Payload = payload.RootElement,
            Version = 1,
            Timestamp = "2024-01-01T00:00:00Z",
        };

        var valid = CmsRequestValidator.TryValidate(request, out var @event, out var error);

        valid.Should().BeTrue();
        error.Should().BeNull();
        @event!.EntityId.Should().Be("entity-1");
        @event.Type.Should().Be(CmsEventType.Publish);
        @event.Version.Should().Be(1);
        @event.Payload.Should().Be("""{"title":"hello"}""");
        @event.Status.Should().Be(CmsEventStatus.Pending);
        @event.Timestamp.Should().Be(DateTimeOffset.Parse("2024-01-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Verifies a valid unPublish request is accepted.
    /// </summary>
    /// <remarks>Source business rule: the four accepted types include <c>unPublish</c>.</remarks>
    [Fact]
    public void TryValidate_ValidUnPublish_IsAccepted()
    {
        using var payload = JsonDocument.Parse("""{"title":"bye"}""");
        var request = new CmsRequest
        {
            Type = "unPublish",
            Id = "entity-2",
            Payload = payload.RootElement,
            Version = 4,
            Timestamp = "2024-01-01T00:00:00+02:00",
        };

        var valid = CmsRequestValidator.TryValidate(request, out var @event, out var error);

        valid.Should().BeTrue();
        @event!.Type.Should().Be(CmsEventType.UnPublish);
        @event.Version.Should().Be(4);
        @event.Timestamp.Should().Be(DateTimeOffset.Parse("2024-01-01T00:00:00+02:00", System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Verifies an unknown event type is rejected.
    /// </summary>
    /// <remarks>Source business rule: only <c>publish</c>, <c>update</c>, <c>unPublish</c> and <c>delete</c> are accepted.</remarks>
    [Theory]
    [InlineData("deploy")]
    [InlineData("Publish")]
    [InlineData("unpublish")]
    [InlineData("1")]
    [InlineData("publish ")]
    public void TryValidate_InvalidType_IsRejected(string type)
    {
        using var payload = JsonDocument.Parse("{}");
        var request = new CmsRequest
        {
            Type = type,
            Id = "entity-1",
            Payload = payload.RootElement,
            Version = 1,
            Timestamp = "2024-01-01T00:00:00Z",
        };

        var valid = CmsRequestValidator.TryValidate(request, out var @event, out var error);

        valid.Should().BeFalse();
        @event.Should().BeNull();
        error.Should().Contain("type");
    }

    /// <summary>
    /// Verifies a missing event type is rejected.
    /// </summary>
    /// <remarks>Source business rule: spec scenario "Missing event type".</remarks>
    [Fact]
    public void TryValidate_MissingType_IsRejected()
    {
        using var payload = JsonDocument.Parse("{}");
        var request = new CmsRequest { Type = null, Id = "entity-1", Payload = payload.RootElement, Version = 1, Timestamp = "2024-01-01T00:00:00Z" };

        var valid = CmsRequestValidator.TryValidate(request, out var @event, out var error);

        valid.Should().BeFalse();
        error.Should().Contain("type");
    }

    /// <summary>
    /// Verifies an empty or whitespace-only id is rejected.
    /// </summary>
    /// <remarks>Source business rule: spec scenario "Empty id".</remarks>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void TryValidate_EmptyOrWhitespaceId_IsRejected(string id)
    {
        using var payload = JsonDocument.Parse("{}");
        var request = new CmsRequest { Type = "publish", Id = id, Payload = payload.RootElement, Version = 1, Timestamp = "2024-01-01T00:00:00Z" };

        var valid = CmsRequestValidator.TryValidate(request, out var @event, out var error);

        valid.Should().BeFalse();
        error.Should().Contain("id");
    }

    /// <summary>
    /// Verifies a missing or unparseable timestamp is rejected.
    /// </summary>
    /// <remarks>Source business rule: spec scenario "Invalid timestamp".</remarks>
    [Theory]
    [InlineData(null)]
    [InlineData("not-a-date")]
    [InlineData("2024-13-45T99:99:99Z")]
    public void TryValidate_InvalidTimestamp_IsRejected(string? timestamp)
    {
        using var payload = JsonDocument.Parse("{}");
        var request = new CmsRequest { Type = "publish", Id = "entity-1", Payload = payload.RootElement, Version = 1, Timestamp = timestamp };

        var valid = CmsRequestValidator.TryValidate(request, out var @event, out var error);

        valid.Should().BeFalse();
        error.Should().Contain("timestamp");
    }

    /// <summary>
    /// Verifies versions below one are rejected for non-delete events.
    /// </summary>
    /// <remarks>
    /// Source business rule: initial requirements "the first version added is version 1" and spec scenario
    /// "Version below one".
    /// </remarks>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void TryValidate_VersionBelowOne_IsRejected(int version)
    {
        using var payload = JsonDocument.Parse("{}");
        var request = new CmsRequest { Type = "publish", Id = "entity-1", Payload = payload.RootElement, Version = version, Timestamp = "2024-01-01T00:00:00Z" };

        var valid = CmsRequestValidator.TryValidate(request, out var @event, out var error);

        valid.Should().BeFalse();
        error.Should().Contain("version");
    }

    /// <summary>
    /// Verifies a missing version is rejected for non-delete events.
    /// </summary>
    /// <remarks>Source business rule: <c>version</c> is required except for <c>delete</c> events.</remarks>
    [Fact]
    public void TryValidate_MissingVersion_IsRejected()
    {
        using var payload = JsonDocument.Parse("{}");
        var request = new CmsRequest { Type = "publish", Id = "entity-1", Payload = payload.RootElement, Version = null, Timestamp = "2024-01-01T00:00:00Z" };

        var valid = CmsRequestValidator.TryValidate(request, out var @event, out var error);

        valid.Should().BeFalse();
        error.Should().Contain("version");
    }

    /// <summary>
    /// Verifies a missing payload is rejected for non-delete events.
    /// </summary>
    /// <remarks>Source business rule: spec scenario "Missing payload on a non-delete event".</remarks>
    [Fact]
    public void TryValidate_MissingPayload_IsRejected()
    {
        var request = new CmsRequest { Type = "update", Id = "entity-1", Payload = null, Version = 1, Timestamp = "2024-01-01T00:00:00Z" };

        var valid = CmsRequestValidator.TryValidate(request, out var @event, out var error);

        valid.Should().BeFalse();
        error.Should().Contain("payload");
    }

    /// <summary>
    /// Verifies a payload that is not a JSON object is rejected.
    /// </summary>
    /// <remarks>Source business rule: spec scenario "Payload is not a JSON object" — only key/value objects are accepted.</remarks>
    [Theory]
    [InlineData("[]")]
    [InlineData("\"a string\"")]
    [InlineData("5")]
    [InlineData("null")]
    [InlineData("true")]
    public void TryValidate_NonObjectPayload_IsRejected(string payloadJson)
    {
        using var payload = JsonDocument.Parse(payloadJson);
        var request = new CmsRequest { Type = "publish", Id = "entity-1", Payload = payload.RootElement, Version = 1, Timestamp = "2024-01-01T00:00:00Z" };

        var valid = CmsRequestValidator.TryValidate(request, out var @event, out var error);

        valid.Should().BeFalse();
        error.Should().Contain("payload");
    }

    /// <summary>
    /// Verifies a delete event without payload or version is accepted.
    /// </summary>
    /// <remarks>Source business rule: spec scenario "Delete without payload or version"; the requirements' schema omits both for delete.</remarks>
    [Fact]
    public void TryValidate_DeleteWithoutPayloadOrVersion_IsAccepted()
    {
        var request = new CmsRequest { Type = "delete", Id = "entity-1", Timestamp = "2024-01-01T00:00:00Z" };

        var valid = CmsRequestValidator.TryValidate(request, out var @event, out var error);

        valid.Should().BeTrue();
        error.Should().BeNull();
        @event!.Type.Should().Be(CmsEventType.Delete);
        @event.Version.Should().BeNull();
        @event.Payload.Should().BeNull();
    }

    /// <summary>
    /// Verifies extra payload and version on a delete event are ignored.
    /// </summary>
    /// <remarks>Source business rule: "extra fields on a delete event shall be ignored".</remarks>
    [Fact]
    public void TryValidate_DeleteWithPayloadAndVersion_IgnoresThem()
    {
        using var payload = JsonDocument.Parse("""{"title":"ignored"}""");
        var request = new CmsRequest { Type = "delete", Id = "entity-1", Payload = payload.RootElement, Version = 7, Timestamp = "2024-01-01T00:00:00Z" };

        var valid = CmsRequestValidator.TryValidate(request, out var @event, out var error);

        valid.Should().BeTrue();
        @event!.Version.Should().BeNull();
        @event.Payload.Should().BeNull();
    }

    /// <summary>
    /// Verifies a null request (e.g. a null element inside a batch array) is rejected.
    /// </summary>
    /// <remarks>Source business rule: batch members must be valid requests.</remarks>
    [Fact]
    public void TryValidate_NullRequest_IsRejected()
    {
        var valid = CmsRequestValidator.TryValidate(null, out var @event, out var error);

        valid.Should().BeFalse();
        @event.Should().BeNull();
        error.Should().NotBeNullOrWhiteSpace();
    }
}
