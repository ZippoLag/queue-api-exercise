## Why

Users.Web currently exposes the sign-in form and entity table with browser defaults, making the application difficult to scan and providing little visual distinction between interactive, loading, and error states. A consistent baseline across the project will make the existing workflows clearer on desktop and small screens without changing their behavior.

## What Changes

- Add a shared visual shell for the Users.Web application, including page spacing, typography, color tokens, surface treatment, and responsive layout rules.
- Style the sign-in form, authenticated header, sign-out action, entity table, administrator visibility controls, loading state, inline errors, and Blazor error boundary consistently.
- Make the entity table usable on narrow screens through responsive overflow and stable control sizing while retaining all current columns and actions.
- Preserve the existing routes, API calls, Basic authentication flow, role-based visibility, text content, and endpoint contracts.

## Capabilities

### New Capabilities

None. This is a presentation-only change and does not introduce a new system capability.

### Modified Capabilities

None. The existing Users.Web behavior remains unchanged; no spec-level requirement changes are needed.

## Impact

- Affected frontend files under `src/Users/Users.Web`, primarily the shared stylesheet and the Razor layout/page markup needed for styling hooks.
- No API, authentication, persistence, deployment, or external dependency changes are expected.
- Visual verification is required at desktop and mobile viewport sizes, including signed-out, signed-in, loading, error, and administrator states.
