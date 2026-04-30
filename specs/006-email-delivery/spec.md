# Feature Specification: 006 Email Delivery Management

**Feature Branch**: `006-email-delivery`  
**Created**: 2026-04-30  
**Status**: Draft  
**Input**: User request: "Implement the email delivery management layer — configuration validation, test delivery, delivery history/logs, and CLI commands for email operations."

## User Scenarios & Testing

### User Story 1 - Test Delivery to Kindle (Priority: P1)

A user wants to verify their SMTP and Kindle email configuration actually works before waiting for the next scheduled recap. They run a test delivery command from the CLI, which sends a small test EPUB to their Kindle, confirming the entire email pipeline is functional end-to-end.

**Why this priority**: Users need confidence that their configuration is correct before relying on automated recaps. Without a test mechanism, they must wait for a scheduled delivery and hope it works — a poor onboarding experience (US-02, US-08).

**Independent Test**: Configure valid SMTP + Kindle email, run `sunny delivery test`, verify a test EPUB arrives at the Kindle email address and the CLI displays a success confirmation.

**Acceptance Scenarios**:

1. **Given** valid SMTP settings and a configured Kindle email, **When** the user runs `sunny delivery test`, **Then** a small test EPUB is sent to the Kindle email and the CLI displays a success confirmation.
2. **Given** valid SMTP settings and a configured Kindle email, **When** the user calls `POST /test-delivery`, **Then** the response includes a success indicator and no recap history or highlight state is modified.
3. **Given** invalid SMTP credentials, **When** the user runs `sunny delivery test`, **Then** the CLI displays an actionable error message explaining authentication failed and what to check.
4. **Given** an unreachable SMTP host, **When** the user runs `sunny delivery test`, **Then** the CLI displays an actionable error explaining the host could not be reached.
5. **Given** no Kindle email configured, **When** the user runs `sunny delivery test`, **Then** the CLI displays an error indicating the Kindle email must be set first.

---

### User Story 2 - Delivery History Visibility (Priority: P2)

A user wants to review past delivery attempts to understand whether recaps are being sent reliably. They run a CLI command that shows a paginated table of delivery history including date, status, attempt count, and any error messages.

**Why this priority**: Visibility into delivery reliability is essential for trust in an automated system, but the system still functions without it — users receive recaps regardless of whether they check the log.

**Independent Test**: Seed multiple `recap_jobs` with various statuses (delivered, failed, pending), run `sunny delivery log`, verify the table renders correctly with pagination.

**Acceptance Scenarios**:

1. **Given** past recap jobs exist, **When** the user runs `sunny delivery log`, **Then** a table is displayed showing scheduled time, status, attempt count, error message, and delivery time for each job.
2. **Given** more jobs exist than the default page size, **When** the user runs `sunny delivery log --page 2`, **Then** the next page of results is shown.
3. **Given** no recap jobs exist, **When** the user runs `sunny delivery log`, **Then** a message indicates no deliveries have been recorded yet.
4. **Given** past recap jobs exist, **When** a client calls `GET /deliveries?offset=0&limit=10`, **Then** the response contains a paginated list of delivery records with total count.

---

### User Story 3 - SMTP Configuration Readiness Check (Priority: P2)

A user wants to know at a glance whether SMTP delivery is properly configured without inspecting environment variables or Docker compose files. They run a CLI command or check server status to see if the SMTP pipeline is ready.

**Why this priority**: Quick feedback on configuration state reduces troubleshooting time, but it doesn't block core functionality — users can also discover issues via test delivery.

**Independent Test**: Start the server with and without SMTP settings, verify `GET /status` reflects SMTP readiness, and verify `sunny delivery status` displays the configuration state.

**Acceptance Scenarios**:

1. **Given** all required SMTP settings are configured, **When** the user runs `sunny delivery status`, **Then** the CLI shows SMTP as "ready" and displays the from-address (but not credentials).
2. **Given** SMTP settings are incomplete (e.g., missing host), **When** the user runs `sunny delivery status`, **Then** the CLI shows SMTP as "not configured" with a message indicating which settings are missing.
3. **Given** all SMTP settings are configured, **When** `GET /status` is called, **Then** the response includes `smtpReady: true`.
4. **Given** SMTP settings are missing, **When** the server starts, **Then** a warning is logged indicating SMTP is not configured.

---

### User Story 4 - Kindle Email Validation (Priority: P3)

When a user updates their Kindle email address via settings, the system validates that it follows the Amazon Kindle email format before accepting it.

**Why this priority**: Prevents misconfiguration that would cause silent delivery failures, but is a safeguard rather than a primary workflow.

**Independent Test**: Call `PUT /settings` with various email formats and verify only valid Kindle addresses are accepted.

**Acceptance Scenarios**:

1. **Given** a user submits `user@kindle.com` via `PUT /settings`, **When** the request is processed, **Then** the email is accepted and saved.
2. **Given** a user submits `user@free.kindle.com` via `PUT /settings`, **When** the request is processed, **Then** the email is accepted and saved.
3. **Given** a user submits `user@gmail.com` via `PUT /settings`, **When** the request is processed, **Then** the request is rejected with a validation error explaining only `@kindle.com` or `@free.kindle.com` addresses are accepted.
4. **Given** a user submits an empty Kindle email, **When** the request is processed, **Then** the request is rejected with a validation error.

## Edge Cases

- Test delivery when SMTP is not configured: return clear error, do not attempt connection.
- Test delivery when Kindle email is not set: return clear error before attempting send.
- Delivery log with zero records: display friendly "no deliveries yet" message, not an empty table.
- Delivery log pagination beyond available records: return empty page with correct total count.
- SMTP settings partially configured (e.g., host present but password missing): report specifically which fields are missing.
- Kindle email with uppercase characters or extra whitespace: normalize before validation.
- Test delivery timeout (SMTP server does not respond): return actionable error with reasonable timeout behavior.
- Concurrent test delivery requests: each request runs independently without interference.

## Requirements

### Functional Requirements

- **FR-006-01**: System MUST validate Kindle email format on `PUT /settings` — only addresses ending in `@kindle.com` or `@free.kindle.com` are accepted.
- **FR-006-02**: System MUST return a `422` validation error with an actionable message when an invalid Kindle email format is submitted.
- **FR-006-03**: System MUST normalize Kindle email input (trim whitespace, lowercase) before validation and storage.
- **FR-006-04**: System MUST check SMTP settings completeness at server startup and log a warning if any required field is missing.
- **FR-006-05**: System MUST expose SMTP readiness as a boolean field (`smtpReady`) in the `GET /status` response.
- **FR-006-06**: System MUST provide a `POST /test-delivery` endpoint that sends a small test EPUB to the configured Kindle email address.
- **FR-006-07**: Test delivery MUST NOT modify recap history, highlight selection state, `last_seen`, or `delivery_count`.
- **FR-006-08**: Test delivery MUST return a success/failure indicator with actionable error messages on failure.
- **FR-006-09**: Test delivery MUST return a pre-condition error if SMTP is not configured or Kindle email is not set.
- **FR-006-10**: System MUST provide a `GET /deliveries` endpoint that returns a paginated list of past recap jobs.
- **FR-006-11**: Delivery list MUST support `offset` and `limit` query parameters for pagination and return a `total` count.
- **FR-006-12**: Delivery list MUST include for each record: scheduled time, status, attempt count, error message (if any), and delivery time (if delivered).
- **FR-006-13**: Delivery list MUST NOT expose SMTP credentials or internal infrastructure details in any response.
- **FR-006-14**: CLI `sunny delivery test` command MUST call `POST /test-delivery` and display the result to the user.
- **FR-006-15**: CLI `sunny delivery log` command MUST call `GET /deliveries` and render results as a formatted table with pagination support.
- **FR-006-16**: CLI `sunny delivery status` command MUST display SMTP configuration readiness without revealing credentials (host, port, from-address only).
- **FR-006-17**: All error responses from delivery endpoints MUST be JSON with actionable messages explaining what went wrong and how to fix it.

### Key Entities

- **TestDeliveryResult**: Outcome of a test delivery attempt — success/failure indicator plus error details when applicable.
- **DeliveryRecord**: Read projection of a `recap_jobs` row for the delivery log — scheduled time, status, attempt count, error message, delivered-at timestamp.
- **SmtpReadiness**: Computed status indicating whether all required SMTP fields are present and non-empty.

## Success Criteria

### Measurable Outcomes

- **SC-006-01**: Users can verify their email delivery pipeline works within 60 seconds of completing SMTP configuration.
- **SC-006-02**: Users can identify the root cause of a delivery failure from the error message alone, without inspecting server logs.
- **SC-006-03**: Users can review their complete delivery history via a single CLI command.
- **SC-006-04**: 100% of invalid Kindle email formats are rejected before being stored.
- **SC-006-05**: SMTP configuration status is visible to users without exposing any credential values.
- **SC-006-06**: Delivery history supports browsing through all past deliveries via pagination.

## Assumptions

- The SMTP transport layer (`MailDeliveryService`, `SmtpSettings`, Polly retry policy) from feature 005 is fully implemented and available.
- The `recap_jobs` table already exists with the schema defined in feature 005 (`id`, `user_id`, `scheduled_for`, `status`, `attempt_count`, `error_message`, `created_at`, `delivered_at`).
- The `EpubComposer` from feature 005 can be reused to generate a small test EPUB, or a minimal static test document can be created.
- The `StatusResponse` DTO already exists and can be extended with an SMTP readiness field.
- Single-user MVP constraints apply (user_id = 1, no authentication).
- No new NuGet packages are required — MailKit and Polly are already available.
- Default pagination page size is 20 records when `limit` is not specified by the caller.
- Test delivery uses the same retry policy as regular recap delivery for consistency.
