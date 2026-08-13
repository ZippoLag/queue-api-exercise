using System.Text.Json;
using System.Text.Json.Serialization;

namespace CmsWebhook.Domain;

/// <summary>
/// The JSON body the CMS webhook expects, sent either as a single object or as one element of a batch array.
/// </summary>
/// <remarks>
/// Glossary: a <b>CmsRequest</b> is what the webhook CMS API expects. It is only the transport shape
/// coming off the wire; correctness is established by <see cref="CmsRequestValidator"/>, which turns a
/// valid request into the <see cref="CmsEvent"/> to record. The JSON property names match the initial
/// requirements' schema exactly (lowercase <c>type</c>, <c>id</c>, <c>payload</c>, <c>version</c>,
/// <c>timestamp</c>).
/// </remarks>
public class CmsRequest
{
    /// <summary>
    /// The operation performed upon the entity: one of the <see cref="CmsEventType"/> wire values.
    /// </summary>
    /// <value>One of <c>"publish"</c>, <c>"update"</c>, <c>"unPublish"</c> or <c>"delete"</c>, case-sensitive.</value>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>
    /// The external entity's id.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>
    /// The actual data of the entity, expected to be a JSON object for non-delete events.
    /// </summary>
    [JsonPropertyName("payload")]
    public JsonElement? Payload { get; set; }

    /// <summary>
    /// The entity's version number coming from the external system; the first version is <c>1</c>.
    /// </summary>
    [JsonPropertyName("version")]
    public int? Version { get; set; }

    /// <summary>
    /// ISO 8601 (aka RFC 3339) date-time of when the event happened in the external CMS.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public string? Timestamp { get; set; }
}
