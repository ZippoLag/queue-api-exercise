# Auth Specification

> Source files: src/Shared/QueueApi.Auth/BasicAuthenticationDefaults.cs, src/Shared/QueueApi.Auth/BasicAuthenticationHandler.cs, src/Shared/QueueApi.Auth/BasicAuthenticationOptions.cs, src/Shared/QueueApi.Auth/BasicAuthenticationServiceCollectionExtensions.cs, src/Shared/QueueApi.Auth/IUserCredentialsProvider.cs, src/Shared/QueueApi.Auth/AuthDbContext.cs, src/Shared/QueueApi.Auth/DbUserCredentialsProvider.cs, src/Shared/QueueApi.Auth/Pbkdf2PasswordHasher.cs, src/Shared/QueueApi.Auth/UserCredential.cs, src/Shared/QueueApi.Auth/QueueApi.Auth.csproj, tools/AuthDbInit/AuthDbInitializer.cs, tools/AuthDbInit/Program.cs, tools/AuthDbInit/AuthDbInit.csproj, tools/AuthDbInit.Tests/AuthDbInitializerTests.cs, tools/AuthDbInit.Tests/AuthDbInit.Tests.csproj, scripts/init-db.sh, tests/Shared/QueueApi.Auth.Tests/BasicAuthenticationHandlerTests.cs, tests/Shared/QueueApi.Auth.Tests/DbUserCredentialsProviderTests.cs, tests/Shared/QueueApi.Auth.Tests/Pbkdf2PasswordHasherTests.cs

## Purpose

Shared authentication capability used by the APIs: the Basic authentication mechanism and the database-backed credential store it verifies credentials against. This library is shared infrastructure — the CMS Webhook API consumes it today, and future user-facing APIs will reuse the same mechanism and store.

## Requirements

### Requirement: Authentication rejects invalid credentials

The shared Basic authentication mechanism SHALL reject requests that omit the `Authorization` header, use an unsupported scheme, present malformed Basic credentials, or present credentials that do not match a known user with `401 Unauthorized`, without executing the protected handler. An unknown username and a wrong password SHALL be indistinguishable to the caller.

#### Scenario: Request without Authorization header

- **WHEN** a client sends a request without an `Authorization` header
- **THEN** authentication fails with `401 Unauthorized` and the protected handler is not executed

#### Scenario: Request with an unsupported authorization scheme

- **WHEN** a client sends an `Authorization` header that does not use the `Basic` scheme
- **THEN** authentication fails with `401 Unauthorized` and the protected handler is not executed

#### Scenario: Request with malformed Basic credentials

- **WHEN** a client sends a `Basic` `Authorization` header whose value cannot be decoded into a `username:password` pair
- **THEN** authentication fails with `401 Unauthorized` and the protected handler is not executed

#### Scenario: Request with credentials of an unknown user

- **WHEN** a client sends Basic credentials whose username matches no known user
- **THEN** authentication fails with `401 Unauthorized` and the protected handler is not executed

#### Scenario: Request with a wrong password for a known user

- **WHEN** a client sends Basic credentials whose username matches a known user but whose password does not match
- **THEN** authentication fails with `401 Unauthorized` and the protected handler is not executed

### Requirement: Credential store is provisioned by an initialization script

The system SHALL provide an idempotent initialization script that creates the credential store schema and seeds the `cms-webhook`, `administrator`, and `regular-user` users with passwords supplied by the operator. Running the script again over an already-initialized store SHALL succeed without duplicating data.

The script SHALL take the passwords as positional arguments. It SHALL NOT read credentials from environment variables: the legacy `AUTH_CMS_PASSWORD` variable SHALL NOT be consulted.

#### Scenario: Initializing a fresh store

- **WHEN** an operator runs the initialization script against a location with no credential store
- **THEN** the schema is created and the `cms-webhook`, `administrator`, and `regular-user` users are seeded with the supplied passwords

#### Scenario: Re-running the initialization script

- **WHEN** an operator runs the initialization script against an already-initialized store
- **THEN** the script completes successfully without errors and does not duplicate the seeded users

#### Scenario: Credentials are supplied as arguments

- **WHEN** an operator runs the initialization script with passwords as positional arguments
- **THEN** the users are seeded with those credentials and no environment variables are consulted

### Requirement: Passwords are stored as hashes

The credential store SHALL store each user's password exclusively as a PBKDF2 hash with a per-user random salt. Plaintext passwords SHALL NOT be written to the credential store.

#### Scenario: Seeding a user with a password

- **WHEN** a user is created in the credential store
- **THEN** only the PBKDF2 hash (with its salt and iteration count) is persisted, never the plaintext password

### Requirement: Passwords are verified against stored hashes

Authentication SHALL verify a presented password by re-deriving the hash with the stored salt and comparing it with the stored hash. Verification SHALL fail when the presented password does not match the stored hash.

#### Scenario: Correct password

- **WHEN** a user presents the password matching the stored hash
- **THEN** authentication succeeds

#### Scenario: Incorrect password

- **WHEN** a user presents a password that does not match the stored hash
- **THEN** authentication fails without revealing the stored hash

### Requirement: Credential store location is configurable

The location of the credential store SHALL be configurable through application configuration, including environment variables, so it can be pointed at a different store without code changes.

#### Scenario: Default configuration

- **WHEN** the application runs without overriding the credential store configuration
- **THEN** it uses the configured default store location

#### Scenario: Overridden store location

- **WHEN** an operator overrides the credential store configuration, for example via an environment variable
- **THEN** the application uses the overridden store location
