## Context

Users.Web is a small Blazor WebAssembly application whose shared layout currently renders only the page body and whose stylesheet is largely framework-generated defaults. See `proposal.md - Why` for the motivation. The design must preserve the existing Razor event handlers, same-origin HTTP calls, Basic authentication flow, and role-dependent markup.

## Goals / Non-Goals

**Goals:**

- Establish one visual language through CSS custom properties for color, spacing, typography, borders, focus states, and surfaces.
- Give the shared layout a responsive application frame and provide stable styling hooks for the page regions and table.
- Make form controls, buttons, statuses, errors, and administrator actions distinguishable and keyboard-accessible.
- Keep the table readable on narrow viewports without changing the data shape or removing information.
- Keep the implementation local to Users.Web and usable without adding a UI framework dependency.

**Non-Goals:**

- Changing API routes, response contracts, authentication, authorization, or persistence behavior.
- Adding client-side state management, routing, data formatting rules, or new user actions.
- Introducing a component library, design-system package, or external font/network dependency.
- Reworking the application into a new page structure.

## Decisions

- **Use the existing `app.css` as the styling boundary.** The stylesheet already loads for the app and avoids adding build configuration or package dependencies. Small semantic classes and layout attributes may be added to `MainLayout.razor` and `Home.razor` only where selectors cannot reliably target the existing structure.
- **Use CSS custom properties and a restrained light palette.** Tokens keep the visual system coherent and make future tuning local. A light neutral canvas with a contrasting accent and explicit success/error colors gives the data table and authentication states clear hierarchy without coupling behavior to presentation.
- **Use a centered, full-height shell with a readable content measure.** The shell should provide consistent page padding and a distinct content surface while allowing the table region to scroll horizontally on small screens. This preserves every existing column rather than hiding payload or administrator controls.
- **Style controls with native semantics and visible focus.** Existing `button`, `input`, `form`, and table elements remain the interaction primitives. CSS will improve affordance, disabled states, hover states, and keyboard focus without replacing them with non-semantic elements.
- **Keep state styling class-based.** Existing `.error`, loading text, and Blazor error-boundary selectors will receive visual treatment; no new state machine or markup behavior is required.
- **Verify through the existing app and focused visual checks.** The implementation should be checked with the Users.Web build and manual or browser checks at desktop and mobile widths for signed-out, signed-in regular-user, administrator, loading, and error states.

## Risks / Trade-offs

- [Risk] The payload column can contain long JSON and force a wide table. -> Mitigation: keep the table inside a horizontal overflow region, use stable minimum widths, and allow payload text to wrap or scroll without affecting other controls.
- [Risk] Styling selectors may accidentally affect generated Blazor or Scalar content. -> Mitigation: scope application rules beneath the Users.Web shell/page classes and leave global framework selectors limited to existing error/loading behavior.
- [Risk] A visual-only change can regress keyboard or contrast accessibility. -> Mitigation: preserve native controls, retain visible focus indicators, use semantic table/form markup, and inspect contrast and narrow viewport layout during verification.
- [Risk] The current page uses a Unicode dash and loading ellipsis in visible text. -> Mitigation: do not alter copy as part of styling; limit edits to classes and layout hooks.

## Migration Plan

1. Add the shared shell and page styling hooks, then update `wwwroot/css/app.css` with the tokenized responsive styles.
2. Build the Users.Web project and run the existing relevant test suite.
3. Inspect the application at desktop and mobile viewport sizes across the listed UI states.
4. Roll back by removing the styling hooks and stylesheet additions; no data migration, API migration, or deployment sequencing change is required.

## Open Questions

None. The remaining visual choices can be resolved during implementation without changing the scope or behavioral contract.
