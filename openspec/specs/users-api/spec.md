# Users API Specification

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

The Users API SHALL expose `POST /entities/{id}/disable` and `POST /entities/{id}/enable`, accepted only from the administrator, that flip the entity's administrator-visibility flag off and on respectively. Disabled entities SHALL be hidden from regular users' listings; enabling SHALL restore them. Both endpoints SHALL accept no request body, SHALL return an empty success response, and SHALL be idempotent — toggling an already-disabled or already-enabled entity SHALL still succeed. An unknown entity id SHALL yield `404 Not Found`.

#### Scenario: Administrator disables an entity

- **WHEN** the administrator requests `POST /entities/{id}/disable` for an existing entity
- **THEN** the API responds with an empty success response and the entity is hidden from regular users' listings

#### Scenario: Administrator enables a disabled entity

- **WHEN** the administrator requests `POST /entities/{id}/enable` for a disabled entity
- **THEN** the API responds with an empty success response and the entity is visible to regular users again

#### Scenario: Disabling is idempotent

- **WHEN** the administrator requests `POST /entities/{id}/disable` for an already-disabled entity
- **THEN** the API responds with an empty success response

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

The Users API SHALL expose an OpenAPI document describing its endpoints, generated from the endpoint code, at `/openapi/v1.json`. The document SHALL be reachable without authentication and SHALL stay in sync with the implemented endpoints — including their actual status codes, response schemas, and authentication requirements. The API SHALL also serve a browsable API reference UI, generated from the same document, at `/scalar/v1`, in every environment, reachable without authentication.

#### Scenario: Contract served anonymously

- **WHEN** a client requests `GET /openapi/v1.json` without an `Authorization` header
- **THEN** the API responds with `200 OK` and an OpenAPI document describing the API's endpoints

#### Scenario: Contract describes the entities endpoints accurately

- **WHEN** a client reads the served OpenAPI document
- **THEN** the `/entities` and `/entities/{id}/disable|enable` operations declare their actual status codes (`200 OK` for the listing, `204 No Content` for disable/enable, `401 Unauthorized` and `403 Forbidden` for authentication/authorization failures, `404 Not Found` for an unknown entity id) and the listing declares its response schema

#### Scenario: Contract declares the authentication scheme

- **WHEN** a client reads the served OpenAPI document
- **THEN** the protected operations declare an HTTP Basic security scheme with a request-level security requirement

#### Scenario: API reference UI served in all environments

- **WHEN** a client requests `GET /scalar/v1` without an `Authorization` header in any environment, including Staging and Production
- **THEN** the API responds with `200 OK` and the browsable API reference UI
