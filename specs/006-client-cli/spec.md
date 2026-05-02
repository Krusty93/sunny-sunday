# Feature Specification: 006 Client CLI

**Feature Branch**: `006-client-cli`  
**Created**: 2026-05-02  
**Status**: Draft  
**Input**: User request: "Implement the sunny client CLI binary (Spectre.Console) with all commands: upload highlights, configure server, manage weights, exclusions, and recaps."

## User Scenarios & Testing

### User Story 1 - Sync Highlights to Server (Priority: P1)

A user runs `sunny sync` to parse their Kindle clippings file and upload all highlights to the server in a single command.

**Why this priority**: This is the primary day-to-day action (US-06) — without syncing highlights, no other feature has data to operate on.

**Independent Test**: Run `sunny sync /path/to/My Clippings.txt` against a running server and verify highlights appear in the server database.

**Acceptance Scenarios**:

1. **Given** a valid `My Clippings.txt` file path, **When** the user runs `sunny sync /path/to/file`, **Then** highlights are parsed and POSTed to the server via `/sync`.
2. **Given** a connected Kindle at a known mount path, **When** the user runs `sunny sync` without arguments, **Then** the CLI auto-detects the clippings file and syncs.
3. **Given** no path argument and no Kindle detected, **When** the user runs `sunny sync`, **Then** the CLI prompts the user to provide a file path.
4. **Given** successful sync, **When** the server responds with sync stats, **Then** the CLI displays a summary (new highlights added, duplicates skipped, books found).
5. **Given** the server is unreachable, **When** the user runs `sunny sync`, **Then** the CLI displays an actionable error message with the server URL attempted.

---

### User Story 2 - Configure Schedule (Priority: P1)

A user configures when recap emails are delivered using `sunny config schedule`.

**Why this priority**: Schedule configuration is required for the core value loop — unattended recap delivery.

**Independent Test**: Run `sunny config schedule daily 08:00` and verify the server settings reflect the new schedule and timezone.

**Acceptance Scenarios**:

1. **Given** valid cadence and time, **When** the user runs `sunny config schedule daily 18:00`, **Then** the CLI sends PUT /settings with cadence, time, and detected timezone.
2. **Given** the user wants to see current schedule, **When** the user runs `sunny config schedule show`, **Then** the CLI fetches and displays the current schedule in local time.
3. **Given** an invalid time format, **When** the user enters `sunny config schedule daily 25:00`, **Then** the CLI shows a validation error without contacting the server.

---

### User Story 3 - Configure Highlight Count (Priority: P2)

A user configures how many highlights appear in each recap.

**Why this priority**: Personalization of recap size is important but has a sensible default (5).

**Independent Test**: Run `sunny config count 10` and verify the server settings are updated.

**Acceptance Scenarios**:

1. **Given** a valid count (1–15), **When** the user runs `sunny config count 10`, **Then** the CLI sends PUT /settings with the new count.
2. **Given** the user wants to see current count, **When** the user runs `sunny config count show`, **Then** the CLI displays the current highlight count.
3. **Given** an out-of-range count, **When** the user runs `sunny config count 0` or `sunny config count 20`, **Then** the CLI shows a validation error.

---

### User Story 4 - View All Settings (Priority: P2)

A user views all current configuration in one place using `sunny config show`.

**Why this priority**: Users need a quick overview without running multiple commands.

**Independent Test**: Run `sunny config show` and verify a table displays schedule, count, and Kindle email.

**Acceptance Scenarios**:

1. **Given** a configured server, **When** the user runs `sunny config show`, **Then** the CLI displays a table with all current settings (schedule cadence, time, count, Kindle email).
2. **Given** server timestamps are UTC, **When** settings are displayed, **Then** times are converted to the user's local timezone.

---

### User Story 5 - Manage Exclusions (Priority: P2)

A user excludes specific highlights, books, or authors from future recaps.

**Why this priority**: Exclusions allow curating recap quality, but the system works without them.

**Independent Test**: Run `sunny exclude book "Some Book"` and verify the book no longer appears in recap selection.

**Acceptance Scenarios**:

1. **Given** a valid highlight/book/author identifier, **When** the user runs `sunny exclude highlight|book|author <id|name>`, **Then** the CLI sends POST to the appropriate exclusion endpoint.
2. **Given** an existing exclusion, **When** the user runs `sunny exclude remove highlight|book|author <id|name>`, **Then** the CLI sends DELETE to remove the exclusion.
3. **Given** existing exclusions, **When** the user runs `sunny exclude list`, **Then** the CLI displays all exclusions in a formatted table grouped by type.
4. **Given** an invalid identifier (not found on server), **When** the user runs an exclude command, **Then** the CLI displays the server's error message clearly.

---

### User Story 6 - Manage Weights (Priority: P3)

A user adjusts highlight weights to influence selection frequency.

**Why this priority**: Weight tuning is advanced functionality; default weights work for most users.

**Independent Test**: Run `sunny weight set <id> 5` and verify the weight is updated on the server.

**Acceptance Scenarios**:

1. **Given** a valid highlight ID and weight value, **When** the user runs `sunny weight set <id> <value>`, **Then** the CLI sends PUT /highlights/{id}/weight with the new value.
2. **Given** highlights with custom weights, **When** the user runs `sunny weight list`, **Then** the CLI displays a table of highlights with their current weights.
3. **Given** an invalid weight value, **When** the user provides a non-numeric or out-of-range weight, **Then** the CLI shows a validation error.

---

### User Story 7 - View Server Status (Priority: P2)

A user checks the health and state of the connected server with `sunny status`.

**Why this priority**: Users need to verify connectivity and see at-a-glance system state.

**Independent Test**: Run `sunny status` against a running server and verify the output shows total highlights, last sync, next recap, and server version.

**Acceptance Scenarios**:

1. **Given** a reachable server, **When** the user runs `sunny status`, **Then** the CLI displays total highlights, last sync time, next scheduled recap, delivery status, and server version.
2. **Given** server timestamps are UTC, **When** status is displayed, **Then** times are converted to local timezone.
3. **Given** an unreachable server, **When** the user runs `sunny status`, **Then** the CLI displays a clear connection error with the URL attempted and suggests checking SUNNY_SERVER.

---

### User Story 8 - Server URL Configuration (Priority: P1)

A user sets the server URL via the `SUNNY_SERVER` environment variable. The CLI resolves where to connect on every invocation.

**Why this priority**: Without server connectivity, no CLI command can function.

**Independent Test**: Set `SUNNY_SERVER=http://localhost:5000` and run any command; verify it connects to that address.

**Acceptance Scenarios**:

1. **Given** `SUNNY_SERVER` is set, **When** any command runs, **Then** the CLI uses that URL as the base address for all API calls.
2. **Given** `SUNNY_SERVER` is not set, **When** any command runs, **Then** the CLI displays an error explaining how to set the variable.
3. **Given** `SUNNY_SERVER` is set to a malformed URL, **When** any command runs, **Then** the CLI displays a validation error.

---

### Edge Cases

- **Empty clippings file**: `sunny sync` with a valid but empty file displays "No highlights found" and exits cleanly.
- **Partial server failure**: Server returns 500 on sync — CLI retries per Polly policy and reports failure after exhaustion.
- **Concurrent syncs**: User runs sync twice in rapid succession — server handles deduplication, CLI reports stats from server response.
- **Kindle auto-detect on unsupported OS**: Falls back to prompting the user for a path.
- **Very large clippings file**: CLI streams parsing without loading entire file into memory; reports progress.
- **Server returns validation errors (400)**: CLI parses error response and displays specific field-level messages.
- **Network timeout**: Polly retry handles 408/timeout; user sees "Server did not respond in time" after retries exhausted.
- **Invalid timezone detection**: If system timezone cannot be detected, CLI warns and uses UTC as fallback.

## Requirements

### Functional Requirements

- **FR-006-01**: CLI MUST use Spectre.Console.Cli CommandApp with a typed command tree structure.
- **FR-006-02**: CLI MUST resolve server URL from the `SUNNY_SERVER` environment variable on every invocation.
- **FR-006-03**: CLI MUST display an actionable error if `SUNNY_SERVER` is not set or is malformed.
- **FR-006-04**: CLI MUST parse `My Clippings.txt` using the existing ClippingsParser and POST results to `/sync`.
- **FR-006-05**: CLI MUST auto-detect Kindle mount paths on macOS (`/Volumes/Kindle/`), Linux (`/media/*/Kindle/`), and Windows (`D:\`, `E:\` with `documents/My Clippings.txt`).
- **FR-006-06**: CLI MUST prompt the user for a file path when no argument is given and auto-detect fails.
- **FR-006-07**: CLI MUST display sync results as a rich summary (new, duplicates, books).
- **FR-006-08**: CLI MUST send schedule updates with cadence, time, and auto-detected system timezone (TimeZoneInfo.Local.Id).
- **FR-006-09**: CLI MUST validate schedule time format (HH:MM, 00:00–23:59) locally before sending to the server.
- **FR-006-10**: CLI MUST validate highlight count range (1–15) locally before sending to the server.
- **FR-006-11**: CLI MUST convert UTC timestamps from server responses to local time for display.
- **FR-006-12**: CLI MUST use `sunny config show` to display all current settings in a unified table.
- **FR-006-13**: CLI MUST support exclude/remove operations for highlights, books, and authors by ID or name.
- **FR-006-14**: CLI MUST display exclusions in a formatted table grouped by type (highlights, books, authors).
- **FR-006-15**: CLI MUST support `sunny weight set <id> <value>` to update highlight weights.
- **FR-006-16**: CLI MUST support `sunny weight list` to display highlights with their weights.
- **FR-006-17**: CLI MUST use `sunny status` to display server health, totals, last sync, next recap, and version.
- **FR-006-18**: CLI MUST use a typed HttpClient with Polly retry policy for transient HTTP errors (408, 429, 5xx).
- **FR-006-19**: CLI MUST retry up to 3 attempts with exponential backoff on transient failures.
- **FR-006-20**: CLI MUST display clear, actionable error messages for connection failures, server errors, and validation errors.
- **FR-006-21**: CLI MUST use Microsoft.Extensions.DependencyInjection with generic host for DI, logging, and configuration.
- **FR-006-22**: CLI MUST produce rich output using Spectre.Console (tables, panels, markup colors).
- **FR-006-23**: CLI MUST exit with non-zero exit code on failure and zero on success.

### Key Entities

- **CommandTree**: Hierarchical structure mapping CLI verbs (sync, config, exclude, weight, status) to Spectre.Console command classes.
- **SunnyHttpClient**: Typed HTTP client wrapping all REST API calls with retry policy and base URL resolution.
- **KindleDetector**: Component responsible for locating Kindle mount paths across supported operating systems.
- **CommandResult**: Unified result type carrying success/failure state and display data for Spectre output rendering.

## Success Criteria

### Measurable Outcomes

- **SC-006-01**: Users can sync highlights from a Kindle clippings file in a single command (`sunny sync`) with zero manual configuration beyond `SUNNY_SERVER`.
- **SC-006-02**: All CLI commands complete within 5 seconds under normal network conditions (excluding file parsing of very large files).
- **SC-006-03**: 100% of server error responses produce user-readable, actionable CLI output (no raw HTTP codes or stack traces).
- **SC-006-04**: CLI automatically retries transient failures and succeeds without user intervention when the server recovers within 3 attempts.
- **SC-006-05**: All timestamp displays use the user's local timezone regardless of server storage format.
- **SC-006-06**: Users can manage all settings, exclusions, and weights without editing any files (NFR-03 compliance).
- **SC-006-07**: CLI exits with appropriate exit codes (0 for success, non-zero for failure) enabling scripting and automation.

## Assumptions

- The REST API surface from feature 004 is fully implemented and available at the server URL.
- Scheduler and recap engine from feature 005 is deployed on the server (for status and schedule-related responses).
- The existing ClippingsParser in `SunnySunday.Cli/Parsing/` is correct and complete for MVP clippings format.
- Single-user architecture (user id 1) — no user switching or multi-tenant concerns.
- `SUNNY_SERVER` includes the scheme and port (e.g., `http://localhost:5000`) — no default port inference.
- Kindle auto-detect covers the three major OS families; other platforms fall back to manual path input.
- Polly is already available as a dependency in the CLI project.
- Spectre.Console.Cli is already available as a dependency in the CLI project.
