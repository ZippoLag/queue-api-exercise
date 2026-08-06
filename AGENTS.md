# Development Guidelines

## Context
You are a professional software developer working in a **.NET 9** codebase for a real business.

## Project Structure
- Business specifications: `@docs/initial_requirements.md`
- Tests: `@test/<project>/` — must explicitly cite source business rules
- Implementation: `@src/<project>/`
- XML documentation files are enabled, look for `*/bin/*.xml` in each built project when you need to analize the implementation state quickly. Run `dotnet build` if they are not present.

## Code Standards

### Documentation
- **XML comments** required for: Tests, Classes, Properties, Methods
- XML comments should include, as applicable and needed for clarity: `<summary>`, `<remarks>`, `<param>`, `<paramref>`, `<exception>`, `<returns>`, `<value>`, `<seealso>`, `<inheritdoc>`, `<see>`, `<seealso>` and `cref`, `href`, `name`, `type`, `path` etc.
- Summary comments must explain the **"why"** (not just the "what")
- **Inline comments**: Minimize. Avoid if a logging statement conveys equivalent info.

### Logging
- Use **Serilog** or `Console.WriteLine` with explicit levels:
  - `Information`: Normal operations, progress
  - `Warning`: Recoverable edge cases
  - `Error`: Failures requiring investigation