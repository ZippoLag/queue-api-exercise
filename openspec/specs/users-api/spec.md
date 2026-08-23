# Users API Specification

> Source files: src/Users/Users.Web/App.razor, src/Users/Users.Web/Pages/Home.razor, src/Users/Users.Web/Layout/MainLayout.razor, src/Users/Users.Web/Program.cs, src/Users/Users.Web/Properties/AssemblyInfo.cs, src/Users/Users.Web/Properties/launchSettings.json, src/Users/Users.Web/Users.Web.csproj, src/Users/Users.Web/_Imports.razor, src/Users/Users.Web/wwwroot/appsettings.json, src/Users/Users.Web/wwwroot/css/app.css, src/Users/Users.Web/wwwroot/index.html

## Purpose

The Users API serves entity data to authenticated users and lets the administrator control which entities regular users can see, backed by the shared credential store and the CMS entity store. Users are provisioned manually; the API itself exposes no user-management endpoints.

## Requirements

### Requirement: Entities are listed by published status and administrator visibility

The Users API SHALL expose `GET /entities` returning the currently published entities. A regular user SHALL only see published entities that have not been disabled by an administrator; the administrator SHALL see all published entities, including disabled ones. Unpublished entities SHALL NOT be returned to any user.

#### Scenario: Regular user sees only published, enabled entities

- **WHEN** a regular user requests `GET /entities` and the store contains published entities, some of which are disabled by the administrator
- **THEN** the response contains only the published entities that are not disabled

#### Scenario: Administrator sees all published entities

- **WHEN** the administrator requests `GET /entities` and the store contains published entities, including disabled ones
- **THEN** the response contains all published entities, including the disabled ones

#### Scenario: Unpublished entities are never listed

- **WHEN** any user requests `GET /entities` and the store contains unpublished entities
- **THEN** the response does not contain any unpublished entity

#### Scenario: Response items include the entity id and visibility flag

- **WHEN** a user requests `GET /entities`
- **THEN** each returned item includes the entity's id and its administrator-visibility flag alongside the version, update time, and payload

### Requirement: Administrator enables and disables entity visibility

The Users API SHALL expose `POST /entities/{id}/disable` and `POST /entities/{id}/enable`, accepted only from the administrator, that flip the entity's administrator-visibility flag off and on respectively. Disabled entities SHALL be hidden from regular users' listings; enabling SHALL restore them. Both endpoints SHALL accept no request body, SHALL return an empty success response, and SHALL be idempotent — toggling an already-disabled or already-enabled entity SHALL still succeed. The route `id` SHALL be validated and sanitized like the CMS Webhook API's: an empty or whitespace-only `id` SHALL be rejected with `400 Bad Request`, and the `id` SHALL be trimmed before the lookup. An unknown entity id SHALL yield `404 Not Found`.

#### Scenario: Administrator disables an entity

- **WHEN** the administrator requests `POST /entities/{id}/disable` for an existing entity
- **THEN** the API responds with an empty success response and the entity is hidden from regular users' listings

#### Scenario: Administrator enables a disabled entity

- **WHEN** the administrator requests `POST /entities/{id}/enable` for a disabled entity
- **THEN** the API responds with an empty success response and the entity is visible to regular users again

#### Scenario: Disabling is idempotent

- **WHEN** the administrator requests `POST /entities/{id}/disable` for an already-disabled entity
- **THEN** the API responds with an empty success response

#### Scenario: Empty or whitespace-only id

- **WHEN** a client requests the enable or disable endpoint with an empty or whitespace-only `id`
- **THEN** the API responds with `400 Bad Request` and does not modify any entity

#### Scenario: Id is trimmed before lookup

- **WHEN** a client requests the enable or disable endpoint with an `id` carrying surrounding whitespace
- **THEN** the API looks up the entity by the trimmed id and responds with the normal success response when it exists

#### Scenario: Unknown entity id

- **WHEN** a client requests the enable or disable endpoint for an entity id that does not exist in the store
- **THEN** the API responds with `404 Not Found`

### Requirement: Users API authentication and roles

Every endpoint of the Users API SHALL require HTTP Basic authentication except the healthcheck, OpenAPI document, and API reference UI endpoints, which SHALL be anonymous. The `administrator` username SHALL be the only user authorized to call the enable/disable endpoints and to see disabled entities; every other authenticated user SHALL be treated as a regular user. The `cms-webhook` username SHALL NOT be authorized on the Users API, as it is reserved for the CMS Webhook API. Missing or invalid credentials SHALL be rejected with `401 Unauthorized`; valid credentials of a user without the required role SHALL be rejected with `403 Forbidden`.

#### Scenario: Request without credentials

- **WHEN** a client requests a protected Users API endpoint without an `Authorization` header
- **THEN** the API responds with `401 Unauthorized` and the handler is not executed

#### Scenario: Regular user lists entities

- **WHEN** a regular user requests `GET /entities` with valid credentials
- **THEN** the API responds with `200 OK` and the published, non-disabled entities

#### Scenario: Regular user cannot disable an entity

- **WHEN** a regular user requests `POST /entities/{id}/disable` with valid credentials
- **THEN** the API responds with `403 Forbidden` and the handler is not executed

#### Scenario: Administrator performs admin operations

- **WHEN** the administrator requests the enable or disable endpoint with valid credentials
- **THEN** the API executes the handler and responds with its normal success status

#### Scenario: cms-webhook is rejected on the Users API

- **WHEN** a client sends valid credentials for the `cms-webhook` user
- **THEN** the API responds with `403 Forbidden` and the handler is not executed

#### Scenario: Anonymous discovery endpoints

- **WHEN** a client requests the healthcheck, OpenAPI document, or API reference UI endpoints without credentials
- **THEN** the request succeeds without authentication

### Requirement: OpenAPI document

The Users API SHALL expose an OpenAPI document describing its endpoints, generated from the endpoint code, at `/openapi/v1.json`. The document SHALL be reachable without authentication and SHALL stay in sync with the implemented endpoints — including their actual status codes, response schemas, and authentication requirements. The document SHALL describe error responses in generic, role-based terms: it SHALL NOT disclose internal implementation details such as the reserved usernames (`cms-webhook`, `administrator`, `regular-user`), the shared credential store, or the cross-API integration they reveal. The API SHALL also serve a browsable API reference UI, generated from the same document, at `/scalar/v1`, in every environment, reachable without authentication.

#### Scenario: Contract served anonymously

- **WHEN** a client requests `GET /openapi/v1.json` without an `Authorization` header
- **THEN** the API responds with `200 OK` and an OpenAPI document describing the API's endpoints

#### Scenario: Contract describes the entities endpoints accurately

- **WHEN** a client reads the served OpenAPI document
- **THEN** the `/entities` and `/entities/{id}/disable|enable` operations declare their actual status codes (`200 OK` for the listing, `204 No Content` for disable/enable, `400 Bad Request` for an empty or whitespace-only id, `401 Unauthorized` and `403 Forbidden` for authentication/authorization failures, `404 Not Found` for an unknown entity id) and the listing declares its response schema

#### Scenario: Contract declares the authentication scheme

- **WHEN** a client reads the served OpenAPI document
- **THEN** the protected operations declare an HTTP Basic security scheme with a request-level security requirement

#### Scenario: Contract does not disclose implementation details

- **WHEN** a client reads the served OpenAPI document
- **THEN** the document does not contain the reserved machine username `cms-webhook` or the shared credential store, and the `403 Forbidden` responses are described in generic role-based authorization terms (for example, "The caller is authenticated but not authorized on this API." and "The caller is not the administrator.") rather than naming a concrete username or the rejecting rule

#### Scenario: API reference UI served in all environments

- **WHEN** a client requests `GET /scalar/v1` without an `Authorization` header in any environment, including Staging and Production
- **THEN** the API responds with `200 OK` and the browsable API reference UI

### Requirement: Users API hosts a browser UI

The Users API SHALL serve a browser-based UI at its origin root (`/`), from the same origin as its own endpoints, so the application shell and its assets load anonymously and all API calls from the UI are same-origin. A fallback SHALL serve the application shell for client-side routes. The UI SHALL let a user sign in with the same credentials the API authenticates against, and SHALL derive the role from the authenticated username exactly as the API does: the administrator SHALL see the full entity table (id, visibility flag, version, update time, payload) with an enable/disable toggle per row that invokes the existing administrator-only enable/disable endpoints; a regular user SHALL see the same table without the toggle column. When a user is signed in, the UI SHALL show a seconds-interval input defaulting to `1` and an unchecked polling toggle; both controls SHALL be enabled by default. When polling is checked, the UI SHALL disable the interval input and SHALL refresh the entity table by calling the authenticated `GET /entities` endpoint once per configured interval second until polling is unchecked. The UI SHALL stop polling when the toggle is unchecked or the user signs out. The UI SHALL NOT offer any capability beyond what the existing API endpoints allow, and serving the UI SHALL NOT change any existing endpoint, authentication policy, or API contract.

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

#### Scenario: Polling controls default to inactive and editable

- **WHEN** an authenticated user views the entity table after signing in
- **THEN** the interval input contains `1`, the polling toggle is unchecked, and both controls are enabled

#### Scenario: Enabling polling refreshes at the configured interval

- **WHEN** an authenticated user enters a valid interval and checks the polling toggle
- **THEN** the interval input becomes disabled and the UI calls authenticated `GET /entities` once per interval until polling is unchecked

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

### Requirement: Read and write paths use separated configurations

The Users API SHALL serve reads and writes on separated EF configurations. The `GET /entities` listing SHALL run on a read-only configuration: the query SHALL NOT track the returned entities and SHALL NOT mutate the store. The `POST /entities/{id}/disable` and `POST /entities/{id}/enable` commands SHALL run on a single-writer configuration that loads and persists the affected row in place. Listings SHALL return each entity's stored payload in full as a JSON string — payloads are not meant to be edited, so no endpoint SHALL accept payload content and the listing SHALL NOT project payloads away. Because the entity's read representation carries state the originating CMS event did not (a maintained update timestamp and the administrator-visibility flag), the read shape SHALL NOT be an exact match of the recorded event's shape: the recorded event carries only what the CMS sent, while the entity carries the processed state.

#### Scenario: Listing is served without tracking or writes

- **WHEN** a user requests `GET /entities`
- **THEN** the response is served from the read-only configuration: the returned entities are not tracked by EF and the request does not write to the store

#### Scenario: Visibility commands run on the writer configuration

- **WHEN** the administrator requests `POST /entities/{id}/disable` or `POST /entities/{id}/enable`
- **THEN** the affected row is loaded and persisted in place on the single-writer configuration

#### Scenario: Full stored payload is returned

- **WHEN** a user requests `GET /entities` and the store contains published entities with stored payloads
- **THEN** each returned item carries the entity's full stored payload as a JSON string

#### Scenario: Read shape differs from the recorded event shape

- **WHEN** a processed entity is listed
- **THEN** its item includes the maintained update timestamp and the administrator-visibility flag in addition to what the accepted event carried, so the item is not an exact match of the recorded event
