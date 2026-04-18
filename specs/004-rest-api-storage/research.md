# Research: REST API & Storage Layer

**Feature**: 004-rest-api-storage
**Phase**: 0 — Research
**Date**: 2026-04-18

## Research Tasks & Findings

### 1. Data Access Pattern: Raw SQL vs Dapper vs EF Core

**Decision**: Dapper (micro-ORM) on top of `Microsoft.Data.Sqlite`

**Rationale**:
- Raw `Microsoft.Data.Sqlite` is already a project dependency and works, but requires manual parameter binding, result mapping, and boilerplate for every query.
- Dapper adds a thin extension-method layer (`QueryAsync<T>`, `ExecuteAsync`) that eliminates mapping boilerplate while keeping full SQL control. It's 50 KB, zero-config, and doesn't hide what the SQL does.
- EF Core is overkill for this project: the schema is already hand-managed by `SchemaBootstrap`, migrations are unnecessary for MVP, and the entity models are simple POCOs. EF Core would add ~5 MB of dependencies and a new mental model.
- Dapper aligns with constitution principle VI (Simplicity / YAGNI): it solves the repetitive mapping problem without introducing an ORM layer.

**Alternatives considered**:
- **Raw Microsoft.Data.Sqlite**: viable but verbose; every query needs 10+ lines of manual `SqliteParameter` creation and `SqliteDataReader` column access. High bug surface for typos.
- **EF Core**: too heavy for a single-user SQLite app with 7 tables. Schema is already managed externally.

**Package**: `Dapper` (latest stable, ~2.1.x) — add to `SunnySunday.Server.csproj`.

---

### 2. API Contract Design: DTOs vs Domain Models

**Decision**: Separate request/response DTOs in `SunnySunday.Core/Contracts/`, shared between Server and CLI

**Rationale**:
- Domain models (`SunnySunday.Server.Models`) carry persistence concerns (auto-increment IDs, FK columns, internal state like `DeliveryCount`). Exposing them directly leaks internal schema to the API surface.
- DTOs give explicit control over what the client sends and receives. The sync endpoint needs a request shape that matches `ParseResult` (books + highlights), not the flat relational model.
- Placing DTOs in `SunnySunday.Core` (not `SunnySunday.Server`) allows the CLI to reference the same contract types when building HTTP requests, eliminating duplication. Both `SunnySunday.Server` and `SunnySunday.Cli` already reference `SunnySunday.Core`.
- Settings GET/PUT have different shapes: GET returns all fields; PUT accepts a partial update. DTOs make this explicit.
- Error responses use `ProblemDetails` (RFC 9457, built into ASP.NET).

**Alternatives considered**:
- **Expose domain models directly**: simpler initially but creates tight coupling between DB schema and API contract. Any schema change would be a breaking API change.
- **DTOs in SunnySunday.Server only**: works for MVP but forces the CLI to duplicate or re-define the same types to deserialize responses — unnecessary divergence.

---

### 3. Endpoint Organization: Program.cs vs MapGroup

**Decision**: Use `MapGroup` with extension methods to organize endpoints by domain area

**Rationale**:
- The spec defines 14 endpoints across 5 domain areas (sync, settings, status, exclusions, weights). Putting all of them in `Program.cs` would make it 300+ lines and hard to navigate.
- ASP.NET Minimal API's `MapGroup` creates route groups with shared prefixes and metadata. Each group lives in its own static extension method class.
- This keeps `Program.cs` as a composition root (~30 lines) and moves endpoint logic to focused files.

**Organization**:
```
Server/
├── Program.cs                          # Composition root: DI + app.MapGroup(...)
├── Endpoints/
│   ├── SyncEndpoints.cs               # POST /sync
│   ├── StatusEndpoints.cs             # GET /status
│   ├── SettingsEndpoints.cs           # GET/PUT /settings
│   ├── ExclusionEndpoints.cs          # POST/DELETE /highlights|books|authors/{id}/exclude, GET /exclusions
│   └── WeightEndpoints.cs            # PUT /highlights/{id}/weight, GET /highlights/weights
```

**Alternatives considered**:
- **Everything in Program.cs**: works for <5 endpoints but doesn't scale to 14.
- **Controller-based**: Minimal API is the idiomatic .NET 10 pattern for simple REST services; controllers add overhead without benefit here.

---

### 4. Error Handling: ProblemDetails (RFC 9457)

**Decision**: Use ASP.NET's built-in `ProblemDetails` via `Results.Problem()` and `Results.ValidationProblem()`

**Rationale**:
- ASP.NET ships `TypedResults.Problem()` and `TypedResults.ValidationProblem()` which produce RFC 9457-compliant JSON responses.
- Validation errors return 422 with a `errors` dictionary mapping field names to error messages.
- Not-found errors return 404 with a descriptive `detail` string.
- Server errors return 500 with minimal info (no stack traces in production).
- No custom middleware needed — the built-in support is sufficient for MVP.

**Alternatives considered**:
- **Custom error envelope**: unnecessary when a well-adopted standard exists.
- **Exception-based flow**: violates .NET best practices; use result types or direct `Results.Problem()` returns.

---

### 5. Validation Approach

**Decision**: Manual inline validation with a simple `ValidationErrors` helper

**Rationale**:
- The validation rules are simple and few: email format, schedule enum, time format (HH:mm), count range (1–15), weight range (1–5), non-empty text.
- FluentValidation would add a dependency and ceremony (validator classes, DI registration, pipeline behavior) for ~20 validation checks total.
- A small helper method that accumulates `Dictionary<string, string[]>` errors and returns `Results.ValidationProblem(errors)` is sufficient.
- Constitution principle VI: don't add a library for something achievable in 30 lines.

**Alternatives considered**:
- **FluentValidation**: well-known but overkill for this scope. Revisit if validation complexity grows significantly.
- **Data annotations on DTOs**: limited expressiveness; doesn't work naturally with Minimal API without extra wiring.

---

### 6. Testing Strategy

**Decision**: Integration tests with in-memory SQLite using `WebApplicationFactory<T>`

**Rationale**:
- ASP.NET's `Microsoft.AspNetCore.Mvc.Testing` provides `WebApplicationFactory` which spins up the full HTTP pipeline in-process. Tests call real endpoints via `HttpClient` and assert on JSON responses.
- SQLite supports `:memory:` databases — each test gets a fresh, isolated DB with the schema applied by `SchemaBootstrap`.
- This tests the full stack (routing → validation → SQL → response) without Docker or external processes.
- Unit tests for isolated logic (validation helpers, mapping functions) supplement integration tests.

**Package**: `Microsoft.AspNetCore.Mvc.Testing` (latest for .NET 10) — add to `SunnySunday.Tests.csproj`.

**Alternatives considered**:
- **Testcontainers**: unnecessary overhead for SQLite (which has native in-memory support).
- **Unit tests only**: miss routing and serialization bugs; integration tests provide higher confidence for REST endpoints.
- **File-based SQLite per test**: works but slower than in-memory; no advantage for test isolation.

---

### 7. Database Connection Management

**Decision**: Register `SqliteConnection` factory via built-in ASP.NET DI; open-per-request pattern

**Rationale**:
- Each request gets a fresh `SqliteConnection` opened against the file DB. Dapper extension methods operate on `IDbConnection`.
- Register using the built-in `Microsoft.Extensions.DependencyInjection` container (already present in ASP.NET Minimal API): `builder.Services.AddScoped<IDbConnection>(_ => new SqliteConnection(connectionString))`. No third-party DI container (Autofac, Lamar, etc.) is needed or used.
- For tests, override the registration to point at `:memory:` with a shared connection (SQLite in-memory DBs are per-connection).
- No connection pooling needed — SQLite connections are cheap to open and single-user MVP has negligible concurrency.

---

### 8. Sync Endpoint Deduplication Strategy

**Decision**: Server-side deduplication via `SELECT` before `INSERT`, within a transaction

**Rationale**:
- The CLI parser already deduplicates within a single file, but subsequent syncs of the same file (or overlapping files) need server-side dedup.
- For each highlight in the import payload: check if `(user_id, book_id, text)` exists; skip if so.
- Wrap the entire import in a single SQLite transaction for atomicity and performance (bulk inserts in SQLite are 10-100x faster inside a transaction).
- Return counts of new vs. duplicated items in the response.

**Alternatives considered**:
- **UNIQUE constraint + INSERT OR IGNORE**: would require a composite unique index on `(user_id, book_id, text)` which we want to avoid for MVP (constitution: no indexes for MVP). Also, `text` can be very long, making the index impractical.
- **Client-side dedup only**: insufficient — re-syncing the same file would create duplicates.

---

### 9. Single-User Auto-Creation

**Decision**: Auto-create user with ID 1 and placeholder email on first request if no user exists

**Rationale**:
- MVP is single-user (constitution). The server should work out of the box without a "create user" step.
- On any request that needs `user_id`, check if user ID 1 exists; if not, create with `kindle_email = ""` and `created_at = now`.
- The user sets their Kindle email via `PUT /settings` (which updates `users.kindle_email`).
- This satisfies "zero-config onboarding" — the only required input is the Kindle email, set via CLI after first sync.

**Alternatives considered**:
- **Explicit user creation endpoint**: adds a step to onboarding; violates zero-config principle.
- **Hardcoded user in schema**: fragile; better to create dynamically.

---

### 10. OpenAPI Documentation and Swagger UI

**Decision**: Use `Swashbuckle.AspNetCore` for OpenAPI spec generation and Swagger UI; enabled in `Development` environment only

**Rationale**:
- The server exposes 14 endpoints that need to be discoverable and manually testable during development. `Swashbuckle.AspNetCore` generates the OpenAPI 3.0 JSON spec at `/swagger/v1/swagger.json` and serves an interactive Swagger UI at `/swagger`.
- Restricting Swagger to `Development` is the ASP.NET idiomatic pattern: `if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }`. This prevents exposing API documentation on production deployments.
- `Swashbuckle.AspNetCore` is the de-facto standard for Swagger in ASP.NET — it has first-class Minimal API support (via `AddEndpointsApiExplorer`) and does not require controller attributes.
- .NET 10 ships `Microsoft.AspNetCore.OpenApi` (built-in, no UI) and Scalar UI as an alternative, but Swashbuckle remains the most familiar and battle-tested option with the `/swagger` UI that users expect.

**Setup**:
```csharp
// Program.cs — service registration
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Program.cs — middleware (Development only)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
```

**Packages**:
- `Swashbuckle.AspNetCore` — add to `SunnySunday.Server.csproj`

**Alternatives considered**:
- **Microsoft.AspNetCore.OpenApi + Scalar**: .NET 10 built-in, no NuGet for spec generation. Scalar UI requires a separate package. Swashbuckle has more documentation and community familiarity for the `/swagger` URL convention.
- **No Swagger in production**: intentional — reduces attack surface.
