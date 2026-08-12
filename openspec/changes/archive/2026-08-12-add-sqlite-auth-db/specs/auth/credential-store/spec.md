## Purpose

Defines the shared database-backed credential store used by the APIs: how it is provisioned, how passwords are stored and verified, and how its location is configured.

## ADDED Requirements

### Requirement: Credential store is provisioned by an initialization script
The system SHALL provide an idempotent initialization script that creates the credential store schema and seeds the `cms-webhook` user with a password supplied by the operator. Running the script again over an already-initialized store SHALL succeed without duplicating data.

#### Scenario: Initializing a fresh store
- **WHEN** an operator runs the initialization script against a location with no credential store
- **THEN** the schema is created and the `cms-webhook` user is seeded with the supplied password

#### Scenario: Re-running the initialization script
- **WHEN** an operator runs the initialization script against an already-initialized store
- **THEN** the script completes successfully without errors and does not duplicate the seeded user

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
