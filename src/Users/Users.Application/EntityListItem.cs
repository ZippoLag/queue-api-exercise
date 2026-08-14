namespace Users.Application;

/// <summary>
/// One item of the <c>GET /entities</c> response.
/// </summary>
/// <remarks>
/// Spec "Response items include the entity id and visibility flag": each returned item carries the
/// entity's id and its administrator-visibility flag so the administrator can discover which entities to
/// target with the enable/disable commands, alongside the version, update time and payload. The shape is
/// uniform for both roles; a regular user only ever receives enabled items, so their flag is always true.
/// </remarks>
/// <param name="Id">The external entity's id.</param>
/// <param name="IsVisibleByAdmin">Whether an administrator has disabled the entity for regular users.</param>
/// <param name="LatestVersion">The latest known data version.</param>
/// <param name="UpdatedAt">When the latest version was last updated.</param>
/// <param name="Payload">The latest payload as a raw JSON object string.</param>
public sealed record EntityListItem(
    string Id,
    bool IsVisibleByAdmin,
    int LatestVersion,
    DateTimeOffset UpdatedAt,
    string Payload);
