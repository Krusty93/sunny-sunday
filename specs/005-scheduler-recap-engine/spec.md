# Feature Specification: 005 Scheduler + Recap Engine

**Feature Branch**: `005-scheduler-recap-engine`  
**Created**: 2026-04-26  
**Status**: Draft  
**Input**: User request: "Implement scheduling and recap engine with spaced repetition, weighting, retries, and Kindle-compatible recap output."

## User Scenarios & Testing

### User Story 1 - Automatic Recap Scheduling (Priority: P1)

A user configures recap cadence (`daily` or `weekly`) and delivery time from the CLI, and the server executes deliveries automatically at the configured time.

**Why this priority**: Without scheduling, recap delivery cannot happen unattended.

**Independent Test**: Configure daily at 18:00 local time with timezone, run scheduler with virtual time, verify one recap job is triggered for each expected slot.

**Acceptance Scenarios**:

1. **Given** default settings, **When** the server starts, **Then** the next recap is scheduled for daily at 18:00 in client local time.
2. **Given** schedule `weekly` at 20:00, **When** the user changes settings, **Then** the scheduler re-plans the next execution using the new cadence/time.
3. **Given** a scheduled slot is reached, **When** the scheduler fires, **Then** the recap pipeline is executed exactly once for that slot.

---

### User Story 2 - Weighted Spaced-Repetition Selection (Priority: P1)

A user receives recap highlights selected with a combined score based on highlight age and weight.

**Why this priority**: This is the core learning value of the product.

**Independent Test**: Seed highlights with different `last_seen`, `created_at`, and `weight`; run selection repeatedly with fixed seed/time and verify ranking and tie-break behavior.

**Acceptance Scenarios**:

1. **Given** eligible highlights with different ages and weights, **When** selection runs, **Then** highlights are ranked by `score = age + weight` (higher score first).
2. **Given** two highlights with equal score, **When** tie-break is needed, **Then** the more recent highlight is selected first.
3. **Given** exclusions at highlight/book/author level, **When** selection runs, **Then** excluded items are never selected.

---

### User Story 3 - Reliable Delivery with Retries (Priority: P1)

A user gets automatic retries when SMTP delivery fails, with actionable errors if all attempts fail.

**Why this priority**: Delivery reliability is mandatory for a scheduled system.

**Independent Test**: Force SMTP failures and verify retry count/backoff, terminal failure status, and unchanged recap history on failure.

**Acceptance Scenarios**:

1. **Given** first SMTP attempt fails transiently, **When** retry policy is active, **Then** the system retries automatically.
2. **Given** delivery eventually succeeds on retry, **When** success is confirmed, **Then** recap history is updated once for delivered highlights.
3. **Given** all attempts fail, **When** retries are exhausted, **Then** recap history is not updated and a clear error is logged/exposed.

---

### User Story 4 - EPUB Recap Composition (Priority: P1)

A user receives a Kindle-friendly EPUB recap rendered as a flat list of highlights.

**Why this priority**: Output format and readability are core product behavior.

**Independent Test**: Generate EPUB from known dataset and validate structure/content against expected list format.

**Acceptance Scenarios**:

1. **Given** selected highlights, **When** recap is generated, **Then** output is EPUB.
2. **Given** recap content, **When** rendered, **Then** highlights appear as a flat list (no grouping by book).
3. **Given** each list item, **When** displayed, **Then** it includes highlight text plus its source metadata.

## Edge Cases

- No eligible highlights (all excluded or empty DB): skip delivery and expose informative status.
- `last_seen` is null: treat as oldest/never-seen for age calculation.
- Weekly schedule with day not explicitly set by user: use current settings default behavior from settings domain.
- Duplicate scheduler triggers after restart or clock drift: deduplicate by scheduled slot key.
- Retry succeeds after one or more failures: update history once, not per attempt.

## Requirements

### Functional Requirements

- **FR-005-01**: System MUST execute recap jobs automatically based on configured schedule (`daily` or `weekly`) and delivery time defaulting to 18:00 local time (client).
- **FR-005-02**: System MUST compute `next recap` deterministically from schedule settings.
- **FR-005-03**: System MUST select recap candidates only from non-excluded highlights.
- **FR-005-04**: System MUST rank candidates by `score = age + weight`.
- **FR-005-05**: System MUST use recency as tie-break: equal score -> most recent highlight first.
- **FR-005-06**: System MUST cap selected highlights to configured `count` (1..15).
- **FR-005-07**: System MUST generate recap as EPUB.
- **FR-005-08**: System MUST render recap as a flat list where each item contains highlight text and source metadata.
- **FR-005-09**: System MUST send recap via configured SMTP pipeline.
- **FR-005-10**: System MUST apply automatic retries on failed delivery attempts.
- **FR-005-11**: System MUST use retry policy: maximum 3 attempts total, exponential backoff (1m, 5m).
- **FR-005-12**: System MUST update `last_seen` and `delivery_count` only after confirmed successful delivery.
- **FR-005-13**: System MUST NOT update recap history when delivery fails permanently.
- **FR-005-14**: System MUST expose actionable failure reasons for exhausted retries (US-08 alignment).
- **FR-005-15**: Client schedule inputs MUST be expressed in client local time with timezone information included.
- **FR-005-16**: Server MUST convert incoming scheduled times to UTC, persist UTC values, and execute scheduling in UTC only.
- **FR-005-17**: Server MUST serialize outbound recap/schedule timestamps in UTC.
- **FR-005-18**: Client MUST parse UTC timestamps from the server and convert them to client local time for display.

### Key Entities

- **RecapJob**: One scheduled execution slot for recap generation and delivery.
- **SelectionCandidate**: Highlight with computed `age`, `weight`, and final `score`.
- **RecapDocument**: Generated EPUB artifact plus metadata used for delivery.
- **DeliveryAttempt**: One SMTP send attempt with outcome and error details.

## Success Criteria

### Measurable Outcomes

- **SC-005-01**: Scheduled recap starts within 60 seconds of intended slot in normal runtime conditions.
- **SC-005-02**: Candidate ranking matches `score = age + weight` and tie-break rule in deterministic tests.
- **SC-005-03**: 100% of successful deliveries update history exactly once per delivered highlight.
- **SC-005-04**: 100% of permanently failed deliveries leave history unchanged.
- **SC-005-05**: Generated recap file is valid EPUB and contains flat, non-grouped highlight list with source metadata.

## Assumptions

- Schedule defaults remain owned by settings domain (`daily`, `18:00`, local time semantics as configured by client with timezone included).
- Existing exclusions and weights APIs from feature 004 are source of truth for selection.
- Initial implementation is single-user (user id 1) per current MVP architecture.
