## Build and test

Requires the .NET 10 SDK. From the repo root:

- Build: `dotnet build TodoApi.slnx`
- Test: `dotnet test TodoApi.slnx`
- Format check: `dotnet format TodoApi.slnx --verify-no-changes` (style rules live in `.editorconfig`; warnings are errors, see `Directory.Build.props`)

`README.md` covers the run command, URLs, endpoints, telemetry, and test harness.

## Layout

- Feature slices: `src/TodoApi/Features/Todos/` — one file per operation, all wired in `TodoEndpoints.cs`. Put shared code in `Domain/`, `Storage/`, or `Contracts/`, not between slices (ADR-0001).
- Store: `src/TodoApi/Storage/` (`ITodoStore`, in-memory only; ADR-0002).
- Telemetry: `src/TodoApi/Telemetry/` and `src/TodoApi/Logging/`.
- Board: `src/TodoApi/Pages/`, Razor Pages reading `ITodoStore` in-process (ADR-0005).
- Tests mirror `src/` under `tests/TodoApi.Tests/`. Shared test infrastructure (`ApiTest`, `TodoApiFactory`, `TelemetryEnabledFactory`, `MetricsCapture`, `Tracing`, `ApiJson`, `Drafts`, `TestMeters`) stays at the root of `tests/TodoApi.Tests/` because it serves every area.

## Commits

Use Conventional Commits with an area scope, e.g. `feat(otel): ...`, `refactor(todos): ...`, `fix(otel): ...`, `test(otel): ...`, or `docs: ...`.

## Agent skills

### Issue tracker

Issues, specs, and tickets live as markdown files under `.scratch/<feature-slug>/`. When creating or fetching one, follow the layout and status conventions in `docs/agents/issue-tracker.md`.

### Triage labels

The five canonical triage roles map 1:1 to labels of the same name (`needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix`). Apply the role's string when triaging. See `docs/agents/triage-labels.md`.

### Domain docs

Single-context: `GLOSSARY.md` and `docs/adr/` at the repo root. Read both before naming domain concepts or changing behavior; use glossary terms and surface ADR conflicts. See `docs/agents/domain.md`.

### Repo skills

Workflow skills live in `.agents/skills/<name>/SKILL.md` (for example `to-spec`, `to-tickets`, `implement-spec`, `triage`, `wayfinder`); read the relevant skill before running its workflow.
