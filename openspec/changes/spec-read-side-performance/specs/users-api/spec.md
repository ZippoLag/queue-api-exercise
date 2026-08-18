## ADDED Requirements

### Requirement: Read and write paths use separated configurations

The Users API SHALL serve reads and writes on separated EF configurations. The `GET /entities` listing SHALL run on a read-only configuration: the query SHALL NOT track the returned entities and SHALL NOT mutate the store. The `POST /entities/{id}/disable` and `POST /entities/{id}/enable` commands SHALL run on a single-writer configuration that loads and persists the affected row in place. Listings SHALL return each entity's stored payload in full as a JSON string — payloads are not meant to be edited, so no endpoint SHALL accept payload content and the listing SHALL NOT project payloads away. Because the entity's read representation carries state the originating CMS event did not (a maintained update timestamp and the administrator-visibility flag), the read shape SHALL NOT be an exact match of the recorded event's shape: the recorded event carries only what the CMS sent, while the entity carries the processed state.

#### Scenario: Listing is served without tracking or writes

- **WHEN** a user requests `GET /entities`
- **THEN** the response is served from the read-only configuration: the returned entities are not tracked by EF and the request does not write to the store

#### Scenario: Visibility commands run on the writer configuration

- **WHEN** the administrator requests `POST /entities/{id}/disable` or `POST /entities/{id}/enable`
- **THEN** the affected row is loaded and persisted in place on the single-writer configuration

#### Scenario: Full stored payload is returned

- **WHEN** a user requests `GET /entities` and the store contains published entities with stored payloads
- **THEN** each returned item carries the entity's full stored payload as a JSON string

#### Scenario: Read shape differs from the recorded event shape

- **WHEN** a processed entity is listed
- **THEN** its item includes the maintained update timestamp and the administrator-visibility flag in addition to what the accepted event carried, so the item is not an exact match of the recorded event
