## RENAMED Requirements

### Requirement: Credentials are sourced from environment variables
**RENAMED TO:** Credentials are sourced from the credential store

## MODIFIED Requirements

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

#### Scenario: Valid configured username length
- **WHEN** the configured cms username is between 10 and 20 characters long
- **THEN** the application starts normally

#### Scenario: Invalid configured username length
- **WHEN** the configured cms username is shorter than 10 or longer than 20 characters
- **THEN** the application fails to start with a descriptive error
