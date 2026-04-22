# Tasks: REST API & Storage Layer

**Input**: Design documents from `/specs/004-rest-api-storage/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/api.md, quickstart.md

**Tests**: Included — TDD approach per project conventions (API endpoints require integration tests).

**Organization**: Tasks grouped by user story (5 stories from spec.md). Each story phase is independently testable after foundational phase completes.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story this task belongs to (US1–US5)
- All file paths are relative to repository root

---

## Phase 1: Setup

**Purpose**: Add new NuGet dependencies and move domain models to the Server project

- [X] T000 Move domain model files from src/SunnySunday.Core/Models/ to src/SunnySunday.Server/Models/: Author.cs, Book.cs, Highlight.cs, Settings.cs, User.cs. Update their namespace from `SunnySunday.Core.Models` to `SunnySunday.Server.Models`. Remove the Models/ folder from SunnySunday.Core. Update all `using SunnySunday.Core.Models` references in SunnySunday.Server and SunnySunday.Tests to `using SunnySunday.Server.Models`. SunnySunday.Cli does not reference domain models, so no changes are needed there. Verify solution builds with `dotnet build src/SunnySunday.slnx`.
- [X] T001 [P] Add Dapper NuGet package to src/SunnySunday.Server/SunnySunday.Server.csproj (`dotnet add package Dapper`)
- [X] T002 [P] Add Microsoft.AspNetCore.Mvc.Testing NuGet package to src/SunnySunday.Tests/SunnySunday.Tests.csproj (`dotnet add package Microsoft.AspNetCore.Mvc.Testing`)
- [X] T002b [P] Add Swashbuckle.AspNetCore NuGet package to src/SunnySunday.Server/SunnySunday.Server.csproj (`dotnet add package Swashbuckle.AspNetCore`). Then register in src/SunnySunday.Server/Program.cs: add `builder.Services.AddEndpointsApiExplorer()` and `builder.Services.AddSwaggerGen()` before `builder.Build()`. After `app.Build()`, add: `if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }`. The Swagger UI will be accessible at `/swagger` in Development; the OpenAPI JSON spec at `/swagger/v1/swagger.json`. Do not enable in Production.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared infrastructure that ALL user stories depend on — DI wiring, user auto-creation, and test harness

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T003 Register scoped `IDbConnection` factory using the built-in ASP.NET `IServiceCollection` (no third-party DI container): `builder.Services.AddScoped<IDbConnection>(_ => new SqliteConnection(connectionString))` where `connectionString` is derived from the same `dbPath` variable already used by `SchemaBootstrap`. Also configure JSON serialization (camelCase property naming) in src/SunnySunday.Server/Program.cs. Connection must be opened before use (Dapper requires an open connection).
- [X] T004 [P] Implement `UserRepository` in src/SunnySunday.Server/Data/UserRepository.cs. Constructor takes `IDbConnection`. Single method `EnsureUserAsync()`: query `SELECT id FROM users WHERE id = 1`; if no row, insert user with `id = 1`, `kindle_email = ''`, `created_at = UTC now`; return user ID (always 1). Uses Dapper `QuerySingleOrDefaultAsync<int?>` and `ExecuteAsync`.
- [X] T005 [P] Implement `TestWebApplicationFactory` in src/SunnySunday.Tests/Api/TestWebApplicationFactory.cs. Extend `WebApplicationFactory<Program>`. Override `ConfigureWebHost` to replace `IDbConnection` registration with a shared in-memory SQLite connection (`DataSource=:memory:`). The shared connection must stay open for the lifetime of the factory (in-memory SQLite DBs are per-connection). Apply schema via `SchemaBootstrap.ApplyAsync` using the in-memory connection's data source. Expose a method to get a fresh `HttpClient` for tests.
- [X] T006 Register `UserRepository` in DI (`AddScoped<UserRepository>`) and verify solution builds with `dotnet build src/SunnySunday.slnx` in src/SunnySunday.Server/Program.cs

**Checkpoint**: Foundation ready — DI, user auto-creation, and test factory in place. User story implementation can begin.

---

## Phase 3: User Story 1 — Bulk Import Highlights (Priority: P1) 🎯 MVP

**Goal**: CLI sends parsed highlights to `POST /sync`; server persists authors, books, and highlights with deduplication and returns an import summary.

**Independent Test**: Send a well-formed sync payload to the server and verify highlights, books, and authors are persisted; re-send the same payload and verify deduplication.

### Implementation for User Story 1

- [X] T007 [P] [US1] Create `SyncRequest`, `SyncBookRequest`, and `SyncHighlightRequest` DTOs in src/SunnySunday.Core/Contracts/SyncRequest.cs. `SyncRequest` has `List<SyncBookRequest> Books` (required, non-null). `SyncBookRequest` has `string Title` (required, non-empty), `string? Author` (nullable), `List<SyncHighlightRequest> Highlights` (required, non-empty). `SyncHighlightRequest` has `string Text` (required, non-empty, non-whitespace), `DateTimeOffset? AddedOn` (nullable). All classes in namespace `SunnySunday.Core.Contracts`.
- [X] T008 [P] [US1] Create `SyncResponse` DTO in src/SunnySunday.Core/Contracts/SyncResponse.cs. Properties: `int NewHighlights`, `int DuplicateHighlights`, `int NewBooks`, `int NewAuthors`. Namespace `SunnySunday.Core.Contracts`.
- [X] T009 [US1] Implement `SyncRepository` in src/SunnySunday.Server/Data/SyncRepository.cs. Constructor takes `IDbConnection`. Method `ImportAsync(int userId, SyncRequest request)` returns `SyncResponse`. Logic: open a transaction; for each book in request — find-or-create author by name (null/blank → "Unknown Author"), find-or-create book by (user_id, author_id, title), for each highlight — check existence via `SELECT 1 FROM highlights WHERE user_id=@uid AND book_id=@bid AND text=@text`, skip if exists (count as duplicate), otherwise INSERT with `weight=3, excluded=0, delivery_count=0, created_at=AddedOn ?? UTC now`; commit transaction; return summary counts. Use Dapper `QuerySingleOrDefaultAsync`, `ExecuteScalarAsync<int>` (for last_insert_rowid()), and `ExecuteAsync`.
- [X] T010 [US1] Implement `SyncEndpoints` in src/SunnySunday.Server/Endpoints/SyncEndpoints.cs. Static class with extension method `MapSyncEndpoints(this RouteGroupBuilder group)` or `MapSyncEndpoints(this WebApplication app)`. Define `POST /sync`: deserialize `SyncRequest` body; validate — `Books` must not be null; each book must have non-empty `Title` and at least one highlight; each highlight must have non-empty, non-whitespace `Text`; collect validation errors into `Dictionary<string, string[]>` with indexed field paths (e.g., `books[0].highlights[0].text`); if errors, return `Results.ValidationProblem(errors)`; otherwise call `UserRepository.EnsureUserAsync()`, then `SyncRepository.ImportAsync(userId, request)`, return `Results.Ok(response)`.
- [X] T011 [US1] Register `SyncRepository` in DI (`AddScoped`) and wire `SyncEndpoints` MapGroup in src/SunnySunday.Server/Program.cs. Add `app.MapSyncEndpoints()` after existing `app.MapGet("/", ...)`.
- [X] T012 [US1] Implement `SyncEndpointTests` in src/SunnySunday.Tests/Api/SyncEndpointTests.cs. Use `TestWebApplicationFactory` and `HttpClient`. Test scenarios from spec: (1) fresh import of 50 highlights across 5 books → 200 OK with newHighlights=50, duplicateHighlights=0, newBooks=5; (2) re-import same payload → newHighlights=0, duplicateHighlights=50; (3) import book with existing author → author not duplicated; (4) empty books list → 200 OK with all zeros; (5) highlight with blank text → 422 with validation error on `books[0].highlights[0].text`; (6) null author → stored as "Unknown Author".

**Checkpoint**: User Story 1 complete — bulk import with dedup is functional and tested.

---

## Phase 4: User Story 2 — Read and Update Settings (Priority: P1)

**Goal**: CLI reads default settings and updates Kindle email, schedule, delivery time, and count via REST endpoints.

**Independent Test**: Read settings (get defaults), update individual fields, read again to confirm changes persisted.

### Implementation for User Story 2

- [X] T013 [P] [US2] Create `SettingsResponse` DTO in src/SunnySunday.Core/Contracts/SettingsResponse.cs. Properties: `string Schedule`, `string? DeliveryDay`, `string DeliveryTime`, `int Count`, `string KindleEmail`. Namespace `SunnySunday.Core.Contracts`.
- [X] T014 [P] [US2] Create `UpdateSettingsRequest` DTO in src/SunnySunday.Core/Contracts/UpdateSettingsRequest.cs. All properties nullable (partial update): `string? Schedule`, `string? DeliveryDay`, `string? DeliveryTime`, `int? Count`, `string? KindleEmail`. Namespace `SunnySunday.Core.Contracts`.
- [X] T015 [US2] Implement `SettingsRepository` in src/SunnySunday.Server/Data/SettingsRepository.cs. Constructor takes `IDbConnection`. Repository uses only domain models: `GetByUserIdAsync(int userId)` returns `Settings` (or default domain values when no row exists) and `UpsertAsync(Settings settings)` persists the effective settings row. Kindle email remains in `UserRepository`.
- [X] T016 [US2] Implement `SettingsEndpoints` in src/SunnySunday.Server/Endpoints/SettingsEndpoints.cs. `GET /settings`: call `UserRepository.EnsureUserAsync()`, read `User` and `Settings` domain models, then map them to `SettingsResponse`. `PUT /settings`: deserialize `UpdateSettingsRequest`; validate — if `Schedule` provided, must be "daily" or "weekly"; if `DeliveryTime` provided, must match `HH:mm` format (00:00–23:59); if `Count` provided, must be 1–15; if `KindleEmail` provided, must be valid email format; apply the validated partial update onto the domain models, persist via repositories, then map the result to `SettingsResponse`.
- [X] T017 [US2] Register `SettingsRepository` in DI (`AddScoped`) and wire `SettingsEndpoints` MapGroup in src/SunnySunday.Server/Program.cs.
- [X] T018 [US2] Implement `SettingsEndpointTests` in src/SunnySunday.Tests/Api/SettingsEndpointTests.cs. Test scenarios from spec: (1) GET default settings → schedule="daily", deliveryTime="18:00", count=3, kindleEmail=""; (2) PUT schedule="weekly" → subsequent GET shows schedule="weekly", other fields unchanged; (3) PUT count=0 → 422 validation error on `count`; (4) PUT count=16 → 422 validation error on `count`; (5) PUT kindleEmail="user@kindle.com" → saved and returned on GET; (6) PUT kindleEmail="invalid" → 422 validation error on `kindleEmail`.

**Checkpoint**: User Story 2 complete — settings CRUD with validation is functional and tested.

---

## Phase 5: User Story 3 — View Server Status (Priority: P2)

**Goal**: CLI queries server for aggregate counts (highlights, books, authors, exclusions) and next recap info.

**Independent Test**: Seed data via `POST /sync`, then `GET /status` and verify counts match.

### Implementation for User Story 3

- [X] T019 [P] [US3] Create `StatusResponse` DTO in src/SunnySunday.Core/Contracts/StatusResponse.cs. Properties: `int TotalHighlights`, `int TotalBooks`, `int TotalAuthors`, `int ExcludedHighlights`, `int ExcludedBooks`, `int ExcludedAuthors`, `string? NextRecap`. Namespace `SunnySunday.Core.Contracts`.
- [X] T020 [US3] Implement `StatusRepository` in src/SunnySunday.Server/Data/StatusRepository.cs. Constructor takes `IDbConnection`. Method `GetStatusAsync(int userId)` returns `StatusResponse`: run aggregate queries — `SELECT COUNT(*) FROM highlights WHERE user_id=@uid`, `SELECT COUNT(*) FROM books WHERE user_id=@uid`, `SELECT COUNT(DISTINCT author_id) FROM books WHERE user_id=@uid`, `SELECT COUNT(*) FROM highlights WHERE user_id=@uid AND excluded=1`, `SELECT COUNT(*) FROM excluded_books WHERE user_id=@uid`, `SELECT COUNT(*) FROM excluded_authors WHERE user_id=@uid`; for `NextRecap`, return null (placeholder — exact scheduling logic deferred to recap generation feature). Use Dapper `QuerySingleAsync<int>` for each count.
- [X] T021 [US3] Implement `StatusEndpoints` in src/SunnySunday.Server/Endpoints/StatusEndpoints.cs. `GET /status`: call `UserRepository.EnsureUserAsync()`, then `StatusRepository.GetStatusAsync(userId)`, return `Results.Ok(response)`. No validation needed (read-only, no parameters).
- [X] T022 [US3] Register `StatusRepository` in DI (`AddScoped`) and wire `StatusEndpoints` MapGroup in src/SunnySunday.Server/Program.cs.
- [X] T023 [US3] Implement `StatusEndpointTests` in src/SunnySunday.Tests/Api/StatusEndpointTests.cs. Test scenarios from spec: (1) seed 100 highlights across 10 books by 5 authors via `/sync`, GET /status → totalHighlights=100, totalBooks=10, totalAuthors=5, excludedHighlights=0; (2) empty database → all counts zero, nextRecap=null.

**Checkpoint**: User Story 3 complete — status endpoint returns accurate aggregate data.

---

## Phase 6: User Story 4 — Manage Exclusions (Priority: P2)

**Goal**: User excludes/re-includes highlights, books, or authors from recaps and views all current exclusions.

**Independent Test**: Import data via `/sync`, exclude items, verify exclusion list, re-include, verify removal.

### Implementation for User Story 4

- [X] T024 [P] [US4] Create `ExclusionsResponse`, `ExcludedHighlightDto`, `ExcludedBookDto`, and `ExcludedAuthorDto` DTOs in src/SunnySunday.Core/Contracts/ExclusionsResponse.cs. `ExclusionsResponse` has `List<ExcludedHighlightDto> Highlights`, `List<ExcludedBookDto> Books`, `List<ExcludedAuthorDto> Authors`. `ExcludedHighlightDto` has `int Id`, `string Text` (truncated to 100 chars), `string BookTitle`. `ExcludedBookDto` has `int Id`, `string Title`, `string AuthorName`, `int HighlightCount`. `ExcludedAuthorDto` has `int Id`, `string Name`, `int BookCount`. Namespace `SunnySunday.Core.Contracts`.
- [X] T025 [US4] Implement `ExclusionRepository` in src/SunnySunday.Server/Data/ExclusionRepository.cs. Constructor takes `IDbConnection`. Methods: `ExcludeHighlightAsync(int userId, int highlightId)` — verify highlight exists and belongs to user (404 if not), set `highlights.excluded = 1`; `IncludeHighlightAsync(int userId, int highlightId)` — verify exists (404 if not), set `excluded = 0`; `ExcludeBookAsync(int userId, int bookId)` — verify book exists (404 if not), INSERT into `excluded_books`; `IncludeBookAsync(int userId, int bookId)` — verify exists (404 if not), DELETE from `excluded_books`; `ExcludeAuthorAsync(int userId, int authorId)` — verify author exists (404 if not), INSERT into `excluded_authors`; `IncludeAuthorAsync(int userId, int authorId)` — verify exists (404 if not), DELETE from `excluded_authors`; `GetExclusionsAsync(int userId)` — query individually excluded highlights (JOIN books for title, SUBSTR text to 100 chars), excluded books (JOIN authors for name, subquery for highlight count), excluded authors (subquery for book count); return `ExclusionsResponse`. Each mutating method returns bool (true=success, false=not found) or throws; endpoints handle 404 response.
- [X] T026 [US4] Implement `ExclusionEndpoints` in src/SunnySunday.Server/Endpoints/ExclusionEndpoints.cs. Six mutation endpoints + one query: `POST /highlights/{id}/exclude` → exclude highlight, 204 or 404; `DELETE /highlights/{id}/exclude` → include highlight, 204 or 404; `POST /books/{id}/exclude` → exclude book, 204 or 404; `DELETE /books/{id}/exclude` → include book, 204 or 404; `POST /authors/{id}/exclude` → exclude author, 204 or 404; `DELETE /authors/{id}/exclude` → include author, 204 or 404; `GET /exclusions` → list all exclusions, 200. All mutation endpoints: call `UserRepository.EnsureUserAsync()`, call repository method, if not found return `Results.Problem(detail: "[Entity] {id} not found.", statusCode: 404)`, otherwise return `Results.NoContent()`.
- [X] T027 [US4] Register `ExclusionRepository` in DI (`AddScoped`) and wire `ExclusionEndpoints` MapGroup in src/SunnySunday.Server/Program.cs.
- [X] T028 [US4] Implement `ExclusionEndpointTests` in src/SunnySunday.Tests/Api/ExclusionEndpointTests.cs. Test scenarios from spec: (1) exclude highlight → 204, GET /exclusions includes it; (2) re-include highlight → 204, GET /exclusions no longer includes it; (3) exclude book → 204, listed in exclusions with highlight count; (4) re-include book → 204, removed from exclusions; (5) exclude author → 204, listed in exclusions with book count; (6) exclude nonexistent highlight → 404 with "Highlight {id} not found."; (7) exclude nonexistent book → 404; (8) GET /exclusions with mixed exclusions → all three categories populated.

**Checkpoint**: User Story 4 complete — full exclusion management is functional and tested.

---

## Phase 7: User Story 5 — Manage Highlight Weights (Priority: P3)

**Goal**: User sets custom weights (1–5) on highlights to influence recap selection frequency.

**Independent Test**: Import data via `/sync`, set weight on a highlight, list weighted highlights.

### Implementation for User Story 5

- [ ] T029 [P] [US5] Create `SetWeightRequest` DTO in src/SunnySunday.Core/Contracts/SetWeightRequest.cs. Property: `int Weight`. Namespace `SunnySunday.Core.Contracts`.
- [ ] T030 [P] [US5] Create `WeightedHighlightDto` DTO in src/SunnySunday.Core/Contracts/WeightedHighlightDto.cs. Properties: `int Id`, `string Text` (truncated to 100 chars), `string BookTitle`, `int Weight`. Namespace `SunnySunday.Core.Contracts`.
- [ ] T031 [US5] Implement `WeightRepository` in src/SunnySunday.Server/Data/WeightRepository.cs. Constructor takes `IDbConnection`. Method `SetWeightAsync(int userId, int highlightId, int weight)`: verify highlight exists and belongs to user (return not-found if not), `UPDATE highlights SET weight=@weight WHERE id=@id AND user_id=@uid`; return success/not-found. Method `GetWeightedHighlightsAsync(int userId)`: `SELECT h.id, SUBSTR(h.text, 1, 100) AS Text, b.title AS BookTitle, h.weight FROM highlights h JOIN books b ON h.book_id = b.id WHERE h.user_id=@uid AND h.weight != 3 ORDER BY h.weight DESC`; return `List<WeightedHighlightDto>`.
- [ ] T032 [US5] Implement `WeightEndpoints` in src/SunnySunday.Server/Endpoints/WeightEndpoints.cs. `PUT /highlights/{id}/weight`: deserialize `SetWeightRequest`; validate `Weight` is 1–5 (if not, return `Results.ValidationProblem` with error on `weight` field); call `UserRepository.EnsureUserAsync()`, then `WeightRepository.SetWeightAsync(userId, id, request.Weight)`; if not found, return `Results.Problem(detail: "Highlight {id} not found.", statusCode: 404)`; otherwise return `Results.NoContent()`. `GET /highlights/weights`: call `UserRepository.EnsureUserAsync()`, then `WeightRepository.GetWeightedHighlightsAsync(userId)`, return `Results.Ok(list)`.
- [ ] T033 [US5] Register `WeightRepository` in DI (`AddScoped`) and wire `WeightEndpoints` MapGroup in src/SunnySunday.Server/Program.cs.
- [ ] T034 [US5] Implement `WeightEndpointTests` in src/SunnySunday.Tests/Api/WeightEndpointTests.cs. Test scenarios from spec: (1) set weight to 5 → 204, GET /highlights/weights includes highlight with weight=5; (2) set weight to 0 → 422 validation error on `weight`; (3) set weight to 6 → 422 validation error on `weight`; (4) set weight on nonexistent highlight → 404; (5) GET /highlights/weights with no custom weights → empty list.

**Checkpoint**: User Story 5 complete — weight management is functional and tested.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Documentation, validation, and final verification

- [ ] T035 [P] Update docs/ARCHITECTURE.md with REST API layer documentation: endpoint groups, data access pattern (Dapper repositories), error handling (ProblemDetails), and project structure additions (Contracts/, Data/, Endpoints/, Tests/Api/)
- [ ] T036 Run quickstart.md validation: build solution, start server, execute all curl examples from specs/004-rest-api-storage/quickstart.md, verify expected responses
- [ ] T037 Verify all tests pass with `dotnet test src/SunnySunday.slnx` and confirm no regressions in existing infrastructure and parser tests

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 (NuGet packages) — **BLOCKS all user stories**
- **US1 — Bulk Import (Phase 3)**: Depends on Phase 2 — no other story dependencies
- **US2 — Settings (Phase 4)**: Depends on Phase 2 — no dependencies on US1, can run in parallel with Phase 3
- **US3 — Status (Phase 5)**: Depends on Phase 2 — independent but benefits from US1 for meaningful test data
- **US4 — Exclusions (Phase 6)**: Depends on Phase 2 — requires US1 data (highlights/books/authors) for meaningful testing; recommended after Phase 3
- **US5 — Weights (Phase 7)**: Depends on Phase 2 — requires US1 data (highlights) for meaningful testing; recommended after Phase 3
- **Polish (Phase 8)**: Depends on all user stories being complete

### User Story Independence

| Story | Can Start After | Parallel With | Notes |
|-------|----------------|---------------|-------|
| US1 (Bulk Import) | Phase 2 | US2 | Foundational — provides data for US3, US4, US5 |
| US2 (Settings) | Phase 2 | US1 | Fully independent — operates on settings/users only |
| US3 (Status) | Phase 2 | US1, US2 | Read-only aggregation — works on empty DB too |
| US4 (Exclusions) | Phase 2 (Phase 3 recommended) | US3, US5 | Needs highlights/books/authors to exclude |
| US5 (Weights) | Phase 2 (Phase 3 recommended) | US3, US4 | Needs highlights to weight |

### Within Each User Story

1. DTOs first (can be parallel within a story)
2. Repository depends on DTOs
3. Endpoints depend on repository
4. DI wiring depends on repository and endpoints
5. Tests depend on everything above plus TestWebApplicationFactory

### Parallel Opportunities

- **Phase 1**: T001 and T002 are parallel (different .csproj files)
- **Phase 2**: T004 and T005 are parallel (different files, both depend on T003)
- **Phase 3–7**: All DTO tasks marked [P] within a phase can run in parallel
- **Cross-phase**: US1 and US2 can be fully developed in parallel after Phase 2
- **Cross-phase**: US3, US4, US5 can be developed in parallel after Phase 3 (recommended)

---

## Parallel Example: User Story 1

```
T007 ──┐
       ├──► T009 ──► T010 ──► T011 ──► T012
T008 ──┘
```

- T007 (SyncRequest DTOs) and T008 (SyncResponse DTO) run in parallel
- T009 (SyncRepository) starts when both DTOs are done
- T010 (SyncEndpoints) depends on T009
- T011 (DI wiring) depends on T009 and T010
- T012 (tests) depends on all above

## Parallel Example: Cross-Story (after Phase 2)

```
Phase 2 ──► US1 (Phase 3) ──┬──► US4 (Phase 6) ──┐
        │                   ├──► US5 (Phase 7) ──├──► Phase 8
        ├──► US2 (Phase 4) ─┘                    │
        └──► US3 (Phase 5) ──────────────────────┘
```

---

## Implementation Strategy

- **MVP**: Phase 1 + Phase 2 + Phase 3 (US1: Bulk Import) — delivers the foundational data ingestion path
- **Core**: Add Phase 4 (US2: Settings) — completes the onboarding flow (sync + configure)
- **Full feature**: Add Phases 5–7 (US3–US5) — complete REST API surface
- **Ship**: Phase 8 — documentation and final validation

**Total tasks**: 37
**Estimated phases**: 8
