## 1. Establish Shared Application Styling

- [ ] 1.1 Add semantic shell and page-region styling hooks in `MainLayout.razor` and `Home.razor` without changing routes, event handlers, displayed data, or API calls; verify the existing sign-in and entity workflows remain represented in the markup.
- [ ] 1.2 Replace the default Users.Web presentation rules in `wwwroot/css/app.css` with scoped design tokens and shared styles for the application frame, surfaces, typography, spacing, links, focus indicators, and responsive containers; verify the stylesheet builds without syntax errors.

## 2. Style User Workflows and States

- [ ] 2.1 Style the sign-in form and authenticated header, including labels, inputs, submit/sign-out controls, disabled states, and inline errors; verify keyboard focus is visible and the existing sign-in/sign-out interactions are unchanged.
- [ ] 2.2 Style the entity table, administrator visibility actions, loading state, error state, and Blazor error boundary while preserving role-based columns and all current text/data; verify regular users still have no visibility toggle and administrators retain the toggle.
- [ ] 2.3 Add narrow-viewport table handling and stable control sizing so long payloads remain inspectable without overlapping controls; verify the table remains usable at a mobile viewport and readable at a desktop viewport.

## 3. Validate the Frontend Change

- [ ] 3.1 Build `src/Users/Users.Web/Users.Web.csproj` and verify the project compiles successfully with no new warnings attributable to the styling change.
- [ ] 3.2 Run the relevant existing Users.Web/API tests and verify authentication, role-based entity visibility, toggle behavior, and endpoint contracts remain green.
- [ ] 3.3 Perform a browser visual check of signed-out, signed-in regular-user, administrator, loading, and error states at desktop and mobile widths; verify no overflow, overlap, or loss of accessible focus treatment.
