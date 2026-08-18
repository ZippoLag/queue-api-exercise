## Purpose

Protects the CMS Webhook API's ingestion endpoint from traffic floods by bounding how many requests it accepts per time window, keeping the shared store responsive under load.

## ADDED Requirements

### Requirement: Ingestion endpoint is rate limited

The CMS Webhook API SHALL rate limit requests to `POST /cms/events` with a fixed-window policy whose request limit and window are configuration values. Requests within the limit SHALL be processed normally; requests that exceed the limit SHALL be rejected with `429 Too Many Requests` and SHALL NOT execute the endpoint handler. The anonymous discovery endpoints (`GET /health`, `GET /openapi/v1.json`, `GET /scalar/v1`) SHALL NOT be subject to the rate limit. The rate limit SHALL be enforced before authentication is evaluated, so a flood of unauthenticated requests is rejected without reaching the credential store.

#### Scenario: Request within the rate limit succeeds

- **WHEN** a client sends a number of requests to `POST /cms/events` within the configured per-window limit, each with valid credentials
- **THEN** every request is processed and responds with its normal status code

#### Scenario: Excess requests are rejected with 429

- **WHEN** a client sends more requests to `POST /cms/events` than the configured per-window limit
- **THEN** the requests beyond the limit respond with `429 Too Many Requests` and the endpoint handler is not executed

#### Scenario: Discovery endpoints are not rate limited

- **WHEN** a client sends more requests to `GET /health`, `GET /openapi/v1.json`, or `GET /scalar/v1` than the ingestion rate limit, without credentials
- **THEN** the requests are not rate limited and respond with their normal status codes

#### Scenario: Unauthenticated flood is rate limited

- **WHEN** a client sends more requests to `POST /cms/events` than the configured per-window limit, without valid credentials
- **THEN** the requests beyond the limit respond with `429 Too Many Requests` rather than `401 Unauthorized`
