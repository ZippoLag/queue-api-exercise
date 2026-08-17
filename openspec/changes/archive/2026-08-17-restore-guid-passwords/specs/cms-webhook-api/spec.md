## MODIFIED Requirements

### Requirement: Configured credential format

The configured cms username SHALL be between 10 and 20 characters in length. A configuration that violates the username length rule SHALL cause the application to fail to start. The password is not a startup configuration value: it is provisioned into the credential store by the initialization script and verified against its stored hash.

The password provisioned for the cms user SHALL be a randomly generated GUID, as the initial requirements specify. The initialization script SHALL reject a supplied password that is not a GUID with a descriptive error, so a non-GUID password is never stored.

The cms username SHALL be read exclusively from the `Auth:CmsUsername` configuration value (default `cms-webhook`). The legacy `AUTH_CMS_*` environment variables SHALL NOT be consulted: they have no effect on the authorized username.

#### Scenario: Valid configured username length

- **WHEN** the configured cms username is between 10 and 20 characters long
- **THEN** the application starts normally

#### Scenario: Invalid configured username length

- **WHEN** the configured cms username is shorter than 10 or longer than 20 characters
- **THEN** the application fails to start with a descriptive error

#### Scenario: Provisioned password is a randomly generated GUID

- **WHEN** the initialization script provisions the cms user's password into the credential store
- **THEN** the stored hash verifies against the supplied GUID password

#### Scenario: Non-GUID password is rejected

- **WHEN** an operator supplies a password that is not a GUID to the initialization script
- **THEN** the script fails with a descriptive error and no user is created or modified

#### Scenario: Legacy environment variables are ignored

- **WHEN** a legacy `AUTH_CMS_*` environment variable is set but `Auth:CmsUsername` is not overridden
- **THEN** the application uses the configured default username and the environment variable has no effect
