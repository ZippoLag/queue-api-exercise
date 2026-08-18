## ADDED Requirements

### Requirement: Database provider is configurable

The EF Core database provider SHALL be selected by configuration through a `Db:Provider` value that defaults to `sqlite`; the SQLite provider SHALL remain the only wired implementation, with the registration structured so additional providers (for example, PostgreSQL) can be added by extending a single switch without changing any registration call site.

#### Scenario: Default provider is SQLite

- **WHEN** no `Db:Provider` value is configured
- **THEN** every EF Core registration (`AuthDbContext`, `CmsDbContext`, `UsersDbContext`, and the `AuthDbInit` tool) uses the SQLite provider

#### Scenario: Provider selection is documented

- **WHEN** a reader consults `docs/configuration.md` or `docs/deployment-aws.md`
- **THEN** the `Db:Provider` key (environment form `Db__Provider`) is documented with its default and its supported values

#### Scenario: Unknown provider fails fast

- **WHEN** `Db:Provider` is set to a value the switch does not support
- **THEN** the application fails to start with a descriptive error naming the supported providers

#### Scenario: Adding a provider requires no call-site change

- **WHEN** a new provider is added to the switch
- **THEN** no registration call site changes — the addition is a switch branch plus its EF Core provider package reference
