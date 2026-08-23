## 1. Polling Controls

- [ ] 1.1 Add the authenticated-page interval input with a default value of `1`, an unchecked polling toggle, and enabled-by-default controls; verify the rendered signed-in state matches the `users-api` polling-control scenarios.
- [ ] 1.2 Add positive-seconds validation and bind the interval/toggle state so an invalid interval does not start polling and the interval input is disabled only while polling is active; verify empty, non-numeric, zero, and negative values remain correctable.

## 2. Refresh Lifecycle

- [ ] 2.1 Implement one component-owned cancellation-aware polling loop that waits the configured number of seconds, calls the existing authenticated entity loader, and prevents overlapping requests; verify repeated `GET /entities` calls occur at the configured cadence.
- [ ] 2.2 Cancel and dispose the polling loop when the toggle is unchecked, the user signs out, or the component is disposed, then restore the input state or sign-in view; verify no scheduled calls occur after each stop condition.
- [ ] 2.3 Preserve existing administrator and regular-user table behavior and inline HTTP error handling during polling; verify role-dependent controls and unauthorized/error responses remain unchanged.

## 3. Verification

- [ ] 3.1 Add or update focused Users.Web/component/browser tests for defaults, activation, input disabling, interval refreshes, invalid intervals, unchecking, sign-out, disposal, and slow-request non-overlap; verify the focused tests pass.
- [ ] 3.2 Build the Users.Web project and run the relevant solution tests; verify no API contract or authentication regressions are introduced.
