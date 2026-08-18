## ADDED Requirements

### Requirement: Deployed Users API serves the browser UI

The AWS deployment SHALL ship the browser UI together with the Users API artifacts, so the users host serves the application shell at its origin root alongside the existing endpoints. The live-deployment verification SHALL include a check that the UI shell is served, so a deploy is reported successful only when the UI is reachable.

#### Scenario: UI artifacts ship with the deploy

- **WHEN** a deploy applies the Users API artifacts to the node
- **THEN** the users host serves the browser application shell at its origin root, alongside the existing endpoints

#### Scenario: Live verification covers the UI shell

- **WHEN** a deployment is verified against the live node
- **THEN** the verification fetches the users host origin root and confirms it returns the browser application shell

## MODIFIED Requirements

None — all existing AWS deployment requirements are unchanged.
