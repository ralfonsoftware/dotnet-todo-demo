# Completion is a reported transition

Status: accepted

`ITodoStore.Complete` returns a `CompletionResult` that names the outcome — not found, completed, or already done — instead of a nullable `TodoItem`. The store classifies the outcome inside the same lock that applies the transition, so telemetry and the HTTP slice read one fact instead of re-deriving the already-done case from a pre-read. A repeated Completion keeps the original timestamp, records `already_done` on the store span, increments `todo.items.completed` only for a real transition, logs `CompleteAlreadyDone`, and returns `200` with the unchanged Todo Item.

## Considered options

- **Nullable `TodoItem?` plus telemetry inference** (the previous shape): forced the decorator to pre-read under its own lock and made the log report a successful mutation while the span reported `already_done`.
- **A `Completion` type in the domain carrying a `NotFound` case**: rejected because not-found is the store's answer about the lookup, not an outcome of the transition. `CompletionResult` lives in `Storage`.
- **A general outcome type for every store operation**: rejected as interface surface without a caller; only Completion has a hidden second success branch.
