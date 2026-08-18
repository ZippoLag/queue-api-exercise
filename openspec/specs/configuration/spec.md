# Configuration Specification

> Source files: src/Shared/QueueApi.Persistence/DbContextOptionsBuilderExtensions.cs, src/Shared/QueueApi.Persistence/QueueApi.Persistence.csproj, docs/configuration.md

## Purpose

Defines how the applications resolve configuration per environment: per-environment configuration files, the 12-factor precedence chain (base file, environment file, user-secrets in Development only, environment variables), and an explicitly configurable database base directory that removes the repository-marker walk from startup.

## Requirements

### Requirement: Per-environment configuration files

The system SHALL load environment-specific configuration from `appsettings.{Environment}.json` for the Development, Staging, and Production environments, layered on top of the base `appsettings.json`, so each environment can override values without touching shared defaults.

#### Scenario: Development environment applies its config file

- **WHEN** the application runs in the Development environment
- **THEN** values from `appsettings.Development.json` are applied on top of `appsettings.json`

#### Scenario: Staging environment applies its config file

- **WHEN** the application runs in the Staging environment
- **THEN** values from `appsettings.Staging.json` are applied on top of `appsettings.json`

#### Scenario: Production environment applies its config file

- **WHEN** the application runs in the Production environment
- **THEN** values from `appsettings.Production.json` are applied on top of `appsettings.json`

### Requirement: Configuration precedence chain

Configuration values SHALL be resolved from layered sources in a fixed precedence order, with later sources overriding earlier ones: base `appsettings.json`, then `appsettings.{Environment}.json`, then user-secrets (Development only), then environment variables. User-secrets SHALL NOT be consulted outside the Development environment. Sensitive values (connection strings, passwords) SHALL NOT be committed as defaults in the committed configuration files; they SHALL be supplied per environment through user-secrets (Development) or environment variables (Staging/Production).

#### Scenario: Environment variable overrides configuration files

- **WHEN** an environment variable such as `ConnectionStrings__CmsDb` is set
- **THEN** its value wins over any value in `appsettings.json` and `appsettings.{Environment}.json`

#### Scenario: Environment file overrides base configuration

- **WHEN** `appsettings.{Environment}.json` sets a value that also exists in `appsettings.json`
- **THEN** the environment-specific value is used

#### Scenario: User-secrets override configuration files in Development

- **WHEN** the application runs in Development and a user-secret value is set
- **THEN** the user-secret value is used over `appsettings.json` and `appsettings.Development.json`

#### Scenario: User-secrets are ignored outside Development

- **WHEN** the application runs in Staging or Production and user-secrets exist
- **THEN** the user-secrets are not consulted and their values have no effect

### Requirement: Database base directory is explicit

The system SHALL resolve relative SQLite data sources against an explicitly configured database base directory rather than searching the filesystem for a repository marker file. Absolute and in-memory data sources SHALL be used as-is. When no base directory is configured, relative data sources SHALL resolve against the application's content root.

#### Scenario: Relative data source resolves against the configured base directory

- **WHEN** the database base directory is configured and a connection string contains a relative data source
- **THEN** the database file is created under the configured base directory

#### Scenario: Absolute data source is used as-is

- **WHEN** a connection string contains an absolute data source
- **THEN** the absolute path is used and no base-directory resolution applies

#### Scenario: In-memory data source is used as-is

- **WHEN** a connection string contains an in-memory data source
- **THEN** the data source is used as-is and no base-directory resolution applies

#### Scenario: No repository marker is required

- **WHEN** the application runs from a deployment directory that does not contain the repository marker file
- **THEN** the application starts and resolves relative data sources against the configured base directory or the content root

#### Scenario: Database directory is created when missing

- **WHEN** a relative data source resolves to a directory that does not exist
- **THEN** the application creates the directory before opening the database file

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
