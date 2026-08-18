## ADDED Requirements

### Requirement: Users API hosts a browser UI

The Users API SHALL serve a browser-based UI at its origin root (`/`), from the same origin as its own endpoints, so the application shell and its assets load anonymously and all API calls from the UI are same-origin. A fallback SHALL serve the application shell for client-side routes. The UI SHALL let a user sign in with the same credentials the API authenticates against, and SHALL derive the role from the authenticated username exactly as the API does: the administrator SHALL see the full entity table (id, visibility flag, version, update time, payload) with an enable/disable toggle per row that invokes the existing administrator-only enable/disable endpoints; a regular user SHALL see the same table without the toggle column. The UI SHALL NOT offer any capability beyond what the existing API endpoints allow, and serving the UI SHALL NOT change any existing endpoint, authentication policy, or API contract.

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

#### Scenario: Existing API behavior is unchanged

- **WHEN** the UI is served alongside the API
- **THEN** every existing endpoint, authentication policy, and the served OpenAPI contract behave exactly as before

## MODIFIED Requirements

None — all existing Users API requirements are unchanged.
