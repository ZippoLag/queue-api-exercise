using System.Globalization;
using System.Text.Json;

namespace CmsWebhook.Domain;

/// <summary>
/// Validates and sanitizes a <see cref="CmsRequest"/>, producing the <see cref="CmsEvent"/> to record when valid.
/// </summary>
/// <remarks>
/// Implements the architecture's request definition: all fields are required with a valid value, except
/// <c>payload</c> and <c>version</c> which <c>delete</c> events omit (and whose presence on a delete is
/// ignored). "First version added is version 1" is enforced as <c>version ≥ 1</c>. The payload must be a
/// JSON object — "checked to be a valid json key/value object and nothing else" (spec: Validates and
/// sanitizes events).
/// </remarks>
public static class CmsRequestValidator
{
    /// <summary>
    /// Validates and sanitizes a request, producing the event to record.
    /// </summary>
    /// <param name="request">The request to validate; may be <see langword="null"/> (e.g. a null element in a batch).</param>
    /// <param name="event">The validated event when the request is valid, otherwise <see langword="null"/>.</param>
    /// <param name="error">A human-readable description of the first validation failure.</param>
    /// <returns><see langword="true"/> when the request is valid and an event was produced.</returns>
    public static bool TryValidate(CmsRequest? request, out CmsEvent? @event, out string? error)
    {
        @event = null;
        error = null;

        if (request is null)
        {
            error = "A request must not be null.";
            return false;
        }

        if (!TryParseType(request.Type, out var type))
        {
            error = $"Unknown or missing 'type'. Expected one of: publish, update, unPublish, delete (case-sensitive).";
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.Id))
        {
            error = "'id' is required and must not be empty.";
            return false;
        }

        if (!DateTimeOffset.TryParse(
                request.Timestamp,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var timestamp))
        {
            error = "'timestamp' is required and must be parseable as an ISO 8601 / RFC 3339 date-time.";
            return false;
        }

        int? version = null;
        string? payloadJson = null;

        if (type == CmsEventType.Delete)
        {
            // A delete event omits payload/version in the requirements' schema; if present they are ignored.
            version = null;
            payloadJson = null;
        }
        else
        {
            if (request.Version is null or < 1)
            {
                error = "'version' is required for this event type and must be an integer of at least 1.";
                return false;
            }

            if (request.Payload is null)
            {
                error = "'payload' is required for this event type.";
                return false;
            }

            if (request.Payload.Value.ValueKind != JsonValueKind.Object)
            {
                error = "'payload' must be a JSON object (key/value), not an array or scalar value.";
                return false;
            }

            version = request.Version;
            payloadJson = request.Payload.Value.GetRawText();
        }

        @event = new CmsEvent
        {
            EntityId = request.Id.Trim(),
            Type = type,
            Version = version,
            Payload = payloadJson,
            Timestamp = timestamp,
            ReceivedAt = DateTimeOffset.UtcNow,
            Status = CmsEventStatus.Pending,
        };
        return true;
    }

    /// <summary>
    /// The exact wire values of the four event types, matched case-sensitively.
    /// </summary>
    /// <remarks>Enum names are PascalCase ("Publish"), but the wire format is lowercase with a capital P in
    /// "unPublish"; an explicit map keeps the accepted values exactly the requirements' schema.</remarks>
    private static readonly IReadOnlyDictionary<string, CmsEventType> WireTypes =
        new Dictionary<string, CmsEventType>(StringComparer.Ordinal)
        {
            ["publish"] = CmsEventType.Publish,
            ["update"] = CmsEventType.Update,
            ["unPublish"] = CmsEventType.UnPublish,
            ["delete"] = CmsEventType.Delete,
        };

    private static bool TryParseType(string? value, out CmsEventType type)
    {
        type = default;
        // An exact, case-sensitive match: "Publish", "unpublish", "1" and "publish " are all rejected.
        return value is not null && WireTypes.TryGetValue(value, out type);
    }
}
