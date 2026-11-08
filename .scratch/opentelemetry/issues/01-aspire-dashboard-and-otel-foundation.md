# 01: Run the Aspire Dashboard and wire OpenTelemetry to it

**What to build:** A developer can start the Aspire Dashboard with one repo-local script and run the API, then immediately see HTTP server spans, runtime metrics, and application logs in the dashboard. The API identifies itself as `todo-api`, exports over OTLP/gRPC to `http://localhost:4317` by default, and keeps the endpoint, service name, and export enablement configurable. Tests never export telemetry.

**Blocked by:** None (can start immediately)

**Status:** resolved

- [x] A repo-local script starts the stable `mcr.microsoft.com/aspire/dashboard` container, maps the UI to `http://localhost:18888`, OTLP/gRPC to `http://localhost:4317`, and OTLP/HTTP to `http://localhost:4318`, enables anonymous local access, prints the endpoints, stays in the foreground, and removes the container on Ctrl+C. It fails with a clear message when Docker is unavailable.
- [x] The API exports traces, metrics, and logs over OTLP/gRPC to the Aspire Dashboard's endpoint, with service name `todo-api`, service version, and deployment environment resource attributes.
- [x] The default endpoint, service name, and export enablement can be overridden through standard `OTEL_*` environment variables and application configuration.
- [x] The dashboard shows ASP.NET Core request traces, HttpClient traces, ASP.NET Core/HttpClient/Runtime metrics, and application logs for service `todo-api`.
- [x] The test environment disables OTLP export, so `dotnet test TodoApi.slnx` passes with no dashboard running and makes no outbound telemetry attempts.
- [x] A test asserts the OpenTelemetry pipeline is registered when enabled and absent when disabled.
- [x] The README documents the two-terminal workflow: start the dashboard, run the API, open the UI. An ADR records the OTLP + Aspire Dashboard choice and the Docker-based local launcher.

## Outcome

`scripts/run-aspire-dashboard.sh` starts the pinned `mcr.microsoft.com/aspire/dashboard:13.6.4` image with the UI on `18888`, OTLP/gRPC on `4317`, OTLP/HTTP on `4318`, anonymous local access, foreground wait, Ctrl+C cleanup, and a clear Docker failure message. `Telemetry/` registers traces, metrics, and logs over OTLP/gRPC with service name `todo-api`, assembly version, and deployment environment; endpoint, service name, and export enablement resolve from `OTEL_*` variables, then the `OpenTelemetry` configuration section, then defaults. The `Testing` environment disables both the pipeline and the exporter, so tests never export. Registration tests use a shared `TelemetryEnabledFactory` with the exporter off. ADR-0003 and the README two-terminal workflow were added. A live create → complete → delete smoke run against the dashboard produced no OTLP errors. `dotnet test TodoApi.slnx`: 87 passed.
