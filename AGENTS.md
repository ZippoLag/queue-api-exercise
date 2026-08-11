# Development Guidelines

## Context
You are a professional software developer working in a **.NET 9** codebase for a real business.
Prefer using the `dotnet` CLI for operations over raw bash scripts when applicable.

## Project Structure
- Domain knowledge: `@docs/dsl_glossary.md` 
- Current architecture plan: `@docs/architecture.md`
- Tests: `@test/<project>/` — must explicitly cite source business rules
- Implementation: `@src/<project>/`
- XML documentation files are enabled, look for `*/bin/*.xml` in each built project when you need to analize the implementation state quickly. Run `dotnet build` if they are not present.

## Code Standards
### Testing coverage
- Unit Testing coverage must include all corner cases
- Unit Testing code should be as concise and easy to understand as possible without requiring inline comments
- Testing project uses xUnit, Moq and FluentAssertions, make full use of their capabilities

### Documentation
- **XML comments** required for: Tests, Classes, Properties, Methods
- XML comments should include, as applicable and needed for clarity: `<summary>`, `<remarks>`, `<param>`, `<paramref>`, `<exception>`, `<returns>`, `<value>`, `<seealso>`, `<inheritdoc>`, `<see>`, `<seealso>` and `cref`, `href`, `name`, `type`, `path` etc.
- `<summary>` comments must explain **what** the commented code does into greater detail than merely it's name.
- `<remarks>` comments must explain **why** it's implemented in this way and/or from which business rule this comes from.
- **Inline comments**: Minimize. Avoid if a logging statement conveys equivalent info.

### Logging
- Use **Serilog** or `Console.WriteLine` with explicit levels:
  - `Information`: Normal operations, progress
  - `Warning`: Recoverable edge cases
  - `Error`: Failures requiring investigation