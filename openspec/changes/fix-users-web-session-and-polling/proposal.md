## Why

Two defects make the Users.Web browser UI lose work and mislead the user: refreshing the page drops the sign-in session because credentials live only in component memory, and with polling checked the scheduled `GET /entities` calls run but the table is not re-rendered until the user interacts with the page again. Both are client-side defects in the Blazor WASM page and neither requires any API change.

## What Changes

- **Persist the sign-in session across page refreshes.** After a successful sign-in, store the session credentials in the browser's `sessionStorage`; on application load, restore them and silently re-authenticate, showing the entity table directly instead of the sign-in form. Clearing `sessionStorage` on sign-out keeps the stored session scoped to the user's explicit sign-in.
- **Fix polling re-rendering.** When the polling toggle is checked, each scheduled refresh updates the entity table and inline error state in the component, and the UI re-renders on every poll — no user interaction required to see fresh data.
- Keep the session restore and polling behavior entirely client-side in `src/Users/Users.Web`; make no changes to Users API routes, response contracts, authentication, authorization, or persistence.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `users-api`: Extend the browser UI requirement so the sign-in session survives a page refresh (restored from `sessionStorage`) and so polling refreshes visibly re-render the entity table on each scheduled call.

## Impact

- Affected frontend code under `src/Users/Users.Web`, primarily `Pages/Home.razor` (session lifecycle, storage interop, polling loop re-render) and its hosting shell if a storage helper needs registration.
- Verification via focused component/E2E coverage or manual browser checks: refresh keeps the user signed in, sign-out clears the stored session, and the table updates on each poll tick without user interaction.
- No API, authentication, persistence, deployment, or external dependency changes are expected.
