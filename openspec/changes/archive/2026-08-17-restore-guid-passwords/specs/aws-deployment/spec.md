## MODIFIED Requirements

### Requirement: Secrets are supplied by AWS Systems Manager

The three user passwords and both connection strings SHALL be stored as `SecureString` parameters in SSM Parameter Store and injected into the API processes as environment variables at boot. The committed local-development default passwords SHALL never be used by the deployed APIs; the initial creation of the environment SHALL generate fresh random GUID passwords for the seeded users, and re-deploys SHALL reuse the stored credentials. The documented password-rotation procedure SHALL generate new random GUID passwords, so the stored and seeded credentials always conform to the initial-requirements password rule.

#### Scenario: API credentials come from the parameter store

- **WHEN** the node boots an API
- **THEN** the process environment contains the connection strings and passwords read from SSM, not from the repository's committed defaults

#### Scenario: Seeded users authenticate with generated passwords

- **WHEN** a client authenticates against the deployed APIs using the generated credentials
- **THEN** authentication succeeds, and the local-development default passwords from the repository fail to authenticate

#### Scenario: Freshly generated passwords are GUIDs

- **WHEN** the environment is created and the three user passwords are generated
- **THEN** each generated password is a randomly generated GUID (8-4-4-4-12 with dashes)

#### Scenario: Rotated passwords are GUIDs

- **WHEN** an operator rotates a user password following the documented procedure
- **THEN** the new password stored in SSM and re-seeded into the credential store is a randomly generated GUID
