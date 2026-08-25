## Context

The Users.Web Blazor WASM page (`src/Users/Users.Web/Pages/Home.razor`) keeps the signed-in credentials only in component fields (`_username`/`_password`), so a page refresh restarts the app and returns the user to the sign-in form. The polling loop added by change `add-users-web-polling` runs `LoadEntitiesAsync()` on a fire-and-forget background task; Blazor only re-renders after event-handler tasks complete or when `StateHasChanged` is invoked, so the network calls succeed, state mutates, and the rendered table stays stale until another event (for example unchecking the toggle) triggers a render. See `proposal.md - Why` for the reported defects. The UI must keep using same-origin Basic authentication against the existing endpoints; no API change is permitted.

## Goals / Non-Goals

**Goals:**

- Persist the signed-in session in `sessionStorage` so a refresh restores the authenticated entity view without showing the sign-in form.
- Clear the stored session on sign-out so a later refresh returns to the sign-in form.
- Make every scheduled poll visibly re-render the table and inline error state without user interaction.
- Keep both fixes local to the client (`src/Users/Users.Web`), reusing the existing request construction and Basic header path.

**Non-Goals:**

- Changing any Users API endpoint, authentication policy, contract, or persistence.
- Persistent logins across browser restarts (sessionStorage is cleared with the tab; `localStorage` was deliberately rejected).
- Introducing a server-side session or a cookie-based login endpoint.
- Adding a UI test harness (bUnit/Playwright) as part of this change; verification reuses the existing E2E smoke suite plus a manual browser checklist.

## Decisions

- **Persist the session in `window.sessionStorage` through `IJSRuntime`, wrapped in a small injected service.** The user chose sessionStorage over localStorage: it survives the refresh (the reported defect) while scoping the credential lifetime to the tab and clearing it when the tab closes, which limits exposure compared to disk-persistent storage. A thin `SessionStore` service keeps the JS interop strings and storage key out of the page component and is mockable if a UI test project is added later. Alternatives considered: direct `IJSRuntime` calls in `Home.razor` (fewer moving parts but couples the component to interop plumbing and storage keys); `ProtectedBrowserStorage` (encrypts at rest but pulls in data-protection machinery that is overkill for a demo tool and has a different story in WASM); `localStorage` (rejected by the user — survives browser restarts and keeps credentials on disk).
- **Store the same username/password pair the component already keeps in memory, as one JSON value under a single key.** The component rebuilds the Basic header from these values today; storing them unchanged means the restore path reuses the existing header builder with no new encoding. One key avoids partial-write states between two keys. The exposure is equivalent to the current in-memory model plus tab lifetime; it is documented as a trade-off (below).
- **Restore on `OnInitializedAsync`, silently, with fallback to the sign-in form.** On load the component reads the stored session; when present it sets the credentials and calls the existing `LoadEntitiesAsync()`, exactly like a successful sign-in. If the restore fails (stored credentials revoked or rotated, or the reserved `cms-webhook` user stored — which cannot happen since sign-in only stores on success), the component falls back to the sign-in form with the existing inline error. The stored session is written only after a successful sign-in load, so an invalid session is never persisted.
- **Clear the stored session on sign-out.** `SignOutAsync` removes the storage entry so a subsequent refresh shows the sign-in form, matching the spec scenario "Signing out clears the stored session".
- **Re-render after every poll tick with `InvokeAsync(StateHasChanged)`.** The polling loop runs off the renderer's event-handler path (a `PeriodicTimer` loop awaited from a fire-and-forget task), so its mutations to `_entities`/`_error` never trigger a render. Calling `StateHasChanged` marshalled through `InvokeAsync` after each `LoadEntitiesAsync()` in `PollEntitiesAsync` makes the table and inline errors update on every tick. This is the documented pattern for updating a component from background work; no render synchronization is needed because Blazor batches and queues render requests.

## Risks / Trade-offs

- [Risk] Basic credentials in `sessionStorage` are readable by any same-origin script (XSS), so persistence widens the exposure window beyond component memory. -> Mitigation: the user explicitly chose sessionStorage over localStorage, it is scoped to the tab and cleared on sign-out, and this is a demo tool with seeded credentials; the storage service keeps the mechanism in one place so a stronger scheme can replace it later.
- [Risk] A stored session can outlive its validity (password rotated, user removed). -> Mitigation: the restore path runs the same authenticated load as sign-in and falls back to the form with the inline error on failure, never silently showing stale data.
- [Risk] Restoring the session on every load adds one authenticated `GET /entities` per refresh. -> Mitigation: this is the same call the user would make by re-signing in manually and is the cost of an auto-restored session; no additional polling is scheduled on restore.
- [Risk] The polling re-render fix relies on calling `StateHasChanged` after every tick; forgetting the call reintroduces the stale-table defect. -> Mitigation: the call lives inside the single polling loop next to `LoadEntitiesAsync()`, and the manual verification checklist explicitly covers a visible table update while polling without interaction.

## Migration Plan

1. Add the `SessionStore` service (sessionStorage adapter) and register it in `Users.Web`'s `Program.cs`; inject it into `Pages/Home.razor`.
2. Write the stored session after successful sign-in, restore it in `OnInitializedAsync`, and clear it on sign-out.
3. Add `InvokeAsync(StateHasChanged)` after each `LoadEntitiesAsync()` inside the polling loop.
4. Build Users.Web and run the relevant solution tests; perform the manual browser verification checklist (refresh keeps the session, sign-out clears it, table updates during polling without interaction).
5. Roll back by reverting the client-side changes; no API, database, or deployment migration is involved.

## Open Questions

None. The storage scope (sessionStorage) and restore behavior (silent auto-restore) were confirmed with the user before planning.
