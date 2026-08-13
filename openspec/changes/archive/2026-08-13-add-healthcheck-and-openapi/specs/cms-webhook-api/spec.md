## MODIFIED Requirements

### Requirement: All endpoints require authentication

Every HTTP endpoint of the CMS Webhook API SHALL require HTTP Basic Authentication, except the healthcheck and OpenAPI document endpoints, which SHALL be anonymous (they are meant to be probed and discovered without credentials). Requests to protected endpoints that omit the `Authorization` header, use an unsupported scheme, or present credentials that do not match a known user SHALL be rejected with `401 Unauthorized` and SHALL NOT execute the endpoint handler.

#### Scenario: Request without Authorization header

- **WHEN** a client sends a request without an `Authorization` header
- **THEN** the API responds with `401 Unauthorized` and the endpoint handler is not executed

#### Scenario: Request with an unsupported authorization scheme

- **WHEN** a client sends an `Authorization` header that does not use the `Basic` scheme
- **THEN** the API responds with `401 Unauthorized` and the endpoint handler is not executed

#### Scenario: Request with malformed Basic credentials

- **WHEN** a client sends a `Basic` `Authorization` header whose value cannot be decoded into a `username:password` pair
- **THEN** the API responds with `401 Unauthorized` and the endpoint handler is not executed

#### Scenario: Request with credentials of an unknown user

- **WHEN** a client sends Basic credentials whose username matches no configured user
- **THEN** the API responds with `401 Unauthorized` and the endpoint handler is not executed

#### Scenario: Request with a wrong password for a known user

- **WHEN** a client sends Basic credentials whose username matches a configured user but whose password does not match
- **THEN** the API responds with `401 Unauthorized` and the endpoint handler is not executed

#### Scenario: Anonymous healthcheck and OpenAPI endpoints

- **WHEN** a client requests the healthcheck or OpenAPI document endpoint without credentials
- **THEN** the request succeeds without authentication and without executing any protected endpoint handler

## ADDED Requirements

### Requirement: Healthcheck endpoint

The CMS Webhook API SHALL expose an anonymous healthcheck endpoint at `/health` that reports the application's liveness. The endpoint SHALL respond with `200 OK` and a JSON body when the application is healthy, and `503 Service Unavailable` when it is unhealthy. The endpoint SHALL be reachable without authentication so load balancers and orchestrators can probe the API.

#### Scenario: Anonymous liveness probe

- **WHEN** a client sends `GET /health` without an `Authorization` header while the application is running
- **THEN** the API responds with `200 OK` and a JSON body reporting a healthy status

### Requirement: OpenAPI document

The CMS Webhook API SHALL expose an OpenAPI document describing its endpoints, generated from the endpoint code, at `/openapi/v1.json`. The document SHALL be reachable without authentication and SHALL stay in sync with the implemented endpoints.

#### Scenario: Contract served anonymously

- **WHEN** a client requests `GET /openapi/v1.json` without an `Authorization` header
- **THEN** the API responds with `200 OK` and an OpenAPI document describing the API's endpoints

#### Scenario: Contract describes the endpoints

- **WHEN** a client reads the served OpenAPI document
- **THEN** the document contains the `/cms/events` and `/health` endpoints with their HTTP methods
