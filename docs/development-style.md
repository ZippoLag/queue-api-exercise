# Development Style

## Development approach

A common temptation in an exercise is to over-engineer as a way to display prowess. The requirements are instead taken at face value, as from a client: no over-thinking abstractions, no bolting-on external dependencies when they can be avoided.

The "standard" solution would be to pick up RabbitMQ and/or a host of libraries; this project deliberately keeps it as simple as possible at each increment.

Development follows TDD as much as possible, one increment at a time (see [Testing](testing.md) for the loop and gates).

## AI Assistance

AI assistance is used for the production of this solution, but not by delegating the full coding / doing SDD. Agents are guided one change at a time, and relevant prose text (such as these docs) is written by hand whenever a human voice should be preserved. Regarding DSL and "specs", a "code as source of truth" approach is taken: implementation code and naming conventions explicitly show the "what" and "how", with summary comments explaining the "why" always present.

## Conventions

- **Clean architecture and domain-driven design** — boundaries are never crossed; the layers stay separate (see [Architecture](architecture.md)).
- **CQRS** — writes and reads are independent.
- **XML doc comments** — required for tests, classes, properties, and methods; `<summary>` explains **what** in more detail than the name, `<remarks>` explains **why** (the business rule).
- **Testing** — xUnit, Moq, and FluentAssertions; unit coverage must include all corner cases, and tests must cite the source business rule (see [Testing](testing.md)).
- **Logging** — Serilog or leveled `Console.WriteLine`: `Information` for normal operations, `Warning` for recoverable edge cases, `Error` for failures requiring investigation.
- **AI-assisted change tracking** — OpenSpec and OpenLore track changes and detect spec/code drift (see [Tooling](tooling.md)).

## See also

- [Tooling](tooling.md) — installation and MCP wiring for Freebuff/OpenSpec/OpenLore.
- [Testing](testing.md) — quality gates, coverage ratchet, and E2E conventions.
- [Debugging](debugging.md) — the debugging surfaces and their conventions.
- [Architecture](architecture.md) — the system's design decisions.
