# Review action plan

Action plan for the project review of 2026-11-08. Each item lists the planned change, why it matters, the steps, and how to verify it. Item numbers match the review.

Baseline at review time: `dotnet build TodoApi.slnx` passes with 0 warnings, and `dotnet test TodoApi.slnx` passes all 145 tests. SDK `10.0.403`. No git remote is configured.

## Overview and order

Work top to bottom. Items marked **decision** need an explicit choice before implementation.

### Decisions (2026-11-08)

- **#3:** accepted. The store decorator owns outcome logging, `BoardMutations` is deleted, and ADR-0006 records the decision.
- **#11:** accepted. Outcome values move to snake_case, and the resource attribute becomes `deployment.environment.name`.
- **#18:** accepted. The remove list below is final.
- **#22:** accepted. Self-host Plus Jakarta Sans.

| # | Item | Priority | Depends on | Decision | Commit scope |
| --- | --- | --- | --- | --- | --- |
| 1 | OTLP `http/protobuf` endpoint bug | High | — | — | `fix(otel)` |
| 12 | `.editorconfig` + `Directory.Build.props` | High | — | — | `chore(build)` |
| 16 | Pin SDK in `global.json` | Medium | — | — | `chore(build)` |
| 17 | CI workflow | Medium | 12, 16 | Hosting | `ci` |
| 2 | Store takes `TodoDraft` | High | — | — | `refactor(todos)` |
| 3 | One owner for outcome logging | High | 2, 5 | Accepted | `refactor(todos)` |
| 4 | Break `Storage` ↔ `Telemetry` cycle | Medium | — | — | `refactor(otel)` |
| 5 | `IMeterFactory` | Medium | 4 | — | `refactor(otel)` |
| 9 | Unsafe `.First()` on edit errors | Medium | — | — | `fix(ui)` |
| 10 | Local vs UTC dates on board | Medium | — | — | `fix(ui)` |
| 6 | Expose `BoardReadModel` directly | Low | — | — | `refactor(ui)` |
| 7 | Board filter as enum | Low | 6 | — | `refactor(ui)` |
| 8 | `maxlength` from `TodoDraft` | Low | — | — | `fix(ui)` |
| 22 | Self-host the board font | Medium | — | Accepted | `fix(ui)` |
| 11 | Telemetry naming conventions | Low | 4 | Accepted | `refactor(otel)` |
| 13 | File names match types | Low | 4 | — | `refactor` |
| 14 | Tests mirror `src/` | Low | 13 | — | `test` |
| 15 | Small duplications | Low | — | — | `refactor(otel)` |
| 18 | Prune vendored skills | Medium | — | Accepted | `chore(agents)` |
| 19 | Normalize ticket status lines | Low | — | — | `docs` |
| 20 | Board spec and README links | Low | 19 | — | `docs` |
| 21 | Run command in `AGENTS.md` | Low | — | — | `docs` |
| 23 | Security headers / CSP | Deferred | — | — | — |
| 24 | Existing strengths | Keep | — | — | — |

## Global verification (after every item)

Run these after each item, and once more at the end:

```bash
dotnet build TodoApi.slnx          # 0 warnings, 0 errors
dotnet test TodoApi.slnx           # all tests pass; report any change to the test count
dotnet format TodoApi.slnx --verify-no-changes   # once item 12 is in place
```

End-to-end check at the end: run `./scripts/run-aspire-dashboard.sh` and `dotnet run --project src/TodoApi`. Use the board at `http://localhost:5266/` (create, edit, complete, delete), then call the API with the README `curl` examples. Confirm in `http://localhost:18888` that traces, metrics, and logs arrive for `todo-api`.

---

## Phase 1: Correctness and safety net

### 1. Fix OTLP `http/protobuf` export endpoint

- **Change.** Stop setting `OtlpExporterOptions.Endpoint` per signal with a shared base URL. Use `openTelemetry.UseOtlpExporter(options.Protocol, options.Endpoint)` when `ExporterEnabled` is true. It appends `/v1/traces`, `/v1/metrics`, and `/v1/logs` for `HttpProtobuf`, and it uses the base URL unchanged for gRPC.
- **Why.** The OpenTelemetry .NET docs say that with `HttpProtobuf`, an endpoint set in code must be the full signal URL. Today every signal POSTs to `http://localhost:4318/`, so the mode the README and ADR-0003 document fails. User-supplied `OTEL_EXPORTER_OTLP_ENDPOINT` values break the same way, because the code overwrites them.
- **Steps.**
  1. Write a failing test first. Start an in-process HTTP listener on a free port that records request paths. Build the app with `OpenTelemetry:Enabled=true`, `ExporterEnabled=true`, `Protocol=http/protobuf`, and `Endpoint=http://127.0.0.1:<port>`. Make one request to `/todos`, then force-flush the `TracerProvider` and `MeterProvider`, and dispose the host to flush logs. Assert that the recorded paths include `/v1/traces`, `/v1/metrics`, and `/v1/logs`.
  2. In `src/TodoApi/Telemetry/TelemetryRegistration.cs`, remove `ConfigureExporter` and the three `AddOtlpExporter` calls. Call `UseOtlpExporter(...)` once, inside `if (options.ExporterEnabled)`.
  3. Check that `UseOtlpExporter` is not combined with any per-signal `AddOtlpExporter`, because the SDK rejects that mix.
  4. Keep `IncludeFormattedMessage` and `IncludeScopes` on the logging options.
- **Verify.**
  - The new test fails before the fix and passes after it.
  - Existing `TelemetryTests` still pass.
  - Manual check: `OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf dotnet run --project src/TodoApi` with the dashboard running shows traces, metrics, and logs. Repeat with the default gRPC protocol.

### 12. Add `.editorconfig` and `Directory.Build.props`

- **Change.** Add a root `.editorconfig` with C# naming and style rules: private fields `_camelCase`, file-scoped namespaces, `var` preferences. Add a root `Directory.Build.props` with shared `TargetFramework`, `Nullable`, and `ImplicitUsings`, plus `TreatWarningsAsErrors`, `AnalysisLevel=latest-recommended`, and `EnforceCodeStyleInBuild=true`.
- **Why.** Field naming is already inconsistent: `_items` in `InMemoryTodoStore`, but `inner` with `this.` in `InstrumentedTodoStore`. Without enforced rules, agents and humans drift further. It also gives agents a cheap, objective style check.
- **Steps.**
  1. `dotnet new editorconfig`, then trim it to the rules that matter and set the private-field naming rule.
  2. Create `Directory.Build.props` and remove the duplicated properties from both `.csproj` files.
  3. Build and fix the new warnings. If there are too many, start with `AnalysisLevel=latest-minimum`, then raise it in a follow-up commit.
  4. Run `dotnet format TodoApi.slnx` and commit the formatting separately from the rule changes.
  5. Add the format check to `AGENTS.md` under "Build and test".
- **Verify.** Build has 0 warnings with `TreatWarningsAsErrors` on. `dotnet format TodoApi.slnx --verify-no-changes` exits 0. All tests pass.

### 16. Pin the SDK in `global.json`

- **Change.** Add `"sdk": { "version": "10.0.403", "rollForward": "latestFeature" }` next to the existing `test` section.
- **Why.** `AGENTS.md` requires the .NET 10 SDK but nothing enforces it. A pin gives reproducible builds locally and in CI.
- **Steps.** Edit `global.json`.
- **Verify.** `dotnet --version` in the repo root prints `10.0.4xx`. Build and tests pass.

### 17. Add CI

- **Change.** Add `.github/workflows/ci.yml`. It checks out the repo, runs `actions/setup-dotnet` with `global-json-file: global.json`, then runs `dotnet build`, `dotnet test`, and `dotnet format --verify-no-changes`.
- **Why.** Nothing protects the "all tests pass" state today. **Note:** the repo has no git remote yet, so this only matters once it is pushed to GitHub. If it will be hosted elsewhere, use that platform's equivalent.
- **Steps.**
  1. Create the workflow, triggered on `push` and `pull_request` to `main`.
  2. Upload `TestResults/` as an artifact when tests fail.
- **Verify.** After the first push, the workflow is green. A throwaway branch with a deliberately failing test turns it red.

### 9. Remove the unsafe `.First()` in the edit error path

- **Change.** In `Pages/Index.cshtml.cs:124-127`, replace `.First(message => !string.IsNullOrWhiteSpace(message))` with a helper `FirstErrorMessage(ModelStateDictionary)`. The helper returns the first non-empty message, or the fallback `"Check the highlighted fields."`.
- **Why.** `ModelError` instances built from exceptions have an empty `ErrorMessage`. If every error is like that, `.First` throws `InvalidOperationException` and the page returns a 500 instead of re-rendering.
- **Steps.**
  1. Add the helper as an `internal static` method on `IndexModel`, or in a small `BoardErrors` class.
  2. Use it in `OnPostUpdate`.
- **Verify.**
  - Unit test: a `ModelStateDictionary` whose only error is `new ModelError(new Exception())` returns the fallback.
  - A second test, where the dictionary also holds a message error, returns that message.
  - Existing `TodoPageTests` pass.

### 10. Use one time zone for board dates

- **Change.** Format "Added" and "Done" dates in the same zone as the "Today" label by converting with `TimeZoneInfo.ConvertTime(value, clock.LocalTimeZone)`. Do the conversion in `BoardReadModel`, or in a view helper that gets the `TimeProvider`.
- **Why.** Today the label uses `GetLocalNow()` but the item dates print UTC values. Near midnight in any non-UTC zone they show different days.
- **Steps.**
  1. Add a formatted-date helper that takes the `TimeProvider` (or its `TimeZoneInfo`).
  2. Use it for both `<time>` texts in `Index.cshtml`. Keep the `datetime` attribute as ISO 8601 UTC (`"O"`).
- **Verify.** A page test sets `FakeTimeProvider.SetLocalTimeZone` to a far-east zone (for example `Pacific/Kiritimati`, UTC+14), using the fixed clock `2026-11-08T11:16:00Z`. It asserts that the label reads `Monday, 9 November` and that a seeded item shows `Added 9 Nov`.
- **Out of scope.** Server-local versus browser-local time. Note it in ADR-0005 consequences if that ever matters.

---

## Phase 2: Design

### 2. `ITodoStore` accepts `TodoDraft`

- **Change.** Change the store methods to `TodoItem Create(TodoDraft draft, DateTimeOffset createdAt)` and `TodoItem? Update(int id, TodoDraft draft)`.
- **Why.** The store currently accepts raw `title`, `description`, and `priority` values, so any caller can skip validation. Taking the validated `TodoDraft` (a glossary term) makes invalid state unrepresentable at the store boundary. It also removes four copies of the field unpacking.
- **Steps.**
  1. Update `ITodoStore`, `InMemoryTodoStore`, and `InstrumentedTodoStore`.
  2. Update the callers: `CreateTodo`, `UpdateTodo`, and `BoardMutations`.
  3. Update the test doubles and helpers: `tests/.../Storage/RecordingTodoStore.cs`, plus `ApiTest.Seed` and `SeedCompleted`. They build drafts with `TodoDraft.TryCreate`, or through a small test helper that throws on invalid input.
  4. Update the XML docs on `ITodoStore`.
- **Verify.** Build and all tests pass. `grep -rn "store.Create(\|store.Update(" src` shows only draft-based calls.

### 3. One owner for Todo outcome logging (accepted)

- **Change.** Move store-outcome logging (succeeded, not found, already done) into the store decorator, using `ILogger<TodoLogCategory>`. HTTP slices and the board then log only what the store cannot know, which is validation rejections. `BoardMutations` shrinks to nothing, so delete it.
- **Why.** The same orchestrate-and-log code exists in the six slices and in `Pages/BoardMutations.cs`, and the two surfaces log under different categories. The decorator already classifies every outcome for spans and metrics, so it also owns the logs. Every caller, including future ones, then gets consistent logs for free.
- **Decision.** Accepted on 2026-11-08. This moves logs from the slices into storage instrumentation. It does not conflict with ADR-0001: shared code goes to `Storage/`, not between slices. ADR-0004 still holds, because the decorator logs `CompleteAlreadyDone` from the `CompletionResult` it already reads.
- **Steps.**
  1. Write `docs/adr/0006-store-decorator-owns-outcome-logs.md` (status: accepted). Cover the context (duplicate logging, split categories), the decision, the rejected minimal alternative (only unify the category in `BoardMutations`), and the consequence: callers log validation rejections only.
  2. Inject `ILogger<TodoLogCategory>` into the decorator. Register it through DI instead of `new` in `Program.cs`, together with item 5.
  3. Emit `CreateSucceeded`, `UpdateSucceeded`, `CompleteSucceeded`, `CompleteAlreadyDone`, `DeleteSucceeded`, `NotFound`, `GetSucceeded`, and `ListSucceeded` from the decorator. Keep every `TodoEventIds` value unchanged.
  4. Remove the same calls from the slices and from `TodoResults.NotFound`, which then only builds the problem result.
  5. Delete `BoardMutations`. `IndexModel` calls the store directly and logs only `CreateValidationRejected` and `UpdateValidationRejected`, using an injected `ILogger<TodoLogCategory>`.
  6. Make sure each operation logs exactly once.
  7. Update the README "Design" list and the `TodoLogs` and `ITodoStore` XML docs to say the decorator emits outcome logs.
- **Verify.**
  - `TodoLogsTests` and `BoardMutationLogTests` pass unchanged, or with only the category assertion changed. Event ids, levels, and properties are identical.
  - Add a test that runs one API call and one board post and asserts that both produce records under `TodoApi.Logging.TodoLogCategory`.
  - Add a test that asserts exactly one log record per operation.

### 4. Break the `Storage` ↔ `Telemetry` dependency cycle

- **Change.** Move `ActivitySourceName` (`"TodoApi.TodosStore"`) and `MeterName` (`"TodoApi.Todos"`) from `InstrumentedTodoStore` to `TodoTelemetry`.
- **Why.** `TelemetryRegistration` imports `TodoApi.Storage` only for these two constants, while `Storage` depends on `Telemetry`. With the move, dependencies point one way: Storage → Telemetry.
- **Steps.**
  1. Add the constants to `TodoTelemetry`.
  2. Update `InstrumentedTodoStore`, `TelemetryRegistration`, and the tests.
  3. Remove `using TodoApi.Storage;` from `Telemetry/`.
- **Verify.** `grep -rn "TodoApi.Storage" src/TodoApi/Telemetry` is empty. `TodoVocabularyTests` still pins the literal strings. All tests pass.

### 5. Create the meter through `IMeterFactory`

- **Change.** The `InstrumentedTodoStore` constructor takes `IMeterFactory` and calls `meterFactory.Create(TodoTelemetry.MeterName)`. The factory owns the meter's lifetime, so the decorator no longer implements `IDisposable`.
- **Why.** `new Meter(...)` is process-global. Parallel test hosts see each other's measurements, which is why `MetricsCapture` needs a non-parallel collection. `IMeterFactory` scopes meters to the DI container and is the recommended pattern on .NET 8+.
- **Steps.**
  1. Change the constructor, and register the decorator with `IMeterFactory` (and the logger from item 3) resolved from DI.
  2. In the unit tests, build a `ServiceCollection().AddMetrics()` provider to get a factory. Consider replacing `MetricsCapture` with `MetricCollector<T>` from `Microsoft.Extensions.Diagnostics.Testing`, which is scoped to one meter instance.
  3. Remove the non-parallel collection from the metrics tests once they no longer share state.
- **Verify.**
  - Metrics tests pass with parallelization enabled. Run the suite three times to catch flakiness.
  - `MetricsIntegrationTests` still see the `TodoApi.Todos` instruments through OpenTelemetry's `AddMeter`.
  - The dashboard still shows custom metrics.

### 6. Expose the board read model directly

- **Change.** Replace the seven copied properties and `Apply()` in `IndexModel` with `public BoardReadModel Board { get; private set; }`. The view reads `Model.Board.OpenCount` and so on.
- **Why.** Every new board field currently has to be added in three places. `DoneCount` is already defined twice.
- **Steps.**
  1. Change `IndexModel` and `Index.cshtml`.
  2. Keep the edit-form fields (`EditTitle`, `EditDescription`, `EditPriority`, `EditError`) on the page model, because they also carry posted values.
- **Verify.** `TodoPageTests` and `PresentationHookTests` pass.

### 7. Board filter as an enum

- **Change.** Add `enum BoardFilter { Open, Done, All }` in `Pages/`. Parse the `view` query value once in `IndexModel`, and keep the lowercase strings only at the URL boundary, for example through `PresentationHooks.FilterValue(BoardFilter)`.
- **Why.** `"open"`, `"done"`, and `"all"` are plain strings repeated in `IndexModel`, `BoardReadModel`, and `Index.cshtml`. A typo compiles fine and silently falls back to `open`.
- **Steps.**
  1. Add the enum and a parse helper with an `Open` fallback.
  2. Switch `BoardReadModel` to the enum.
  3. Generate the filter links in the view from the enum.
- **Verify.** Existing filter tests pass, including unknown values falling back to Open. `grep -rn '"done"\|"all"\|"open"' src/TodoApi/Pages` matches only the helper.

### 8. Take `maxlength` from `TodoDraft`

- **Change.** Replace the hard-coded `maxlength="200"` and `"2000"` in `Index.cshtml` (lines 38, 75, 149, 154) with `@TodoDraft.TitleMaxLength` and `@TodoDraft.DescriptionMaxLength`.
- **Why.** If the server-side limits change, the form limits drift silently.
- **Steps.** Edit the four attributes.
- **Verify.** A page test asserts that the rendered title inputs carry `maxlength="{TodoDraft.TitleMaxLength}"` and the textareas `maxlength="{TodoDraft.DescriptionMaxLength}"`.

### 22. Self-host the board font (accepted)

- **Change.** Remove the three Google Fonts `<link>` tags from `_Layout.cshtml`. Serve Plus Jakarta Sans (SIL OFL 1.1) from `wwwroot/fonts/`, declared with `@font-face` in `site.css`.
- **Why.** It is the only external request in a demo meant to work locally, and the board breaks visually offline. Loading Google Fonts from Google's CDN also sends visitor IPs to Google, which LG München (2022) ruled a GDPR violation.
- **Decision.** Accepted on 2026-11-08: self-host, keeping the current look.
- **Steps.**
  1. Get the font from the upstream project (`tokotype/PlusJakartaSans` on GitHub) or the Google Fonts download.
     - Preferred: one variable `woff2` covering weights 400–800 (`PlusJakartaSans[wght].woff2`).
     - Fallback: five static `woff2` files for 400, 500, 600, 700 and 800.
     - Latin subset only, if the source offers one.
  2. Place it at `src/TodoApi/wwwroot/fonts/` and add `OFL.txt` from the upstream release next to it. The license requires shipping it.
  3. Add to the top of `site.css`:
     ```css
     @font-face {
         font-family: "Plus Jakarta Sans";
         src: url("../fonts/PlusJakartaSans-Variable.woff2") format("woff2");
         font-weight: 400 800;
         font-style: normal;
         font-display: swap;
     }
     ```
     Keep the existing `font-family` declarations. Make sure they end with a system fallback (`system-ui, sans-serif`).
  4. Delete both `preconnect` links and the stylesheet link in `_Layout.cshtml`.
  5. Optional: add `<link rel="preload" href="~/fonts/PlusJakartaSans-Variable.woff2" as="font" type="font/woff2" crossorigin />` to avoid a flash of fallback text.
  6. Add a "Third-party assets" note to the README naming the font and its OFL license.
- **Verify.**
  - A page test asserts the rendered HTML contains no `googleapis` or `gstatic` hosts.
  - A test requests `/fonts/PlusJakartaSans-Variable.woff2` and asserts `200` with `Content-Type: font/woff2`.
  - Manual check with the browser network panel: only `localhost` requests, and the font loads from `/fonts/`. The board still renders correctly with the network disabled after first load.
  - Visual check: headings at weight 800 and body text at weight 400 look the same as before.

### 11. Telemetry naming conventions (accepted)

- **Change.** Align the observability contract with OpenTelemetry semantic conventions.

  | Contract | Current | New |
  | --- | --- | --- |
  | `todo.outcome` value | `found` | `found` (unchanged) |
  | `todo.outcome` value | `not found` | `not_found` |
  | `todo.outcome` value | `created` / `updated` / `completed` / `deleted` | unchanged |
  | `todo.outcome` value | `already done` | `already_done` |
  | Resource attribute | `deployment.environment` | `deployment.environment.name` |

  Tag keys (`todo.operation`, `todo.id`, `todo.priority`, `todo.outcome`), span names, instrument names, units, and `TodoEventIds` stay unchanged.
- **Why.** OpenTelemetry semantic conventions use snake_case enum values and have renamed the deployment attribute. Dashboards and queries expect both, and values with spaces need quoting in most query languages.
- **Decision.** Accepted on 2026-11-08. Saved queries that use the old values break. That is acceptable, because the dashboard is in-memory and has no saved state.
- **Steps.**
  1. In `TodoTelemetry.Outcomes`, set `NotFound = "not_found"` and `AlreadyDone = "already_done"`.
  2. In `TelemetryRegistration`, replace `["deployment.environment"]` with `["deployment.environment.name"]`. Use a constant in `TodoTelemetry` (for example `TodoTelemetry.Resource.DeploymentEnvironmentName`) so the contract test pins it.
  3. Update the docs:
     - README: the span table, the `todo.store.failures` row, the metrics prose ("reported as `already_done`"), and the "Configuration" note on `deployment.environment`.
     - ADR-0003 "Signals" bullet and ADR-0004 (`already done` → `already_done`).
     - `InstrumentedTodoStore` XML remarks.
     - The `CompleteAlreadyDone` log message text stays human-readable ("already done"). Only telemetry values change.
  4. Update the contract test (`TodoVocabularyTests`, renamed to `TodoTelemetryTests` in item 13) and every test asserting the old literals.
- **Verify.**
  - `grep -rn '"not found"\|"already done"\|deployment\.environment"' src tests docs README.md` returns nothing.
  - The contract test pins the new literals, and the tracing and metrics tests pass.
  - The dashboard shows `deployment.environment.name` on the resource and `todo.outcome=not_found` on a 404 span.

---

## Phase 3: Structure and style cleanup

### 13. Make file names match their types

- **Change.**
  - Split `Telemetry/TodoVocabulary.cs` into `TodoOperation.cs` and `TodoTelemetry.cs`.
  - Rename `Features/Todos/MapTodoEndpoints.cs` to `TodoEndpoints.cs`.
  - Move `TodoLogCategory` into `Logging/TodoLogCategory.cs`.
- **Why.** Agents and IDE search find types by file name, and one type per file keeps lookups predictable.
- **Steps.**
  1. Use `git mv` so history follows the files.
  2. Rename the test files to match, for example `TodoVocabularyTests.cs` → `TodoTelemetryTests.cs`.
  3. Update `AGENTS.md`, which says slices are "wired in `MapTodoEndpoints.cs`". ADR-0001 refers to the `MapTodoEndpoints` extension method, which keeps its name.
- **Verify.** Build and tests pass. `grep -rn "MapTodoEndpoints.cs\|TodoVocabulary" --exclude-dir={bin,obj} .` returns nothing.

### 14. Make tests mirror `src/`

- **Change.** Split the root `tests/TodoApi.Tests/TelemetryTests.cs` by subject under test:
  - `Telemetry/TelemetryRegistrationTests.cs`: pipeline registered or absent, logging options, console logging, endpoint still works.
  - `Telemetry/TelemetryOptionsTests.cs`: the `Resolve_*` tests.
  - Remove the cases that `TelemetrySettingsTests` already covers at the settings level.
- **Why.** `AGENTS.md` says tests mirror `src/`. Right now an agent looking for `TelemetryOptions` tests won't find them under `Telemetry/`.
- **Steps.**
  1. Move the tests into the two new files.
  2. Compare the precedence cases with `TelemetrySettingsTests` and drop exact duplicates.
  3. Leave the shared test infrastructure at the root (`ApiTest`, `TodoApiFactory`, `TelemetryEnabledFactory`, `MetricsCapture`, `Tracing`, `ApiJson`) and add one line to `AGENTS.md` explaining that.
- **Verify.** All tests pass. Report the change in test count, which should equal the number of duplicates removed.

### 15. Remove small duplications

- **Change.**
  - Keep one `FirstSet` helper. `TelemetryOptions.ResolveServiceVersion` can use `string.IsNullOrWhiteSpace` checks directly.
  - Delete `appsettings.Development.json`, which only repeats the logging block in `appsettings.json`.
  - Remove the undocumented `"http"` protocol alias in `TelemetryOptions.ParseProtocol`. OTLP spec values are `grpc`, `http/protobuf`, and `http/json`.
- **Why.** Less code that can drift. An undocumented alias is either a hidden contract or dead code.
- **Steps.**
  1. Apply the three edits.
  2. Add a test that `"http"` is now rejected, next to `Resolve_RejectsInvalidProtocol`.
- **Verify.** Tests pass. `dotnet run --project src/TodoApi` still logs at `Information` in Development.

---

## Phase 4: Agent-friendliness and docs

### 18. Prune vendored skills (accepted)

- **Change.** Remove skills that don't apply to this .NET repo or are unfinished.
  - Remove: `setup-ts-deep-modules`, `migrate-to-shoehorn`, `scaffold-exercises`, `setup-pre-commit` (JS/husky), `teach`, `writing-beats`, `writing-fragments`, `writing-shape`, `chief-of-staff`, `loop-me`, `claude-handoff`, `setup-matt-pocock-skills` (setup is done).
  - Keep: the skills referenced by `AGENTS.md` and `docs/agents/` (`to-spec`, `to-tickets`, `implement-spec`, `implement`, `triage`, `wayfinder`, `domain-modeling`, `grill-with-docs`, `improve-codebase-architecture`), plus `tdd`, `diagnosing-bugs`, `code-review`, `codebase-design`, `pr`, `handoff`, `grilling`, `research`, `prototype`.
- **Why.** About 40 skills make skill discovery noisy and invite irrelevant workflows, for example TypeScript tooling in a C# repo.
- **Decision.** Remove list accepted on 2026-11-08. Skills that appear in neither list (`ask-matt`, `git-guardrails-claude-code`, `grill-me`, `retro`, `to-questionnaire`, `wait-what`, `wizard`, `writing-for-agents`) stay.
- **Steps.**
  1. Remove the skills with the tool that maintains `skills-lock.json`, so the lock file stays consistent. Otherwise delete the directories and their lock entries by hand.
  2. Update `AGENTS.md` "Repo skills" if a listed example disappears.
- **Verify.**
  - Every key in `skills-lock.json` has a matching `.agents/skills/<name>/SKILL.md`, and every directory has a lock entry. Check with a short `jq` or `ls` comparison.
  - Every skill named in `AGENTS.md` and `docs/agents/*.md` still exists.

### 19. Normalize ticket status lines

- **Change.**
  - Use `Status: <value>` everywhere. Replace `**Status:** resolved` in `.scratch/opentelemetry/issues/*.md`.
  - Set `.scratch/todo-api/spec.md` to `Status: resolved`.
- **Why.** `docs/agents/issue-tracker.md` defines a `Status:` line, and agents scan for it with plain text matching. The bold variant is missed, and the stale `ready-for-agent` on a finished spec could trigger re-implementation.
- **Steps.**
  1. `sed -i '' 's/^\*\*Status:\*\* /Status: /' .scratch/opentelemetry/issues/*.md`
  2. Edit `spec.md`.
- **Verify.** `grep -L '^Status: ' .scratch/*/spec.md .scratch/*/issues/*.md` prints nothing. `grep -rh '^Status:' .scratch | sort | uniq -c` shows only expected values.

### 20. Document the board feature and link all tickets

- **Change.**
  - Add `.scratch/todo-board/spec.md` (`Status: resolved`), written after the fact from ADR-0005 and the README board section.
  - Add README "Design" links to the board spec and to `.scratch/opentelemetry/issues/`.
- **Why.** The board is the largest feature by code size, but it is the only one without a spec. Agents following `docs/agents/issue-tracker.md` can't find it.
- **Steps.** Write a short spec: goals, behaviour, out of scope, and links to ADR-0005 and the tests. Then add the README links.
- **Verify.** Every relative link in `README.md` resolves. Check by extracting the links and testing each path with `test -e`.

### 21. Put the run command in `AGENTS.md`

- **Change.** Add `Run: dotnet run --project src/TodoApi (board http://localhost:5266/, API /todos, docs /scalar)` under "Build and test".
- **Why.** Starting the app is the most common agent action after build and test, and right now it requires reading the README.
- **Steps.** Edit `AGENTS.md`. Keep the README pointer for the details.
- **Verify.** Read `AGENTS.md`. The command runs as written.

---

## Deferred / no action

### 23. Security headers and CSP (deferred)

No change now: this is a local, single-user demo. If the app is ever exposed beyond localhost, add `UseHsts`/`UseHttpsRedirection`, security headers, and a CSP. The inline theme script in `_Layout.cshtml` will then need a nonce or a move into `site.js`, and the antiforgery setup is already in place.

### 24. Keep these properties

Do not regress the following while doing the work above:

- POST/Redirect/GET with antiforgery, and the board working without JavaScript.
- `CompletionResult` (ADR-0004).
- Ids that are never reused.
- No title or description in logs, spans, or metrics.
- The pinned dashboard image and the launcher script tests.
- The fixed test clock.

The existing tests cover most of these. Keep them passing unchanged unless an item above explicitly changes a contract.
