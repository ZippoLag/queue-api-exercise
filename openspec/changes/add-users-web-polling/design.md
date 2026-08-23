## Context

The Users.Web page already stores the signed-in credentials in component state, loads `GET /entities`, and refreshes after administrator visibility changes. See `proposal.md - Why` and `specs/users-api/spec.md` for the requested behavior and contract. The implementation must remain local to the Blazor page, preserve same-origin Basic authentication, and avoid changing the Users API.

## Goals / Non-Goals

**Goals:**

- Expose an interval input and polling toggle only in the authenticated page state.
- Keep the initial interval at one second, polling off, and both controls editable.
- Start and stop one authenticated refresh loop in response to the toggle and sign-out lifecycle.
- Prevent invalid or concurrent polling requests from creating uncontrolled work.
- Preserve the existing table refresh and inline error behavior.

**Non-Goals:**

- Changing API endpoints, authentication, response models, or persistence.
- Polling while signed out or adding browser storage for credentials or preferences.
- Adding a timer package or application-wide state-management abstraction.
- Changing the administrator visibility action semantics.

## Decisions

- **Use a component-owned cancellable async loop.** A `CancellationTokenSource` plus `PeriodicTimer` keeps the schedule tied to the page component and makes unchecking/sign-out deterministic. A `System.Threading.Timer` would require extra synchronization around async callbacks, while browser JavaScript timers would add an interop dependency for behavior that belongs to the component.
- **Keep the interval as a seconds value in component state.** Bind a numeric input to the displayed setting, default it to `1`, and validate that the parsed value is positive before starting the loop. Invalid input leaves polling stopped and the input enabled so the user can correct it.
- **Disable the interval while active.** The toggle is the only control that can stop an active loop; disabling the interval prevents the schedule from changing underneath an in-flight timer and matches the user-visible contract.
- **Reuse `LoadEntitiesAsync` for every poll.** Polling uses the existing request construction and Basic header path so authorization, response parsing, and existing status/error handling remain consistent. The loop must avoid starting a second request if the prior refresh is still running.
- **Cancel before replacing or clearing component state.** Unchecking and sign-out cancel the current source, dispose it, and await the loop when needed. Component disposal also cancels the source so no delayed callback can update a detached page.
- **Keep polling failures inline.** A non-success response follows the existing `_error` and `_entities` handling; polling continues only while enabled, allowing a later response to recover the table without changing API semantics.

## Risks / Trade-offs

- [Risk] A one-second interval can create frequent requests. -> Mitigation: polling is opt-in, the default toggle is off, and the interval is validated before scheduling.
- [Risk] Component disposal or rapid toggle changes could leave an asynchronous loop alive. -> Mitigation: centralize cancellation/disposal and use a cancellation-aware loop with one owner for the active source.
- [Risk] A slow request can overlap the next interval. -> Mitigation: await each refresh before the loop waits for its next tick, ensuring at most one polling request is active.
- [Risk] Invalid values may be rejected by browser numeric-input binding before component validation runs. -> Mitigation: retain the input's editable state and treat any parse/validation failure as stopped polling; verify empty, non-numeric, zero, and negative cases.

## Migration Plan

1. Add the authenticated polling controls and component lifecycle cancellation logic to `Pages/Home.razor`.
2. Add focused component or browser coverage for default state, timer cadence, stop behavior, invalid input, and sign-out cleanup.
3. Build Users.Web and run the relevant test suite.
4. Roll back by removing the polling controls and loop state; no API, database, or deployment migration is required.

## Open Questions

None. The interval unit, default, activation state, and stop conditions are defined by the requirement delta.
