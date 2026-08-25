## MODIFIED Requirements

### Requirement: Users API hosts a browser UI

The Users API SHALL serve a browser-based UI at its origin root (`/`), from the same origin as its own endpoints, so the application shell and its assets load anonymously and all API calls from the UI are same-origin. A fallback SHALL serve the application shell for client-side routes. The UI SHALL let a user sign in with the same credentials the API authenticates against, and SHALL derive the role from the authenticated username exactly as the API does: the administrator SHALL see the full entity table (id, visibility flag, version, update time, payload) with an enable/disable toggle per row that invokes the existing administrator-only enable/disable endpoints; a regular user SHALL see the same table without the toggle column. The UI SHALL persist the sign-in session in browser session storage so a page refresh keeps the user signed in: after a refresh, the UI SHALL restore the stored credentials, silently re-authenticate, and show the authenticated entity table without displaying the sign-in form. The UI SHALL remove the stored session when the user signs out. When a user is signed in, the UI SHALL show a seconds-interval input defaulting to `1` and an unchecked polling toggle; both controls SHALL be enabled by default. When polling is checked, the UI SHALL disable the interval input and SHALL refresh the entity table by calling the authenticated `GET /entities` endpoint once per configured interval second until polling is unchecked, and SHALL visibly update the rendered table on every scheduled refresh without requiring user interaction. The UI SHALL stop polling when the toggle is unchecked or the user signs out. The UI SHALL NOT offer any capability beyond what the existing API endpoints allow, and serving the UI SHALL NOT change any existing endpoint, authentication policy, or API contract.

#### Scenario: UI shell loads anonymously

- **WHEN** a client requests the origin root without credentials
- **THEN** the API responds with the browser application shell

#### Scenario: Client-side routes fall back to the shell

- **WHEN** a client requests a client-side route path without credentials
- **THEN** the API responds with the browser application shell

#### Scenario: Sign-in with Users API credentials

- **WHEN** a user signs in with valid credentials of a seeded user
- **THEN** the UI shows the entity table for that user's role

#### Scenario: Administrator sees the toggle

- **WHEN** the administrator signs in and the store contains published entities
- **THEN** each table row shows the full entity data including the visibility flag and an enable/disable toggle

#### Scenario: Administrator toggles visibility

- **WHEN** the administrator presses the toggle on an entity
- **THEN** the UI invokes the existing enable/disable endpoint for that entity and the table reflects the new visibility state

#### Scenario: Regular user has no toggle

- **WHEN** a regular user signs in and the store contains published entities
- **THEN** each table row shows the full entity data without the toggle column

#### Scenario: Reserved cms user is rejected

- **WHEN** a user signs in with the reserved `cms-webhook` credentials
- **THEN** the UI surfaces a descriptive error and shows no entity data

#### Scenario: Refresh preserves the sign-in session

- **WHEN** a signed-in user refreshes the page
- **THEN** the UI restores the stored session, silently re-authenticates, and shows the entity table directly without displaying the sign-in form

#### Scenario: Signing out clears the stored session

- **WHEN** a signed-in user signs out
- **THEN** the UI removes the stored session and a subsequent page refresh shows the sign-in form

#### Scenario: Polling controls default to inactive and editable

- **WHEN** an authenticated user views the entity table after signing in
- **THEN** the interval input contains `1`, the polling toggle is unchecked, and both controls are enabled

#### Scenario: Enabling polling refreshes at the configured interval

- **WHEN** an authenticated user enters a valid interval and checks the polling toggle
- **THEN** the interval input becomes disabled, the UI calls authenticated `GET /entities` once per interval until polling is unchecked, and the rendered table updates on each scheduled refresh without user interaction

#### Scenario: Unchecking polling stops refreshes

- **WHEN** an authenticated user unchecks the polling toggle
- **THEN** the UI stops making scheduled `GET /entities` calls and re-enables the interval input

#### Scenario: Signing out stops polling

- **WHEN** an authenticated user signs out while polling is enabled
- **THEN** the UI stops scheduled polling, clears the authenticated table, and shows the sign-in form

#### Scenario: Invalid polling interval is not scheduled

- **WHEN** an authenticated user provides an empty, non-numeric, or non-positive interval and enables polling
- **THEN** the UI does not schedule polling and keeps the interval control available for correction

#### Scenario: Existing API behavior is unchanged

- **WHEN** the UI is served alongside the API
- **THEN** every existing endpoint, authentication policy, and the served OpenAPI contract behave exactly as before
