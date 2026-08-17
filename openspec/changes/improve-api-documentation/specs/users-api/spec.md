## ADDED Requirements

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
