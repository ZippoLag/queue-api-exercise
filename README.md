# Queue API Exercise

This repository aims to store the implementation of a .Net 9 platform-agnostic API solution which will accept messages which will eventually be processed resulting in their corresponding changes in a database.

## Development environment
To simplify the development experience, a Dockerfile is provided as dev container.

### Running instructions
TODO: write-up after implementation.

## Development approach
When given an exercise for an interview a common temptation is to over-engineer as a way to "flex" or display prowess, however I've chosen to tackle this as if it was a requirement coming from a client: taking the list of requirements at face value, not over-thinking abstractions and bolting-on external dependencies when they can be avoided.

Regular instinct and "current trends" / "best" practices would have guided me to a "standard" solution of "just" picking up RabbitMQ and/or a host of libraries, however I'm deliberately choosing to keep it as simple as possible at each increment.

Speaking of "increments", I will be developing this solution following TDD as much as possible.

### AI Assistance
I've been encouraged to rely on AI assistance for the production of this solution, however I won't just be delegating the full coding / doing SDD. I prefer to guide Agents one change at a time, and to write relevant text (such as this README) by hand whenever I want my voice to be preserved.

## Architecture / Plan
In tandem of the KISS principle, it would be an oversight in my years of experience to not treat this project as if it had plans to grow in the future, meaning I will aim to keep a clear separation of boundaries and domains within a Modular Monolith, following a Ports+Adapters and Clean architecture. Then, given the fact that from the start there are requirements for event handling and distinct flows (CMS VS Users), following an Event-Driven architecture (not Event-Sourcing for now) with CQRS also in place feels natural.