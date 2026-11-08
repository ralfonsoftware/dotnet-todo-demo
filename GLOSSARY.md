# Todo API

A small demo HTTP API for tracking todo items through to completion.

## Language

**Todo Item**:
A unit of work tracked by the API, identified by a number and carrying a title, an optional description, a priority, and its completion state.
_Avoid_: Task, to-do, ticket, entry

**Completion**:
The one-way transition of a Todo Item to Done, stamped with the time it happened. A Completion either applies the transition or finds the Todo Item already done, in which case the original timestamp is kept; a completed Todo Item never returns to open.
_Avoid_: Close, finish, resolve, check off

**Done**:
The completion state of a Todo Item; true once Completion has occurred.
_Avoid_: Completed flag, finished

**Priority**:
The urgency level of a Todo Item: Low, Medium, or High, defaulting to Medium when unspecified.
_Avoid_: Severity, importance, rank

**Todos Store**:
The collection holding every Todo Item and the only source of truth for todo state.
_Avoid_: Repository, database, cache
