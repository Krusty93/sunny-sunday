# Feature Specification: Core Infrastructure

**Feature Branch**: `002-core-infrastructure`
**Created**: 2026-04-07
**Status**: Implemented
**Input**: User description: "Self-contained .NET 10 solution scaffold for Sunny Sunday: four projects (Core, Server, Cli, Tests), shared domain models (Highlight, Book, Author, User, Settings), SQLite schema creation on server startup, Serilog logging with file and SQLite sinks. No business logic — infrastructure only."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Solution Builds Cleanly (Priority: P1)

A developer clones the repository and runs `dotnet build`. All four projects compile with zero errors and zero warnings.

**Why this priority**: This is the foundation for all subsequent features. Nothing else can be developed or tested without a clean, compilable solution.

**Independent Test**: `dotnet build` exits 0 on a fresh clone with no errors or warnings.

**Acceptance Scenarios**:

1. **Given** a fresh clone of the repository, **When** `dotnet build` is run, **Then** all four projects compile successfully with exit code 0
2. **Given** the solution compiles, **When** `dotnet test` is run, **Then** the test project runs with no compilation errors (even if no tests exist yet)

---

### User Story 2 - SQLite Schema Initialized on Server Startup (Priority: P2)

When the server starts for the first time against an empty Docker volume, it creates the database file and all required tables.

**Why this priority**: All subsequent server features depend on the database being present and having the correct schema.

**Independent Test**: Start the server against an empty volume, inspect the database schema with `sqlite3 /data/sunny.db .schema` — all tables must be present.

**Acceptance Scenarios**:

1. **Given** a Docker volume with no `sunny.db` file, **When** the server starts, **Then** `sunny.db` is created with all required tables
2. **Given** a `sunny.db` that already has the correct schema, **When** the server restarts, **Then** the schema creation is skipped without error (idempotent)
3. **Given** a `sunny.db` with a partial schema, **When** the server starts, **Then** missing tables are created without affecting existing ones

---

### User Story 3 - Serilog Writes Structured Logs (Priority: P3)

When the server is running, all log entries are written to both a rolling log file and the SQLite `Logs` table.

**Why this priority**: Structured logging is required for debugging delivery issues in a self-hosted, headless environment.

**Independent Test**: Start the server, trigger one HTTP request, verify that a log file exists under `/data/logs/` and that the `Logs` table in `sunny.db` contains at least one entry.

**Acceptance Scenarios**:

1. **Given** the server is running, **When** any operation is executed, **Then** a log entry appears in the daily rolling log file under `/data/logs/sunny-YYYYMMDD.log`
2. **Given** the server is running, **When** any operation is executed, **Then** a log entry appears in the `Logs` table in `sunny.db`
3. **Given** the server encounters an error, **When** the error is logged, **Then** structured fields (timestamp, level, message, exception) are present in both sinks

---

### Edge Cases

- What happens when `/data/` is not writable (volume mount permission error)?
- What happens when `sunny.db` exists but is corrupted?
- What happens when the log file cannot be created (disk full)?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Solution MUST contain exactly four projects: `SunnySunday.Core`, `SunnySunday.Server`, `SunnySunday.Cli`, `SunnySunday.Tests`
- **FR-002**: `SunnySunday.Core` MUST define shared domain models: `Highlight`, `Book`, `Author`, `User`, `Settings`
- **FR-003**: Server MUST create the SQLite schema on startup if it does not exist (idempotent — safe to run on every startup)
- **FR-004**: Server MUST configure Serilog with a rolling file sink (`/data/logs/sunny-.log`, daily rotation) and a SQLite sink (`/data/sunny.db`, `Logs` table)
- **FR-005**: All projects MUST target .NET 10
- **FR-006**: `SunnySunday.Server` and `SunnySunday.Cli` MUST reference `SunnySunday.Core`
- **FR-007**: `SunnySunday.Tests` MUST reference all other projects

### Key Entities

- **Highlight**: A text passage marked by the user on their Kindle. Key fields: id, user_id, book_id, text, weight (1–5), excluded (bool), last_seen (datetime), delivery_count, created_at
- **Book**: A Kindle book. Key fields: id, title, author_id
- **Author**: A book author. Key fields: id, name
- **User**: A Sunny Sunday user. Key fields: id, kindle_email, created_at
- **Settings**: Per-user configuration. Key fields: user_id, schedule (daily/weekly, default: daily), delivery_time (default 18:00), count (1–15, default 3)

### SQLite Schema

Tables required at startup: `users`, `authors`, `books`, `highlights`, `excluded_books`, `excluded_authors`, `settings`, `Logs`

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: `dotnet build` exits 0 with no errors and no warnings
- **SC-002**: Server startup creates `sunny.db` with correct schema in under 5 seconds against an empty volume
- **SC-003**: After server start, at least one log entry is present in both `/data/logs/` and the `Logs` table in `sunny.db`
- **SC-004**: Schema creation is idempotent — restarting the server multiple times produces no errors

## Assumptions

- No business logic (parsers, spaced repetition, email delivery, REST endpoints) is implemented in this feature
- No REST endpoints are defined in this feature — the server starts but serves no routes yet
- The choice of database migration tooling (EF Core Migrations, Fluent Migrator, or plain SQL scripts) is deferred to `/speckit.plan`
- The `SunnySunday.Cli` project scaffolded here contains no commands yet — that is feature 007
- Docker volume is mounted at `/data/` — this is a deployment convention, not enforced by the code
