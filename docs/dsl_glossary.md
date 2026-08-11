# Domain Specific Language Glossary/Dictionary

- **Administrator**: Administrator (aka "Admin") for our system, it's the only _user_ authorized to define which _entities_ are visible by the other, "regular", _users_.
- **CMS**: Content Management System, an external party which is sending us information.
- **CmsEntity**: internal representation for our system about an _entity_ from the external CMS.
- **CmsEvent**: an _event_ published by the external _CMS_ which notifies our system of something that has already happened to an entity in it's domains, and to which our system may be required to react (eg: by updating it's store of _entities_). Contains full details of the received **CmsRequest** (except headers).
- **CmsRequest**: what the _webhook_ CMS API expects.
- **CMSWebhook**: the set of endpoints which respond to external requests by the CMS, intially consisting of a single _webhook_.
- **User**: an authenticated actor in our system, authorized to see the list of **CmsEntities**.
- **Webhook**: a[ lightweight, event-driven communication that automatically sends data between applications via HTTP](https://www.redhat.com/en/topics/automation/what-is-a-webhook).