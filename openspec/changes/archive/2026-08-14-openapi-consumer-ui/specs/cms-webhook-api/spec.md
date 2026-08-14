## MODIFIED Requirements

### Requirement: OpenAPI document

The CMS Webhook API SHALL expose an OpenAPI document describing its endpoints, generated from the endpoint code, at `/openapi/v1.json`. The document SHALL be reachable without authentication and SHALL stay in sync with the implemented endpoints. The API SHALL also serve a browsable API reference UI, generated from the same document, at `/scalar/v1`, in every environment, reachable without authentication.

#### Scenario: Contract served anonymously

- **WHEN** a client requests `GET /openapi/v1.json` without an `Authorization` header
- **THEN** the API responds with `200 OK` and an OpenAPI document describing the API's endpoints

#### Scenario: Contract describes the endpoints

- **WHEN** a client reads the served OpenAPI document
- **THEN** the document contains the `/cms/events` and `/health` endpoints with their HTTP methods

#### Scenario: API reference UI served in all environments

- **WHEN** a client requests `GET /scalar/v1` without an `Authorization` header in any environment, including Staging and Production
- **THEN** the API responds with `200 OK` and the browsable API reference UI
