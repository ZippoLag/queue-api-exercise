## Why

The Users.Web page only loads entity data during sign-in and after an administrator changes visibility, so users must manually repeat the workflow to notice updates. An optional, user-controlled polling mode will keep the authenticated table current while preserving the existing request and authorization model.

## What Changes

- Add a seconds-interval input to the authenticated Users.Web page with a default value of `1` second.
- Add an unchecked polling toggle that is enabled by default alongside the interval input.
- When polling is enabled, disable the interval input and refresh the entity table by calling `GET /entities` at the selected interval until polling is unchecked.
- Stop scheduled polling when the toggle is unchecked or the user signs out, and preserve the existing inline error handling and role-specific table behavior.
- Keep the feature client-side and make no changes to Users API routes, response contracts, authentication, authorization, or persistence.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `users-api`: Extend the browser UI requirement with an optional polling control and timed authenticated entity refreshes.

## Impact

- Affected frontend code under `src/Users/Users.Web`, primarily `Pages/Home.razor` and its component lifecycle/timer handling.
- Focused UI tests or browser verification will cover default control state, interval behavior, refresh calls, disabling the input while active, and stopping polling.
- No API, authentication, persistence, deployment, or external dependency changes are expected.
