# Domain Specific Language Glossary/Dictionary

- **Administrator**: Administrator (aka "Admin") for our system, it's the only _user_ authorized to define which _entities_ are visible by the other, "regular", _users_. The reserved `administrator` username is provisioned by the initialization script and is the only one accepted by the Users API's enable/disable endpoints.
- **CMS**: Content Management System, an external party which is sending us information.
- **CmsEntity**: internal representation for our system about an _entity_ from the external CMS.
- **CmsEvent**: an _event_ published by the external _CMS_ which notifies our system of something that has already happened to an entity in it's domains, and to which our system may be required to react (eg: by updating it's store of _entities_). Contains full details of the received **CmsRequest** (except headers).
- **CmsEventLog**: the table in the dedicated CMS database where every accepted **CmsEvent** is durably recorded before it is processed; it acts as the system's _outbox_ and audit log of received deliveries.
- **CmsWebhook (username)**: the reserved `cms-webhook` username used by the external **CMS** to authenticate against the **CMSWebhook** API; its credentials are rejected by the **Users API**.
- **CmsRequest**: what the _webhook_ CMS API expects.
- **CMSWebhook**: the set of endpoints which respond to external requests by the CMS, intially consisting of a single _webhook_.
- **Outbox**: the ingestion pattern where the webhook validates and records incoming **CmsEvents** immediately (returning `201` to the CMS) and a background worker processes them asynchronously from the **CmsEventLog**.
- **Outbox worker**: the background component (_CmsEventProcessorWorker_) that applies recorded **CmsEvents** to the **CmsEntities** store, advancing each event from _Pending_ to _Processed_ or _Failed_.
- **Regular user**: any authenticated actor on the Users API other than the **Administrator** and the reserved **CMS** client; sees the published **CmsEntities** that have not been disabled by the **Administrator**. The reserved `regular-user` username is provisioned by the initialization script for local development and testing.
- **User**: an authenticated actor in our system, authorized to see the list of **CmsEntities**.
- **Webhook**: a[ lightweight, event-driven communication that automatically sends data between applications via HTTP](https://www.redhat.com/en/topics/automation/what-is-a-webhook).