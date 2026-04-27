# Feature Specification: REST API & Storage Layer

**Feature Branch**: `004-rest-api-storage`  
**Created**: 2026-04-18  
**Status**: Draft  
**Input**: User description: "Implement the server-side REST API and SQLite-backed storage layer for Sunny Sunday"

## User Scenarios & Testing

### User Story 1 — Bulk Import Highlights (Priority: P1)

A user connects their Kindle via USB, runs the CLI sync command, and all parsed highlights are stored on the server. The server creates any authors and books not yet known, deduplicates highlights that were already imported, and returns a summary of what changed.

**Why this priority**: Without importing highlights, no other feature has data to operate on. This is the foundational data-ingestion path for the entire system.

**Independent Test**: Can be fully tested by sending a well-formed import payload to the server and verifying that highlights, books, and authors are persisted and retrievable.

**Acceptance Scenarios**:

1. **Given** an empty server database, **When** the user syncs a clippings file containing 50 highlights across 5 books, **Then** the server stores all 50 highlights, 5 books, and their authors, and returns a summary showing 50 new highlights, 0 duplicates.
2. **Given** a server that already contains 30 highlights, **When** the user syncs a clippings file containing the same 30 plus 10 new highlights, **Then** only the 10 new highlights are added, and the summary shows 10 new, 30 duplicates.
3. **Given** a sync payload containing a book whose author already exists under a different book, **When** the import runs, **Then** the existing author record is reused (not duplicated).
4. **Given** a sync payload with an empty highlights list, **When** the import runs, **Then** the server returns a success response with zero new highlights and zero duplicates.

---

### User Story 2 — Read and Update Settings (Priority: P1)

A user configures their recap preferences — Kindle email, schedule (daily/weekly), delivery time, and highlights-per-recap count — through CLI commands that read from and write to the server.

**Why this priority**: The server must know the user's Kindle email and schedule before it can generate or deliver recaps. This is required for basic onboarding.

**Independent Test**: Can be fully tested by reading default settings, updating individual fields, and confirming that subsequent reads reflect the changes.

**Acceptance Scenarios**:

1. **Given** a newly created user with no explicit settings, **When** the user reads settings, **Then** the server returns sensible defaults: schedule "daily", delivery time "18:00", count 3.
2. **Given** existing settings, **When** the user updates the schedule to "weekly", **Then** subsequent reads show schedule "weekly" with all other settings unchanged.
3. **Given** existing settings, **When** the user sets count to 0 or 16, **Then** the server rejects the request with a clear validation error message.
4. **Given** existing settings, **When** the user sets the Kindle email to a valid address, **Then** the email is saved and returned on subsequent reads.
5. **Given** existing settings, **When** the user sends an update with an invalid email format, **Then** the server rejects the request with a validation error.

---

### User Story 3 — View Server Status (Priority: P2)

A user checks the server status to see the total number of highlights, books, and authors stored, as well as the next scheduled recap time and current delivery configuration.

**Why this priority**: Provides operational visibility but does not block data ingestion or configuration. Important for user confidence and troubleshooting.

**Independent Test**: Can be fully tested by seeding the server with known data and verifying the status response matches expected counts and schedule information.

**Acceptance Scenarios**:

1. **Given** a server with 100 highlights across 10 books by 5 authors, **When** the user requests status, **Then** the response includes total highlights (100), total books (10), total authors (5), and the next scheduled recap time.
2. **Given** an empty server, **When** the user requests status, **Then** the response shows zero highlights, zero books, zero authors, and indicates no recap is scheduled until highlights are imported and settings are configured.

---

### User Story 4 — Manage Exclusions (Priority: P2)

A user excludes specific highlights, entire books, or all books by an author from future recaps. They can also re-include previously excluded items and view a list of all current exclusions.

**Why this priority**: Exclusions directly affect recap quality and user satisfaction, but the system can function without them initially.

**Independent Test**: Can be fully tested by excluding/re-including items and confirming that the exclusions list reflects all changes.

**Acceptance Scenarios**:

1. **Given** an active highlight, **When** the user excludes it, **Then** the highlight is marked excluded and no longer eligible for recap selection.
2. **Given** an excluded highlight, **When** the user re-includes it, **Then** the highlight becomes eligible for recap selection again.
3. **Given** a book with 10 highlights, **When** the user excludes the book, **Then** all 10 highlights in that book are excluded from recap selection.
4. **Given** an excluded book, **When** the user re-includes it, **Then** its highlights become eligible for recap selection again (unless individually excluded).
5. **Given** an author with 3 books, **When** the user excludes the author, **Then** all highlights in all 3 books are excluded from recap selection.
6. **Given** a mix of excluded highlights, books, and authors, **When** the user lists exclusions, **Then** the response includes all three categories with identifying details (titles, names, highlight text snippets).
7. **Given** a nonexistent highlight/book/author ID, **When** the user attempts to exclude it, **Then** the server returns a clear "not found" error.

---

### User Story 5 — Manage Highlight Weights (Priority: P3)

A user assigns weights (1–5) to individual highlights to influence how often they appear in recaps. Higher-weighted highlights appear more frequently.

**Why this priority**: Weight management is a refinement of the recap selection algorithm. The system works well with default weights; this is a power-user feature.

**Independent Test**: Can be fully tested by setting weights on highlights and confirming the weights are stored and retrievable.

**Acceptance Scenarios**:

1. **Given** a highlight with default weight (3), **When** the user sets its weight to 5, **Then** subsequent reads show weight 5.
2. **Given** a highlight, **When** the user sets its weight to 0 or 6, **Then** the server rejects the request with a validation error.
3. **Given** multiple highlights with varying weights, **When** the user lists weighted highlights, **Then** the response includes all highlights with non-default weights and their current weight values.
4. **Given** a nonexistent highlight ID, **When** the user attempts to set its weight, **Then** the server returns a "not found" error.

---

### Edge Cases

- What happens when the sync payload contains highlights with missing or blank text? The server must reject those entries and report them in the summary.
- What happens when two concurrent sync requests arrive? The server must handle them without data corruption or duplicate records.
- What happens when an author name in the sync payload is `null` or unknown? The server must assign a placeholder (e.g., "Unknown Author") and still import the highlights.
- What happens when the user excludes a book and then also individually re-includes a highlight from that book? The book-level exclusion takes precedence; the individual highlight remains excluded.
- What happens when the database file is missing or corrupt at startup? The server should create/recreate the schema automatically (existing feature 002 behavior).

## Requirements

### Functional Requirements

- **FR-001**: System MUST accept a bulk import of highlights, books, and authors in a single request from the CLI client and persist them to storage.
- **FR-002**: System MUST deduplicate highlights during import — if a highlight with the same book title, author, and text already exists for the user, it is skipped.
- **FR-003**: System MUST create author and book records on the fly during import if they do not yet exist, and reuse existing records when they match.
- **FR-004**: System MUST return an import summary after each sync: new highlights added, duplicates skipped, new books created, new authors created.
- **FR-005**: System MUST expose current server status including total highlights, books, authors, excluded counts, and next scheduled recap information.
- **FR-006**: System MUST allow reading the current user settings and return sensible defaults when no explicit settings have been configured.
- **FR-007**: System MUST allow updating individual user settings (schedule, delivery time, count, Kindle email) with validation.
- **FR-008**: System MUST validate that schedule is one of "daily" or "weekly".
- **FR-009**: System MUST validate that delivery time is a valid time in HH:mm format.
- **FR-010**: System MUST validate that highlights-per-recap count is between 1 and 15 (inclusive).
- **FR-011**: System MUST validate that Kindle email is a syntactically valid email address.
- **FR-012**: System MUST allow excluding and re-including individual highlights by ID.
- **FR-013**: System MUST allow excluding and re-including entire books by ID, affecting all highlights within.
- **FR-014**: System MUST allow excluding and re-including all books by an author by author ID, affecting all highlights within those books.
- **FR-015**: System MUST list all current exclusions grouped by category (highlights, books, authors) with identifying information.
- **FR-016**: System MUST allow setting a highlight weight (1–5) by highlight ID.
- **FR-017**: System MUST list all highlights that have non-default weights.
- **FR-018**: System MUST return clear, actionable error messages for all validation failures, including which field failed and why.
- **FR-019**: System MUST return a "not found" error when operations target a nonexistent entity ID.
- **FR-020**: System MUST apply sensible defaults for new highlights: weight 3, excluded false, delivery count 0, last seen null.
- **FR-021**: System MUST support single-user operation for MVP, with user ID implicit or auto-created.
- **FR-022**: System MUST persist all data to durable storage that survives server restarts.

### Key Entities

- **Highlight**: A single text passage from a Kindle book. Core entity — carries weight, exclusion status, and delivery history. Belongs to one Book and one User.
- **Book**: A Kindle book identified by title. Contains multiple Highlights. Belongs to one Author and one User.
- **Author**: A book author identified by name. Has many Books.
- **User**: The Sunny Sunday user. Has Highlights, Books, Settings, and Exclusions. Single-user for MVP.
- **Settings**: Per-user configuration for recap delivery: schedule, delivery time, count, Kindle email.
- **Excluded Book**: A book-level exclusion record. All highlights in the book are excluded from recaps.
- **Excluded Author**: An author-level exclusion record. All highlights in all books by the author are excluded from recaps.

## Success Criteria

### Measurable Outcomes

- **SC-001**: Users can import a 1,000-highlight clippings file and receive a complete summary in under 5 seconds.
- **SC-002**: Duplicate highlights are correctly identified and skipped during import with 100% accuracy.
- **SC-003**: Users can read and update all settings through a single round-trip per operation.
- **SC-004**: All validation errors return a clear message identifying the specific field and constraint violated, with no server crashes or generic error pages.
- **SC-005**: Exclusions and re-inclusions take effect immediately — the next status check reflects the updated counts.
- **SC-006**: All data persists across server restarts without loss.
- **SC-007**: Users can complete the full onboarding flow (sync + configure settings) in under 2 minutes.
- **SC-008**: Weight changes are reflected immediately — the next weight listing shows updated values.

## Assumptions

- Single-user operation for MVP: the system auto-creates or reuses a single user record; multi-user support is deferred.
- The client CLI is responsible for parsing `My Clippings.txt` and sending structured data to the server; the server does not parse raw clippings text.
- The server database (SQLite) and schema bootstrap are already implemented in feature 002; this feature builds on that foundation.
- Domain models (Highlight, Book, Author, User, Settings) are already defined in `SunnySunday.Server/Models/`.
- No authentication is required for MVP — the server trusts all requests on the local network (per ADR-004).
- Delivery time is expressed in the client's local timezone.
- The sync endpoint receives data in the same structure as the parser output (`ParseResult` with `ParsedBook` and `ParsedHighlight` records).
- The "next scheduled recap" shown in the status endpoint is informational and may be approximate; exact scheduling logic is implemented in a later feature (recap generation).

# Feature Specification: [FEATURE NAME]

**Feature Branch**: `[###-feature-name]`  
**Created**: [DATE]  
**Status**: Draft  
**Input**: User description: "$ARGUMENTS"

## User Scenarios & Testing *(mandatory)*

<!--
  IMPORTANT: User stories should be PRIORITIZED as user journeys ordered by importance.
  Each user story/journey must be INDEPENDENTLY TESTABLE - meaning if you implement just ONE of them,
  you should still have a viable MVP (Minimum Viable Product) that delivers value.
  
  Assign priorities (P1, P2, P3, etc.) to each story, where P1 is the most critical.
  Think of each story as a standalone slice of functionality that can be:
  - Developed independently
  - Tested independently
  - Deployed independently
  - Demonstrated to users independently
-->

### User Story 1 - [Brief Title] (Priority: P1)

[Describe this user journey in plain language]

**Why this priority**: [Explain the value and why it has this priority level]

**Independent Test**: [Describe how this can be tested independently - e.g., "Can be fully tested by [specific action] and delivers [specific value]"]

**Acceptance Scenarios**:

1. **Given** [initial state], **When** [action], **Then** [expected outcome]
2. **Given** [initial state], **When** [action], **Then** [expected outcome]

---

### User Story 2 - [Brief Title] (Priority: P2)

[Describe this user journey in plain language]

**Why this priority**: [Explain the value and why it has this priority level]

**Independent Test**: [Describe how this can be tested independently]

**Acceptance Scenarios**:

1. **Given** [initial state], **When** [action], **Then** [expected outcome]

---

### User Story 3 - [Brief Title] (Priority: P3)

[Describe this user journey in plain language]

**Why this priority**: [Explain the value and why it has this priority level]

**Independent Test**: [Describe how this can be tested independently]

**Acceptance Scenarios**:

1. **Given** [initial state], **When** [action], **Then** [expected outcome]

---

[Add more user stories as needed, each with an assigned priority]

### Edge Cases

<!--
  ACTION REQUIRED: The content in this section represents placeholders.
  Fill them out with the right edge cases.
-->

- What happens when [boundary condition]?
- How does system handle [error scenario]?

## Requirements *(mandatory)*

<!--
  ACTION REQUIRED: The content in this section represents placeholders.
  Fill them out with the right functional requirements.
-->

### Functional Requirements

- **FR-001**: System MUST [specific capability, e.g., "allow users to create accounts"]
- **FR-002**: System MUST [specific capability, e.g., "validate email addresses"]  
- **FR-003**: Users MUST be able to [key interaction, e.g., "reset their password"]
- **FR-004**: System MUST [data requirement, e.g., "persist user preferences"]
- **FR-005**: System MUST [behavior, e.g., "log all security events"]

*Example of marking unclear requirements:*

- **FR-006**: System MUST authenticate users via [NEEDS CLARIFICATION: auth method not specified - email/password, SSO, OAuth?]
- **FR-007**: System MUST retain user data for [NEEDS CLARIFICATION: retention period not specified]

### Key Entities *(include if feature involves data)*

- **[Entity 1]**: [What it represents, key attributes without implementation]
- **[Entity 2]**: [What it represents, relationships to other entities]

## Success Criteria *(mandatory)*

<!--
  ACTION REQUIRED: Define measurable success criteria.
  These must be technology-agnostic and measurable.
-->

### Measurable Outcomes

- **SC-001**: [Measurable metric, e.g., "Users can complete account creation in under 2 minutes"]
- **SC-002**: [Measurable metric, e.g., "System handles 1000 concurrent users without degradation"]
- **SC-003**: [User satisfaction metric, e.g., "90% of users successfully complete primary task on first attempt"]
- **SC-004**: [Business metric, e.g., "Reduce support tickets related to [X] by 50%"]

## Assumptions

<!--
  ACTION REQUIRED: The content in this section represents placeholders.
  Fill them out with the right assumptions based on reasonable defaults
  chosen when the feature description did not specify certain details.
-->

- [Assumption about target users, e.g., "Users have stable internet connectivity"]
- [Assumption about scope boundaries, e.g., "Mobile support is out of scope for v1"]
- [Assumption about data/environment, e.g., "Existing authentication system will be reused"]
- [Dependency on existing system/service, e.g., "Requires access to the existing user profile API"]
