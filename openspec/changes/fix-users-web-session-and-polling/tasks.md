## 1. Session Persistence

- [ ] 1.1 Add the `sessionStorage`-backed session store service under `src/Users/Users.Web` (XML-documented what/why) and register it in `Program.cs`; verify the project builds and the storage key plus JSON shape match design.md.
- [ ] 1.2 Persist the session only after a successful sign-in load in `Pages/Home.razor`; verify a valid sign-in writes the `sessionStorage` entry and a failed sign-in (invalid credentials or reserved `cms-webhook`) writes nothing.
- [ ] 1.3 Restore the stored session in `OnInitializedAsync`, silently re-authenticating via the existing `LoadEntitiesAsync` and falling back to the sign-in form with the inline error when the stored session is rejected; verify a page refresh keeps the signed-in entity view and an invalidated stored session returns to the form.
- [ ] 1.4 Clear the stored session on sign-out; verify sign-out removes the storage entry and a subsequent refresh shows the sign-in form (spec scenario "Signing out clears the stored session").

## 2. Polling Re-render

- [ ] 2.1 Re-render the component after each scheduled refresh by calling `InvokeAsync(StateHasChanged)` inside the polling loop next to `LoadEntitiesAsync`; verify in the browser that with polling checked the table and inline errors visibly update on each interval without any user interaction, and that unchecking still stops the refreshes (spec scenario "Enabling polling refreshes at the configured interval").

## 3. Verification and Docs

- [ ] 3.1 Build the Users.Web project and run the relevant solution tests, including the E2E smoke suite; verify no API contract or authentication regressions are introduced.
- [ ] 3.2 Update the `users-api` spec and linked docs (`docs/architecture.md` and the README docs index if the UI behavior description changes) to describe the persistent session and visible polling refresh; verify the spec delta is synced and docs stay consistent with the implemented behavior.
