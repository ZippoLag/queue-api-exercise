# CMS Webhook API Specification

> Source files: src/CmsWebhook/CmsWebhook.Api/Program.cs, src/CmsWebhook/CmsWebhook.Api/appsettings.json, src/CmsWebhook/CmsWebhook.Api/CmsWebhook.Api.csproj, src/CmsWebhook/CmsWebhook.Api/Properties/AssemblyInfo.cs, src/CmsWebhook/CmsWebhook.Api/Properties/launchSettings.json, tests/CmsWebhook/CmsWebhook.Api.Tests/CmsWebhookApiAuthTests.cs, tests/CmsWebhook/CmsWebhook.Api.Tests/CmsWebhookApiFactory.cs, tests/CmsWebhook/CmsWebhook.Api.Tests/InMemoryUserCredentialsProvider.cs, tests/CmsWebhook/CmsWebhook.Api.Tests/ProgramConfigurationTests.cs

## Purpose

The CMS Webhook API's authentication policy: every endpoint requires Basic authentication, only the reserved cms user is authorized, and the cms username comes from configuration. The API consumes the shared authentication capability (`auth` domain) for the mechanism and credential store.

## Requirements

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

### Requirement: Only the cms user is authorized

The `cms-webhook` username is the only user authorized to access the CMS Webhook API. Requests authenticated with valid credentials of any other user SHALL be rejected with `403 Forbidden` and SHALL NOT execute the endpoint handler.

#### Scenario: Valid credentials for a non-cms user

- **WHEN** a client sends Basic credentials that are valid for a user other than `cms-webhook`
- **THEN** the API responds with `403 Forbidden` and the endpoint handler is not executed

#### Scenario: Valid credentials for the cms user

- **WHEN** a client sends Basic credentials that match the configured `cms-webhook` user
- **THEN** the API executes the requested endpoint and responds with its normal success status

### Requirement: Credentials are sourced from the credential store

The `cms-webhook` user's credentials SHALL be read from the database-backed credential store provisioned by the initialization script. The application SHALL fail to start when the configured credential store is unreachable or has not been initialized with the cms user.

#### Scenario: Credential store is initialized

- **WHEN** the credential store has been initialized and contains the configured cms user
- **THEN** the application starts and accepts requests authenticated with that user's credentials

#### Scenario: Credential store is not initialized

- **WHEN** the configured credential store does not contain the cms user
- **THEN** the application fails to start with a descriptive error

#### Scenario: Credential store is unreachable

- **WHEN** the configured credential store cannot be reached
- **THEN** the application fails to start with a descriptive error

### Requirement: Configured credential format

The configured cms username SHALL be between 10 and 20 characters in length. A configuration that violates the username length rule SHALL cause the application to fail to start. The password is not a startup configuration value: it is provisioned into the credential store by the initialization script and verified against its stored hash.

The cms username SHALL be read exclusively from the `Auth:CmsUsername` configuration value (default `cms-webhook`). The legacy `AUTH_CMS_*` environment variables SHALL NOT be consulted: they have no effect on the authorized username.

#### Scenario: Valid configured username length

- **WHEN** the configured cms username is between 10 and 20 characters long
- **THEN** the application starts normally

#### Scenario: Invalid configured username length

- **WHEN** the configured cms username is shorter than 10 or longer than 20 characters
- **THEN** the application fails to start with a descriptive error

#### Scenario: Legacy environment variables are ignored

- **WHEN** a legacy `AUTH_CMS_*` environment variable is set but `Auth:CmsUsername` is not overridden
- **THEN** the application uses the configured default username and the environment variable has no effect

### Requirement: Healthcheck endpoint

The CMS Webhook API SHALL expose an anonymous healthcheck endpoint at `/health` that reports the application's liveness. The endpoint SHALL respond with `200 OK` and a JSON body when the application is healthy, and `503 Service Unavailable` when it is unhealthy. The endpoint SHALL be reachable without authentication so load balancers and orchestrators can probe the API.

#### Scenario: Anonymous liveness probe

- **WHEN** a client sends `GET /health` without an `Authorization` header while the application is running
- **THEN** the API responds with `200 OK` and a JSON body reporting a healthy status

### Requirement: OpenAPI document

The CMS Webhook API SHALL expose an OpenAPI document describing its endpoints, generated from the endpoint code, at `/openapi/v1.json`. The document SHALL be reachable without authentication, SHALL stay in sync with the implemented endpoints — including their actual status codes, request and response schemas, and authentication requirements — and SHALL describe the accepted ingestion request shape. The API SHALL also serve a browsable API reference UI, generated from the same document, at `/scalar/v1`, in every environment, reachable without authentication.

#### Scenario: Contract served anonymously

- **WHEN** a client requests `GET /openapi/v1.json` without an `Authorization` header
- **THEN** the API responds with `200 OK` and an OpenAPI document describing the API's endpoints

#### Scenario: Contract describes the endpoints

- **WHEN** a client reads the served OpenAPI document
- **THEN** the document contains the `/cms/events` and `/health` endpoints with their HTTP methods

#### Scenario: Contract matches the implemented status codes

- **WHEN** a client reads the served OpenAPI document
- **THEN** each operation's documented responses match the status codes the endpoint actually returns (`201 Created` for accepted ingestion, `400 Bad Request` for invalid bodies, `401 Unauthorized` for missing or invalid credentials)

#### Scenario: Contract documents the ingestion request shape

- **WHEN** a client reads the served OpenAPI document
- **THEN** the `POST /cms/events` operation declares a request body schema covering both accepted forms — a single event object and a batch array of event objects — with per-field descriptions for `type` (one of `publish`, `update`, `unPublish`, `delete`, case-sensitive), `id`, `payload`, `version`, and `timestamp`

#### Scenario: Contract declares the authentication scheme

- **WHEN** a client reads the served OpenAPI document
- **THEN** the protected operations declare an HTTP Basic security scheme with a request-level security requirement

#### Scenario: API reference UI served in all environments

- **WHEN** a client requests `GET /scalar/v1` without an `Authorization` header in any environment, including Staging and Production
- **THEN** the API responds with `200 OK` and the browsable API reference UI
