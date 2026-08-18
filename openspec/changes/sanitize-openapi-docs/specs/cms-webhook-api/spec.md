## MODIFIED Requirements

### Requirement: OpenAPI document

The CMS Webhook API SHALL expose an OpenAPI document describing its endpoints, generated from the endpoint code, at `/openapi/v1.json`. The document SHALL be reachable without authentication, SHALL stay in sync with the implemented endpoints — including their actual status codes, request and response schemas, and authentication requirements — and SHALL describe the accepted ingestion request shape. The document SHALL describe operations in consumer-facing terms: it SHALL NOT disclose internal implementation details such as the outbox architecture or the shared credential store, and it SHALL document the actual failure modes of each operation in generic wording — including the `403 Forbidden` returned for valid credentials of a user not authorized on this API. The API SHALL also serve a browsable API reference UI, generated from the same document, at `/scalar/v1`, in every environment, reachable without authentication.

#### Scenario: Contract served anonymously

- **WHEN** a client requests `GET /openapi/v1.json` without an `Authorization` header
- **THEN** the API responds with `200 OK` and an OpenAPI document describing the API's endpoints

#### Scenario: Contract describes the endpoints

- **WHEN** a client reads the served OpenAPI document
- **THEN** the document contains the `/cms/events` and `/health` endpoints with their HTTP methods

#### Scenario: Contract matches the implemented status codes

- **WHEN** a client reads the served OpenAPI document
- **THEN** each operation's documented responses match the status codes the endpoint actually returns (`201 Created` for accepted ingestion, `400 Bad Request` for invalid bodies, `401 Unauthorized` for missing or invalid credentials, `403 Forbidden` for valid credentials of a user not authorized on this API, `429 Too Many Requests` when the ingestion rate limit is exceeded)

#### Scenario: Contract documents the ingestion request shape

- **WHEN** a client reads the served OpenAPI document
- **THEN** the `POST /cms/events` operation declares a request body schema covering both accepted forms — a single event object and a batch array of event objects — with per-field descriptions for `type` (one of `publish`, `update`, `unPublish`, `delete`, case-sensitive), `id`, `payload`, `version`, and `timestamp`

#### Scenario: Contract declares the authentication scheme

- **WHEN** a client reads the served OpenAPI document
- **THEN** the protected operations declare an HTTP Basic security scheme with a request-level security requirement

#### Scenario: Contract does not disclose implementation details

- **WHEN** a client reads the served OpenAPI document
- **THEN** the document does not mention the reserved username `cms-webhook`, the outbox, or the shared credential store, describes accepted events in processing terms (for example, "recorded for processing") rather than naming the internal architecture, and describes the `403 Forbidden` and `429 Too Many Requests` responses in generic consumer-facing wording without naming the rejecting rule or user

#### Scenario: API reference UI served in all environments

- **WHEN** a client requests `GET /scalar/v1` without an `Authorization` header in any environment, including Staging and Production
- **THEN** the API responds with `200 OK` and the browsable API reference UI
