# Tasks: Scheduler + Recap Engine

**Input**: Design documents from `/specs/005-scheduler-recap-engine/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, quickstart.md

**Tests**: Included — xUnit service tests and ASP.NET integration tests are part of this feature per the design docs and repository conventions.

**Organization**: Tasks are grouped by user story so each slice stays independently testable after the shared foundation is in place.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on an incomplete task)
- **[Story]**: Which user story the task belongs to (`US1`, `US2`, `US3`, `US4`)
- All file paths are relative to the repository root

---

## Phase 1: Setup

**Purpose**: Add the shared and server-side dependencies needed for scheduling, SMTP delivery, and retries.

- [X] T001 Create `src/PackageVersions.props` to declare `Polly` once for both client and server, then update `src/SunnySunday.Cli/SunnySunday.Cli.csproj` and `src/SunnySunday.Server/SunnySunday.Server.csproj` to consume that shared `Polly` version while adding `Quartz.Extensions.Hosting` and `MailKit` to the server; verify the solution still builds from `src/SunnySunday.slnx`.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared schema, contracts, configuration, and test-harness changes required by every user story.

**⚠️ CRITICAL**: No user story work should start until this phase is complete.

- [X] T002 Update `src/SunnySunday.Server/Infrastructure/Database/SchemaBootstrap.cs` to add the `recap_jobs` table and unique slot index, plus an idempotent `timezone` migration for the `settings` table in both sync and async bootstrap paths.
- [X] T003 [P] Update `src/SunnySunday.Server/Models/Settings.cs` and `src/SunnySunday.Server/Data/SettingsRepository.cs` to persist `Timezone` with a default of `UTC` alongside the existing schedule fields.
- [X] T004 [P] Create `src/SunnySunday.Server/Models/RecapJobRecord.cs`, `src/SunnySunday.Server/Services/SelectionCandidate.cs`, and `src/SunnySunday.Server/Data/RecapRepository.cs` for recap slot persistence, last-job lookup, candidate reads, and post-delivery highlight history updates.
- [X] T005 [P] Update `src/SunnySunday.Core/Contracts/SettingsResponse.cs`, `src/SunnySunday.Core/Contracts/UpdateSettingsRequest.cs`, and `src/SunnySunday.Core/Contracts/StatusResponse.cs` to add `Timezone`, `LastRecapStatus`, and `LastRecapError` contract fields.
- [X] T006 [P] Create `src/SunnySunday.Server/Infrastructure/Smtp/SmtpSettings.cs` and update `src/SunnySunday.Server/appsettings.json` plus `src/SunnySunday.Server/appsettings.Development.json` with `Smtp` settings for `Host`, `Port`, `Username`, `Password`, `FromAddress`, and `UseSsl` so `FromAddress` remains explicit in server configuration.
- [X] T007 Update `src/SunnySunday.Server/Program.cs` to bind `SmtpSettings` from configuration and map the README-source environment variables `SMTP_HOST`, `SMTP_PORT`, `SMTP_USER`, `SMTP_PASSWORD`, `SMTP_FROM_ADDRESS`, and `SMTP_USE_SSL` onto that options model before feature service registration.
- [X] T008 Update `src/SunnySunday.Tests/Api/SunnyTestApplicationFactory.cs` to apply the new schema shape in-memory and support replacing recap pipeline services during scheduler and delivery integration tests.

**Checkpoint**: Schema, contracts, SMTP config binding, and shared test plumbing are ready. User stories can now proceed in dependency order.

---

## Phase 3: User Story 1 - Automatic Recap Scheduling (Priority: P1)

**Goal**: Persist schedule timezone data, compute the next recap deterministically, and trigger the recap pipeline exactly once per scheduled slot.

**Independent Test**: Configure daily and weekly settings with a valid timezone, verify `GET /status` returns the next UTC recap time, and execute the Quartz job against a fake recap service to confirm one pipeline invocation per slot.

### Tests for User Story 1

- [ ] T009 [P] [US1] Extend `src/SunnySunday.Tests/Api/SettingsEndpointTests.cs` and `src/SunnySunday.Tests/Api/StatusEndpointTests.cs` to cover timezone persistence, timezone validation failures, and UTC `nextRecap` serialization.
- [ ] T010 [P] [US1] Create `src/SunnySunday.Tests/Recap/SchedulerServiceTests.cs` to verify daily and weekly next-fire calculations, rescheduling after settings changes, and duplicate-slot behavior with deterministic inputs.

### Implementation for User Story 1

- [ ] T011 [P] [US1] Create `src/SunnySunday.Server/Services/IRecapService.cs` and `src/SunnySunday.Server/Jobs/RecapJob.cs` so Quartz can invoke the recap pipeline and skip slots already marked delivered in `recap_jobs`.
- [ ] T012 [P] [US1] Create `src/SunnySunday.Server/Services/ISchedulerService.cs` and `src/SunnySunday.Server/Services/SchedulerService.cs` to translate `Settings` cadence, delivery time, and timezone into Quartz UTC triggers and expose the next fire time.
- [ ] T013 [US1] Update `src/SunnySunday.Server/Endpoints/SettingsEndpoints.cs` and `src/SunnySunday.Server/Endpoints/StatusEndpoints.cs` to validate `Timezone`, reschedule on `PUT /settings`, and serialize `NextRecap` in UTC.
- [ ] T014 [US1] Update `src/SunnySunday.Server/Program.cs` to register Quartz, `SchedulerService`, and `RecapJob`, and to schedule the initial trigger from persisted settings during server startup.

**Checkpoint**: Scheduling is functional and independently testable with a fake recap pipeline.

---

## Phase 4: User Story 2 - Weighted Spaced-Repetition Selection (Priority: P1)

**Goal**: Select recap candidates from non-excluded highlights using `score = age + weight`, with recent highlights winning score ties.

**Independent Test**: Seed highlights with different `last_seen`, `created_at`, exclusions, and weights, run selection with a fixed clock, and verify ranking plus count capping.

### Tests for User Story 2

- [ ] T015 [P] [US2] Create `src/SunnySunday.Tests/Recap/HighlightSelectionServiceTests.cs` to cover age-plus-weight scoring, `last_seen = null` handling, recent-first tie breaks, exclusion filtering, and `count` limits.

### Implementation for User Story 2

- [ ] T016 [US2] Create `src/SunnySunday.Server/Services/HighlightSelectionService.cs` to compute `ageInDays`, rank `SelectionCandidate` records from `RecapRepository.SelectCandidatesAsync`, and return the top configured highlights.

**Checkpoint**: Candidate ranking is deterministic, tested, and ready for recap composition.

---

## Phase 5: User Story 4 - EPUB Recap Composition (Priority: P1)

**Goal**: Compose a Kindle-friendly EPUB 2 document that renders recap highlights as one flat list with source metadata.

**Independent Test**: Generate an EPUB from a known ordered selection and verify the ZIP structure, XHTML content, and item ordering without involving SMTP.

### Tests for User Story 4

- [ ] T017 [P] [US4] Create `src/SunnySunday.Tests/Recap/EpubComposerTests.cs` to verify EPUB structure, uncompressed `mimetype`, flat-list XHTML output, and source metadata rendering in input order.

### Implementation for User Story 4

- [ ] T018 [US4] Create `src/SunnySunday.Server/Services/EpubComposer.cs` to build an EPUB 2 archive in memory with `mimetype`, `META-INF/container.xml`, `OEBPS/content.opf`, `OEBPS/toc.ncx`, and `OEBPS/highlights.xhtml` containing a flat `<ul>` of highlights.

**Checkpoint**: EPUB generation is deterministic and validated independently from scheduling and SMTP.

---

## Phase 6: User Story 3 - Reliable Delivery with Retries (Priority: P1)

**Goal**: Deliver recap EPUBs via SMTP with Polly exponential backoff, update recap history only after confirmed success, and expose actionable failure details.

**Independent Test**: Run the recap pipeline with a fake mail service that fails and succeeds in controlled patterns, then verify retry counts, final job status, and highlight history updates.

### Tests for User Story 3

- [ ] T019 [P] [US3] Create `src/SunnySunday.Tests/Recap/RecapServiceTests.cs` to cover immediate success, transient retry then success, exhausted retries, no-eligible-highlight skips, and single history updates after confirmed delivery.

### Implementation for User Story 3

- [ ] T020 [P] [US3] Create `src/SunnySunday.Server/Services/IMailDeliveryService.cs` and `src/SunnySunday.Server/Services/MailDeliveryService.cs` to send recap EPUB attachments through MailKit using `SmtpSettings.FromAddress` and the mapped SMTP environment variables.
- [ ] T021 [P] [US3] Create `src/SunnySunday.Server/Infrastructure/Resilience/RecapDeliveryPolicy.cs` to define a Polly exponential backoff policy capped at 3 total attempts, with delay computation owned by the policy helper rather than hardcoded waits in application services.
- [ ] T022 [US3] Create `src/SunnySunday.Server/Services/RecapService.cs` to orchestrate candidate selection, EPUB composition, `recap_jobs` persistence, SMTP delivery through the Polly policy, and post-success `last_seen` plus `delivery_count` updates.
- [ ] T023 [US3] Update `src/SunnySunday.Server/Program.cs` and `src/SunnySunday.Server/Data/StatusRepository.cs` to register `IMailDeliveryService` and `RecapService`, and to expose `LastRecapStatus` plus `LastRecapError` from `recap_jobs` in status responses.

**Checkpoint**: End-to-end recap generation and delivery are reliable, retry-aware, and auditable.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Align repo docs with the implemented behavior and verify the full feature set.

- [ ] T024 [P] Update `README.md`, `docs/DX.md`, and `docs/ARCHITECTURE.md` to document the recap pipeline, `recap_jobs`, timezone-aware scheduling, and the authoritative SMTP variables `SMTP_HOST`, `SMTP_PORT`, `SMTP_USER`, `SMTP_PASSWORD`, `SMTP_FROM_ADDRESS`, and `SMTP_USE_SSL`.
- [ ] T025 [P] Reconcile `specs/005-scheduler-recap-engine/quickstart.md` with the shipped configuration shape so local-development and Docker examples include the final SMTP settings and UTC scheduling expectations.
- [ ] T026 Validate the feature against `specs/005-scheduler-recap-engine/quickstart.md` and run the regression suite from `src/SunnySunday.slnx`.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies.
- **Phase 2 (Foundational)**: Depends on Phase 1 and blocks all user stories.
- **Phase 3 (US1 Scheduling)**: Depends on Phase 2.
- **Phase 4 (US2 Selection)**: Depends on Phase 2.
- **Phase 5 (US4 EPUB Composition)**: Depends on Phase 2.
- **Phase 6 (US3 Delivery + Retry)**: Depends on Phases 2, 4, and 5; it integrates with Phase 3 for live scheduled execution.
- **Phase 7 (Polish)**: Depends on all story phases.

### User Story Dependencies

- **US1**: Can start as soon as the shared foundation is done; it only requires a recap-service interface, not the final delivery implementation.
- **US2**: Can start after the foundation and is independent from scheduling and SMTP.
- **US4**: Can start after the foundation and is independent from Quartz and SMTP when fed deterministic `SelectionCandidate` input.
- **US3**: Requires US2 and US4 because it consumes ranked candidates and EPUB bytes; it then plugs into the US1 scheduler path.

### Within Each User Story

- Write or extend the story tests first.
- Add the story-specific interfaces or models next.
- Implement the core service behavior.
- Finish with endpoint, DI, or orchestration wiring.

### Parallel Opportunities

- `T003`, `T004`, `T005`, and `T006` can run in parallel once `T002` lands.
- `US1`, `US2`, and `US4` can proceed in parallel after Phase 2.
- Within `US1`, `T009` and `T010` can run in parallel, then `T011` and `T012` can run in parallel before the endpoint and startup wiring.
- Within `US3`, `T019`, `T020`, and `T021` can run in parallel before `T022` integrates them.

---

## Parallel Example: User Story 1

```text
T009 ──┐
       ├──► T011 ──┐
T010 ──┘           ├──► T013 ───► T014
T012 ──────────────┘
```

## Parallel Example: User Story 3

```text
T019 ──┐
T020 ──┼──► T022 ───► T023
T021 ──┘
```

---

## Implementation Strategy

### Incremental Delivery

1. Complete Setup and Foundational work to unblock all later slices.
2. Land US1 with a fake recap-service dependency to prove scheduler correctness and status exposure.
3. Land US2 and US4 in either order to finish deterministic selection and EPUB generation.
4. Land US3 to make recap delivery live with retry semantics and history updates.
5. Finish with doc alignment and full quickstart plus regression validation.

### Suggested MVP Scope

For this feature, the smallest demonstrable increment is **Phase 1 + Phase 2 + US1** for scheduler correctness. The smallest end-to-end recap-delivery slice is **US1 + US2 + US4 + US3** because all four stories are required for an actual delivered recap.

---

## Notes

- All SMTP configuration work should preserve `FromAddress` inside `SmtpSettings`; it is not derived from `Username`.
- The README-style environment variable names are the source of truth for server config mapping.
- Retry logic must use Polly exponential backoff with a maximum of 3 total attempts and no inline fixed-delay waits in application code.
