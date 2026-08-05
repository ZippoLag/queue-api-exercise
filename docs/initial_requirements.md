# Task: Build a .NET 9 service that ingests those events, processes them, stores them in a database, and exposes them via a clean API.

## Requirements:

### Data ingestion layer

Expose a webhook endpoint: /cms/events

Secure your endpoint using Basic Authentication.

Username: feel free to use any string with a length of 10 to 20 characters.
Password: random guid.

Accept batch events. The first version added is version 1, and you can get multiple revisions per entity. Schema example below:

```
[
 { "type": "publish", "id": "X", "payload": {...}, "version": 2, "timestamp": "2024-01-01T00:00:00Z"},
 { "type": "delete", "id": "Y", "timestamp": "2024-01-01T00:00:00Z" },
 { "type": "unPublish", "id": "Z", "payload": {...}, "version": 4, "timestamp": "2024-01-01T00:00:00Z"},
]
```

### Application Layer

Incoming data must be validated and sanitized.

Deleted entities should be removed (hard-delete), while unpublish should still keep the data in your persistence layer.

### Data storage

EF Core + Relational database of your choice.

Entities must keep track of the latest data version.

### REST API

Provide endpoints that support listing entities for consumers.

If the user is an admin (feel free to decide which userId/email corresponds to that of an admin), it will receive all entities that a normal user can see, including the ones that were disabled. No need to implement separate endpoints, feel free to decide how to approach this!

Data can not be updated by any kind of users, but an admin can disable them from the API - this will not affect the CMS, it’s an overwrite that does not affect CMS data!

### Performance

Feel free to use any asynchronous mechanisms to process incoming requests. This can also be synchronous, but we would like to understand why either decision was taken.

Use a read-only/writer configuration for your application context and optimize your EF read queries.

### Observability

Log processed events, including failing ones.

### Testing

Test how events are processed and whether the ingestion constraints are successfully followed by the service.

Test your basic authentication mechanism with valid/invalid user-password combinations.

### Deliverable

.NET Core 9 or above solution.

README must include setup/running instructions.

The solution should work on either Mac or Windows, so build it platform-agnostic.
