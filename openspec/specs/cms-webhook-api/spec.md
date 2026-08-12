# CMS Webhook API Specification

> Source files: src/CmsWebhook/CmsWebhook.Api/Program.cs, src/CmsWebhook/CmsWebhook.Api/appsettings.json, src/CmsWebhook/CmsWebhook.Api/CmsWebhook.Api.csproj, src/CmsWebhook/CmsWebhook.Api/Properties/launchSettings.json, tests/CmsWebhook/CmsWebhook.Api.Tests/CmsWebhookApiAuthTests.cs, tests/CmsWebhook/CmsWebhook.Api.Tests/CmsWebhookApiFactory.cs, tests/CmsWebhook/CmsWebhook.Api.Tests/InMemoryUserCredentialsProvider.cs

## Purpose

The CMS Webhook API's authentication policy: every endpoint requires Basic authentication, only the reserved cms user is authorized, and the cms username comes from configuration. The API consumes the shared authentication capability (`auth` domain) for the mechanism and credential store.

## Requirements

### Requirement: All endpoints require authentication

Every HTTP endpoint of the CMS Webhook API SHALL require HTTP Basic Authentication. Requests that omit the `Authorization` header, use an unsupported scheme, or present credentials that do not match a known user SHALL be rejected with `401 Unauthorized` and SHALL NOT execute the endpoint handler.

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
