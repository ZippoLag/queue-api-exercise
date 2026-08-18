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
/// Sanitization means the accepted values are valid and safe to store: strings are non-empty, the id is
/// trimmed, and values are normalized to canonical types before being persisted through parameterized
/// queries. The payload is opaque: only its being a JSON object is enforced, its contents and format are
/// never inspected or transformed (spec: "Payload contents are opaque").
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

        if (!TryParseTimestamp(request.Timestamp, out var timestamp))
        {
            error = "'timestamp' is required and must be an ISO 8601 / RFC 3339 date-time, e.g. 2024-01-01T00:00:00Z or 2024-01-01T00:00:00+02:00.";
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

    /// <summary>
    /// The exact ISO 8601 / RFC 3339 date-time formats accepted, per the requirements' example
    /// <c>2024-01-01T00:00:00Z</c>: ending in <c>Z</c> or a numeric UTC offset, with optional fractional seconds.
    /// </summary>
    /// <remarks>
    /// RFC 3339 requires the <c>T</c> separator, seconds, and a <c>Z</c>/offset designator, so date-only,
    /// culture-formatted, and offset-less values are rejected even though the general
    /// <see cref="DateTimeOffset.TryParse(string, IFormatProvider?, DateTimeStyles, out DateTimeOffset)"/>
    /// overload would accept them (spec: "Invalid timestamp").
    /// </remarks>
    private static readonly string[] TimestampFormats =
    [
        "yyyy-MM-dd'T'HH:mm:ss'Z'",
        "yyyy-MM-dd'T'HH:mm:sszzz",
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'",
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz",
    ];

    private static bool TryParseTimestamp(string? value, out DateTimeOffset timestamp)
        => DateTimeOffset.TryParseExact(
            value,
            TimestampFormats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out timestamp);

    private static bool TryParseType(string? value, out CmsEventType type)
    {
        type = default;
        // An exact, case-sensitive match: "Publish", "unpublish", "1" and "publish " are all rejected.
        return value is not null && WireTypes.TryGetValue(value, out type);
    }
}
