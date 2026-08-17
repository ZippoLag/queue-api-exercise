# Domain Specific Language Glossary/Dictionary

## Naming and synonyms

Different spellings of the same thing appear across the codebase and docs on purpose — each maps to a specific context (code identifier, prose title, reserved string). The table below is the single place that reconciles them; use the context-appropriate spelling and never "fix" one into the other.

| Term | Context / when to use | Notes |
|---|---|---|
| `cms-webhook` | the reserved username, and only that | literal string; never renamed |
| `CmsWebhook` | C# project/type names (`CmsWebhook.Api`, `CmsWebhook.Domain`) | CamelCase per C# convention |
| `CMS Webhook` / `CMS Webhook API` | human-facing prose titles | preferred spelling in docs prose |
| `CMSWebhook` | legacy glossary spelling | a synonym of "CMS Webhook"; not used for new prose |
| `Users API` | human-facing prose | "User API" is not used |
| `CmsEntity`, `CmsEvent`, `CmsRequest`, `CmsEventLog`, `Outbox`, `Outbox worker` | domain terms | canonical definitions below |

## Glossary

- **Administrator**: Administrator (aka "Admin") for our system, the only _user_ authorized to define which _entities_ are visible by the other, "regular", _users_. The reserved `administrator` username is provisioned by the initialization script and is the only one accepted by the Users API's enable/disable endpoints.
- **CMS**: Content Management System, an external party which is sending us information.
- **CMS Webhook API**: the set of endpoints which respond to external requests by the CMS, initially consisting of a single _webhook_ (`POST /cms/events`). `CMSWebhook` is the legacy glossary spelling of this term (see [Naming and synonyms](#naming-and-synonyms)).
- **CmsEntity**: internal representation for our system about an _entity_ from the external CMS.
- **CmsEvent**: an _event_ published by the external _CMS_ which notifies our system of something that has already happened to an entity in its domains, and to which our system may be required to react (eg: by updating its store of _entities_). Contains full details of the received **CmsRequest** (except headers).
- **CmsEventLog**: the table in the dedicated CMS database where every accepted **CmsEvent** is durably recorded before it is processed; it acts as the system's _outbox_ and audit log of received deliveries.
- **CmsRequest**: what the _webhook_ CMS API expects.
- **Outbox**: the ingestion pattern where the webhook validates and records incoming **CmsEvents** immediately (returning `201` to the CMS) and a background worker processes them asynchronously from the **CmsEventLog**.
- **Outbox worker**: the background component (_CmsEventProcessorWorker_) that applies recorded **CmsEvents** to the **CmsEntities** store, advancing each event from _Pending_ to _Processed_ or _Failed_.
- **Regular user**: any authenticated actor on the Users API other than the **Administrator** and the reserved **CMS** client; sees the published **CmsEntities** that have not been disabled by the **Administrator**. The reserved `regular-user` username is provisioned by the initialization script for local development and testing.
- **User**: an authenticated actor in our system, authorized to see the list of **CmsEntities**.
- **Webhook**: a[ lightweight, event-driven communication that automatically sends data between applications via HTTP](https://www.redhat.com/en/topics/automation/what-is-a-webhook).
- **cms-webhook**: the reserved username used by the external **CMS** to authenticate against the **CMS Webhook API**; its credentials are rejected by the **Users API**. Never renamed (see [Naming and synonyms](#naming-and-synonyms)).
