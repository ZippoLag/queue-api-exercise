## Purpose

The documentation rendered by the DocFX site — the conceptual markdown under `docs/` and the API reference generated from XML doc comments — SHALL be accurate: it describes event semantics precisely and reflects the implemented system instead of stale or misleading wording.

## ADDED Requirements

### Requirement: Event semantics are described precisely

The documentation SHALL describe the `delete` event as an unrecoverable removal of the entity from the store and SHALL distinguish it from `unPublish`, which keeps the entity in the store (merely hidden from regular users). The documentation SHALL NOT describe `delete` with wording that conflates it with `unPublish` (for example, "removed/unpublished for good") or that leaves the permanence of the removal ambiguous.

#### Scenario: Delete row states unrecoverable removal

- **WHEN** a reader looks up the meaning of the `delete` event in the documentation (for example, the Event semantics table in `docs/api-contract.md`)
- **THEN** the row describes the entity as deleted and removed from the store unrecoverably, without any wording that suggests the data is merely unpublished or disabled

#### Scenario: Delete and unPublish remain distinct

- **WHEN** a reader compares the `delete` and `unPublish` rows of the Event semantics table
- **THEN** the two events are described as distinct behaviors — `delete` removes the entity from the store, `unPublish` keeps it in the store while hiding it — so the table never implies one is a form of the other

### Requirement: Documentation reflects the implemented system

The documentation SHALL describe implemented capabilities as implemented. XML doc comments and conceptual markdown SHALL NOT refer to a capability that is already built as "future", "deferred", "to be implemented", or similar (for example, the Users API, which is implemented, must not be called "deferred" or "(future)"). Wording about genuinely unbuilt features MAY keep such qualifiers.

#### Scenario: No stale future/deferred wording about the Users API

- **WHEN** a reader consults the DocFX sources (conceptual markdown and XML doc comments of `src/**`) about the Users API
- **THEN** no comment or doc describes the implemented Users API as future or deferred
