## MODIFIED Requirements

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
