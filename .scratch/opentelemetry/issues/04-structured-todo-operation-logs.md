# 04: Emit structured logs for Todo Item operations

**What to build:** Todo Item operations emit structured log records with named properties rather than interpolated text: create, update, complete, and delete successes; not-found results; and validation rejections. The records carry the Todo Item id, operation, Priority where relevant, and error count for validation, and appear in the dashboard correlated with the active trace while the console stays readable.

**Blocked by:** 01 — Run the Aspire Dashboard and wire OpenTelemetry to it

Status: resolved

- [x] All new log events use source-generated `LoggerMessage` definitions with stable event IDs and named parameters.
- [x] Successful create, update, complete, and delete operations log at information level; not-found and validation-rejected outcomes log at warning level; successful reads log at debug level.
- [x] Events carry the Todo Item id, operation, Priority where relevant, and error count for validation, but never Todo Item title or description.
- [x] OpenTelemetry logging exports scopes and formatted messages and correlates log records with the trace and span identifiers.
- [x] Console logging remains enabled and human-readable.
- [x] Tests capture log records and assert event IDs and structured properties rather than matching formatted strings.
- [x] The dashboard's structured logs page shows the properties and trace correlation for a create and a completion request.

## Outcome

Source-generated `LoggerMessage` events in `Logging/TodoLogs.cs` use stable IDs: `1001`–`1004` information for create/update/complete/delete success, `1101` warning for not-found (get/update/complete/delete), `1201`/`1202` warning for create/update validation with an error count (update includes the Todo Item id), and `1301`/`1302` debug for successful get/list. Named properties carry `Operation`, `TodoId`, `Priority`, and `ErrorCount` but never title or description. Console logging stays human-readable; OpenTelemetry logging keeps scopes and formatted messages and correlates via the active Activity. Capturing `ILoggerProvider` tests assert event IDs, levels, and structured state, including a privacy sweep across all six operations. `dotnet test TodoApi.slnx`: 87 passed.
