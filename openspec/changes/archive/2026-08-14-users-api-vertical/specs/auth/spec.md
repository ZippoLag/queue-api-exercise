## MODIFIED Requirements

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
