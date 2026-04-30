# Tasks: Email Delivery Management

**Input**: Design documents from `/specs/006-email-delivery/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, quickstart.md, contracts/api.md

**Tests**: Included — TDD approach per project conventions.

**Organization**: Tasks grouped by user story for independent implementation and testing.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3, US4)
- Includes exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: New contract DTOs, service interfaces, and DI registration needed by all user stories

- [ ] T001 [P] Create `TestDeliveryResponse` record in `src/SunnySunday.Core/Contracts/TestDeliveryResponse.cs` — `bool Success`, `string? Error`
- [ ] T002 [P] Create `DeliveryRecord` record in `src/SunnySunday.Core/Contracts/DeliveryRecord.cs` — `ScheduledFor` (ISO 8601 string), `Status`, `AttemptCount`, `ErrorMessage?`, `DeliveredAt?`
- [ ] T003 [P] Create `DeliveryResponse` record in `src/SunnySunday.Core/Contracts/DeliveryResponse.cs` — `IReadOnlyList<DeliveryRecord> Items`, `int Total`, `int Offset`, `int Limit`
- [ ] T004 Add `SmtpReady` boolean property to existing `StatusResponse` in `src/SunnySunday.Core/Contracts/StatusResponse.cs`
- [ ] T005 [P] Create `ISmtpReadinessService` interface in `src/SunnySunday.Server/Services/ISmtpReadinessService.cs` — `bool IsReady { get; }`, `IReadOnlyList<string> MissingFields { get; }`
- [ ] T006 Create `SmtpReadinessService` singleton in `src/SunnySunday.Server/Services/SmtpReadinessService.cs` — inject `IOptions<SmtpSettings>`, check Host/Username/Password/FromAddress are non-null/non-whitespace, populate `MissingFields` with names of empty fields
- [ ] T007 Register `ISmtpReadinessService` as singleton in `src/SunnySunday.Server/Program.cs` and add startup warning log via `ISmtpReadinessService.IsReady` check after `app.Build()` (FR-006-04)

**Checkpoint**: Solution builds. `SmtpReadinessService` is registered. New DTOs compile. No endpoints wired yet.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Data access and validation helpers that multiple user stories depend on

- [ ] T008 Add `GetDeliveriesAsync(int userId, int offset, int limit)` method to `src/SunnySunday.Server/Data/RecapRepository.cs` — returns `(IReadOnlyList<RecapJobRecord> Items, int Total)` using `SELECT COUNT(*)` + paginated `SELECT ... ORDER BY scheduled_for DESC LIMIT @Limit OFFSET @Offset`
- [ ] T009 Add `IsValidKindleEmail(string value, out string normalized)` private method to `src/SunnySunday.Server/Endpoints/SettingsEndpoints.cs` — trims, lowercases, runs existing `EmailRegex()`, then checks domain ends with `@kindle.com` or `@free.kindle.com`; replace the `IsValidEmail` call for `kindleEmail` in `PUT /settings` handler with `IsValidKindleEmail`; update error message to `"Kindle email must end with @kindle.com or @free.kindle.com."`

**Checkpoint**: Solution builds. `PUT /settings` rejects non-Kindle email domains. `RecapRepository.GetDeliveriesAsync` compiles. No new endpoints yet.

---

## Phase 3: User Story 3 — SMTP Configuration Readiness Check (Priority: P2)

**Goal**: Users can check SMTP readiness via `GET /status` and `sunny delivery status` without inspecting env vars or Docker config.

**Independent Test**: Start server with/without SMTP settings, verify `GET /status` reflects `smtpReady`, and `sunny delivery status` renders the state.

> Note: This story is implemented before US1 (Test Delivery) because US1 depends on `SmtpReadinessService` being wired into `StatusEndpoints` and verified.

### Tests for User Story 3

- [ ] T010 [P] [US3] Add unit tests in `src/SunnySunday.Tests/Infrastructure/SmtpReadinessServiceTests.cs` — test `IsReady=true` when all fields set; `IsReady=false` when Host/Username/Password/FromAddress missing; `MissingFields` lists each missing field name
- [ ] T011 [P] [US3] Add integration tests in `src/SunnySunday.Tests/Api/StatusEndpointTests.cs` — test `GET /status` returns `smtpReady: true` when SMTP configured; `smtpReady: false` when settings incomplete; response shape matches updated `StatusResponse`

### Implementation for User Story 3

- [ ] T012 [US3] Update `StatusEndpoints.MapStatusEndpoints` in `src/SunnySunday.Server/Endpoints/StatusEndpoints.cs` — inject `ISmtpReadinessService`, set `status.SmtpReady = smtpReadiness.IsReady` before returning
- [ ] T013 [US3] Implement `sunny delivery status` CLI command in `src/SunnySunday.Cli/Program.cs` — call `GET /status` via `HttpClient`, display SMTP readiness (ready/not configured), show Host, Port, FromAddress (never credentials); use `AnsiConsole.MarkupLine` for output; read server URL from `SUNNY_SERVER_URL` env var (default `http://localhost:5000`)

**Checkpoint**: `GET /status` includes `smtpReady`. Unit tests for SmtpReadinessService pass. `sunny delivery status` renders SMTP state.

---

## Phase 4: User Story 4 — Kindle Email Validation (Priority: P3)

**Goal**: `PUT /settings` validates Kindle email format and normalizes input before storage.

**Independent Test**: Call `PUT /settings` with various email formats — only `@kindle.com` / `@free.kindle.com` accepted.

### Tests for User Story 4

- [ ] T014 [P] [US4] Add/update integration tests in `src/SunnySunday.Tests/Api/SettingsEndpointTests.cs` — test `PUT /settings` with `user@kindle.com` (accepted), `user@free.kindle.com` (accepted), `user@gmail.com` (rejected 422), empty string (rejected 422), `" User@Kindle.COM "` (accepted after normalization, stored lowercase/trimmed), existing generic email validation still works for non-Kindle fields

**Checkpoint**: All Kindle email validation tests pass. Invalid domains rejected with 422 and actionable message.

---

## Phase 5: User Story 1 — Test Delivery to Kindle (Priority: P1)

**Goal**: Users can verify their SMTP + Kindle email config by sending a test EPUB without affecting recap state.

**Independent Test**: Configure valid SMTP + Kindle email, call `POST /test-delivery`, verify test EPUB is composed and sent; verify no database writes occur.

### Tests for User Story 1

- [ ] T015 [P] [US1] Create `src/SunnySunday.Tests/Api/TestDeliveryEndpointTests.cs` — tests: (1) `POST /test-delivery` returns 422 when SMTP not configured with missing field names, (2) returns 422 when Kindle email not set, (3) returns 200 `{ success: true }` when SMTP ready + Kindle set + `IMailDeliveryService` succeeds (mock or test double), (4) returns 200 `{ success: false, error: "..." }` when `IMailDeliveryService` throws `SmtpCommandException`, (5) verify no `recap_jobs` rows created after test delivery

### Implementation for User Story 1

- [ ] T016 [US1] Create `src/SunnySunday.Server/Endpoints/TestDeliveryEndpoints.cs` — `POST /test-delivery` route: check `ISmtpReadinessService.IsReady` → 422 if not (include `MissingFields`); check `UserRepository` for Kindle email → 422 if empty; compose test EPUB via `EpubComposer.Compose` with 2–3 synthetic `SelectionCandidate` placeholder highlights; send via `IMailDeliveryService.SendRecapAsync`; catch `SmtpCommandException` / `SmtpProtocolException` → return `TestDeliveryResponse { Success = false, Error = actionable message }`; on success return `TestDeliveryResponse { Success = true }`
- [ ] T017 [US1] Register test delivery endpoint in `src/SunnySunday.Server/Program.cs` — add `app.MapTestDeliveryEndpoints()` call alongside other endpoint mappings
- [ ] T018 [US1] Implement `sunny delivery test` CLI command in `src/SunnySunday.Cli/Program.cs` — call `POST /test-delivery` via `HttpClient`, parse `TestDeliveryResponse`, display success/failure with `AnsiConsole.MarkupLine`; show actionable error on failure

**Checkpoint**: `POST /test-delivery` works end-to-end. CLI `sunny delivery test` displays result. All test delivery tests pass. No recap state modified.

---

## Phase 6: User Story 2 — Delivery History Visibility (Priority: P2)

**Goal**: Users can review paginated past delivery attempts via `GET /deliveries` and `sunny delivery log`.

**Independent Test**: Seed `recap_jobs` with various statuses, call `GET /deliveries`, verify pagination, table rendering in CLI.

### Tests for User Story 2

- [ ] T019 [P] [US2] Create `src/SunnySunday.Tests/Api/DeliveryEndpointTests.cs` — tests: (1) `GET /deliveries` returns empty list with `total: 0` when no jobs, (2) returns paginated results ordered by `scheduled_for DESC`, (3) respects `offset`/`limit` params, (4) returns correct `total` across pages, (5) clamps `limit` to max 100, (6) response excludes `id` and `user_id` fields, (7) response does not contain SMTP credentials

### Implementation for User Story 2

- [ ] T020 [US2] Create `src/SunnySunday.Server/Endpoints/DeliveryEndpoints.cs` — `GET /deliveries` route with `offset` (default 0, min 0) and `limit` (default 20, min 1, max 100) query params; call `RecapRepository.GetDeliveriesAsync`; map `RecapJobRecord` → `DeliveryRecord` (ISO 8601 strings for dates); return `DeliveryResponse` with `Items`, `Total`, `Offset`, `Limit`
- [ ] T021 [US2] Register delivery endpoint in `src/SunnySunday.Server/Program.cs` — add `app.MapDeliveryEndpoints()` call
- [ ] T022 [US2] Implement `sunny delivery log` CLI command in `src/SunnySunday.Cli/Program.cs` — accept `--page N` argument (default 1); call `GET /deliveries?offset=((N-1)*20)&limit=20`; render `Spectre.Console.Table` with columns Date, Status, Attempts, Error, Delivered At; show "No deliveries recorded yet." if empty; show pagination info (page X of Y)

**Checkpoint**: `GET /deliveries` returns paginated results. CLI `sunny delivery log` renders table. All delivery endpoint tests pass.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Swagger docs, build validation, final integration check

- [ ] T023 [P] Add Swagger metadata (`.WithSummary`, `.WithDescription`, `.Produces<>`) to `POST /test-delivery` in `src/SunnySunday.Server/Endpoints/TestDeliveryEndpoints.cs` and `GET /deliveries` in `src/SunnySunday.Server/Endpoints/DeliveryEndpoints.cs`
- [ ] T024 Run full test suite (`dotnet test src/SunnySunday.Tests`) and verify all new and existing tests pass
- [ ] T025 Run quickstart.md manual verification steps from `specs/006-email-delivery/quickstart.md` — verify `GET /status` includes `smtpReady`, `PUT /settings` validates Kindle email, `POST /test-delivery` works, `GET /deliveries` paginates, all three CLI commands execute

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 (DTOs + service interface must exist)
- **US3 — SMTP Readiness (Phase 3)**: Depends on Phase 2 (SmtpReadinessService registered)
- **US4 — Kindle Validation (Phase 4)**: Depends on Phase 2 (`IsValidKindleEmail` in Phase 2; tests only need Phase 2)
- **US1 — Test Delivery (Phase 5)**: Depends on Phase 3 (needs SmtpReadinessService wired into status, endpoint pattern established)
- **US2 — Delivery History (Phase 6)**: Depends on Phase 2 (`GetDeliveriesAsync` in RecapRepository)
- **Polish (Phase 7)**: Depends on all user story phases complete

### User Story Dependencies

- **US3 (SMTP Readiness)**: Independent — no dependency on other stories
- **US4 (Kindle Validation)**: Independent — no dependency on other stories
- **US1 (Test Delivery)**: Soft dependency on US3 (reuses SmtpReadinessService patterns); can be implemented in parallel if both reference Phase 2 service
- **US2 (Delivery History)**: Independent — no dependency on other stories

### Within Each User Story

- Tests MUST be written and FAIL before implementation
- Service/data layer before endpoints
- Endpoints before CLI commands
- Core implementation before integration

### Parallel Opportunities

- **Phase 1**: T001, T002, T003, T005 can all run in parallel (independent new files)
- **Phase 2**: T008 and T009 can run in parallel (different files)
- **Phase 3**: T010 and T011 can run in parallel (different test files)
- **Phase 4**: T014 can start as soon as Phase 2 completes
- **Phase 5**: T015 can start as soon as Phase 3 completes
- **Phase 6**: T019 can start as soon as Phase 2 completes (independent of US1/US3/US4)
- **US3 (Phase 3) and US4 (Phase 4)** can proceed in parallel after Phase 2
- **US2 (Phase 6)** can proceed in parallel with US1 (Phase 5) after Phase 2

---

## Parallel Example: Phase 1

```
T001 ──┐
T002 ──┤
T003 ──┼── All parallel (independent new files)
T005 ──┘
         ↓
T004 ── (depends on StatusResponse existing, but file already exists)
T006 ── (depends on T005 interface)
T007 ── (depends on T006 implementation)
```

## Parallel Example: User Stories after Phase 2

```
Phase 2 complete
    ├── US3 (Phase 3): T010 ∥ T011 → T012 → T013
    ├── US4 (Phase 4): T014 (independent)
    ├── US2 (Phase 6): T019 → T020 → T021 → T022
    └── US1 (Phase 5): T015 → T016 → T017 → T018
```

---

## Implementation Strategy

**MVP Scope**: Phase 1 + Phase 2 + Phase 3 (SMTP Readiness) + Phase 5 (Test Delivery) — users can verify their email pipeline works.

**Incremental Delivery**:
1. Phases 1–2: Foundation (all DTOs, services, data access)
2. Phase 3: SMTP readiness visible in status
3. Phase 4: Kindle email validation tightened
4. Phase 5: Test delivery end-to-end
5. Phase 6: Delivery history browsing
6. Phase 7: Polish

**Total tasks**: 25
- Phase 1 (Setup): 7 tasks
- Phase 2 (Foundational): 2 tasks
- Phase 3 (US3 — SMTP Readiness): 4 tasks
- Phase 4 (US4 — Kindle Validation): 1 task
- Phase 5 (US1 — Test Delivery): 4 tasks
- Phase 6 (US2 — Delivery History): 4 tasks
- Phase 7 (Polish): 3 tasks
