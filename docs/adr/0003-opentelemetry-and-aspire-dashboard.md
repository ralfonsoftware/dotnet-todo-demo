# OpenTelemetry and the Aspire Dashboard

Status: accepted

The API emits traces, metrics, and logs through the OpenTelemetry .NET SDK and exports them over OTLP/gRPC to `http://localhost:4317` by default. Locally, a repo-local launcher (`scripts/run-aspire-dashboard.sh`) runs the standalone Aspire Dashboard container so a developer can see ASP.NET Core and HttpClient spans, ASP.NET Core/HttpClient/Runtime metrics, and application logs for the `todo-api` service in one UI.

## Decision

- **Signals.** Trace ASP.NET Core requests and outgoing HttpClient calls; collect ASP.NET Core, HttpClient, and .NET runtime metrics; export application logs. The resource carries `service.name` (`todo-api` by default), `service.version` from the assembly, and `deployment.environment` from the ASP.NET Core environment name.
- **Application metrics.** The `TodoApi.Todos` meter publishes Todo Item lifecycle counters (created by Priority, completed, deleted, failed store operations), current total and Done gauges, and a Todos Store operation duration histogram alongside the built-in instruments. Metrics never carry Todo Item title or description.
- **Transport.** OTLP/gRPC is the default (`OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317`). The protocol can be switched with `OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf`, which moves the default endpoint to `http://localhost:4318`.
- **Local viewer.** The Aspire Dashboard is started by `scripts/run-aspire-dashboard.sh` with the pinned stable image `mcr.microsoft.com/aspire/dashboard:13.6.4`. Host port 18888 is the UI; host ports 4317 and 4318 map to the container's OTLP/gRPC (18889) and OTLP/HTTP (18890) listeners. Anonymous local access is enabled with `DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true`.
- **Configuration.** Standard `OTEL_*` environment variables take precedence over the `OpenTelemetry` configuration section, which takes precedence over the built-in defaults. `OTEL_SDK_DISABLED=true` disables the pipeline regardless of application configuration. `OpenTelemetry:Enabled` controls whether the pipeline is registered; `OpenTelemetry:ExporterEnabled` controls whether an OTLP exporter is attached.
- **Tests.** The `Testing` environment loads `appsettings.Testing.json`, which sets both `Enabled` and `ExporterEnabled` to `false`. The suite therefore makes no outbound telemetry attempts and needs no dashboard. Registration tests enable the pipeline explicitly while leaving the exporter disabled.

## Why

The demo already has no external dependencies beyond the .NET SDK; adding one local container keeps that story intact. The Aspire Dashboard is a single image that renders traces, metrics, and logs, so it replaces a Jaeger + Prometheus + Grafana + log store stack. OTLP is vendor-neutral: the same instrumentation can later point at any OTLP-compatible backend by changing a configuration value.

## Alternatives considered

- **No telemetry.** Simplest, but the ticket's goal is to make request traces, runtime metrics, and logs visible immediately; console exporters would not show correlated traces.
- **`dotnet Aspire` AppHost orchestration.** Rich, but it adds an orchestration project and service-discovery model that this single-project demo does not need.
- **Jaeger + Prometheus + Grafana (and a log backend).** Standard, but four services and their configuration for a demo that only needs a local viewer.
- **Cloud APM (Application Insights or similar).** Requires an account and network egress, which conflicts with the local-first demo and with the rule that tests never export.
- **Unpinned `latest` dashboard image.** Rejected in favour of `13.6.4` so the documented ports and behaviour stay reproducible.

## Consequences

- Developers who want the dashboard need Docker; the script fails with a clear message when Docker or its daemon is unavailable.
- Dashboard state is in memory and disappears with the container; there is no durable telemetry store.
- The OTLP endpoint is plain HTTP on the loopback interface by default. This is a local development choice, not a production security posture.
- The API now constructs telemetry providers at startup when enabled, which has a small cost. Tests avoid both the cost and the network traffic by disabling the pipeline and the exporter.
