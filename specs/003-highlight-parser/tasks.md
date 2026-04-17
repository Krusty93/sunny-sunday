# Tasks: Highlight Parser

**Input**: Design documents from `/specs/003-highlight-parser/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, quickstart.md

**Tests**: TDD approach — write tests before or alongside implementation. Tests go in `src/SunnySunday.Tests/Parsing/ClippingsParserTests.cs`.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3, US4)
- All source paths are relative to repository root

---

## Phase 1: Setup

**Purpose**: Create directory structure for parser code and tests

- [X] T001 Create directories `src/SunnySunday.Core/Parsing/` and `src/SunnySunday.Tests/Parsing/`, verify `dotnet build src/SunnySunday.slnx` still succeeds

---

## Phase 2: Foundational — Parser Data Types

**Purpose**: Define all parser-specific types that every user story depends on. These types live in `SunnySunday.Core/Parsing/` namespace and are distinct from the persistence models in `SunnySunday.Core/Models/`.

**⚠️ CRITICAL**: No user story work can begin until all types are defined and the solution builds.

- [X] T002 [P] Create `RawClipping` internal record in `src/SunnySunday.Core/Parsing/RawClipping.cs` with properties: `string Title`, `string? Author`, `bool IsNote`, `string? Location`, `DateTimeOffset? AddedOn`, `string Text`. This is an intermediate type not exposed publicly. Mark the class `internal`. `IsNote` is true when the metadata line says "Note" — used to prepend `[my note]` prefix.
- [X] T003 [P] Create `ParsedHighlight` public record in `src/SunnySunday.Core/Parsing/ParsedHighlight.cs` with properties: `string Text`, `string? Location`, `DateTimeOffset? AddedOn`. Immutable. Notes are stored here with `[my note]` already prepended to `Text` — no type field.
- [X] T004 [P] Create `ParsedBook` public record in `src/SunnySunday.Core/Parsing/ParsedBook.cs` with properties: `string Title`, `string? Author`, `IReadOnlyList<ParsedHighlight> Highlights`. A ParsedBook is never emitted with zero highlights.
- [X] T005 [P] Create `ParseResult` public record in `src/SunnySunday.Core/Parsing/ParseResult.cs` with properties: `IReadOnlyList<ParsedBook> Books`, `int TotalEntriesProcessed`, `int DuplicatesRemoved`. No warnings collection — malformed entries are logged via `ILogger`.
- [X] T006 Verify `dotnet build src/SunnySunday.slnx` succeeds with all four new types. Fix any compilation errors.

**Checkpoint**: All parser types compile. User story implementation can begin.

---

## Phase 3: User Story 1 — Parse Highlights from My Clippings.txt (Priority: P1) 🎯 MVP

**Goal**: Parse a standard `My Clippings.txt` file and extract structured highlight data including book title, author, highlight text, location, and date. Bookmarks are skipped. Notes are treated as highlights with a `[my note]` prefix on their text.

**Independent Test**: Provide a sample `My Clippings.txt` string via `StringReader` and verify all valid highlights are extracted with correct book/author/text associations.

**Functional Requirements**: FR-001, FR-002, FR-003, FR-004, FR-005

### Tests for User Story 1

> **Write these tests FIRST — they must FAIL (or not compile) before implementation begins.**
> All tests go in `src/SunnySunday.Tests/Parsing/ClippingsParserTests.cs` using xUnit `[Fact]` / `[Theory]`.
> Use `StringReader` to provide test input — no temp files needed.
> Reference quickstart.md for sample Kindle format.

- [X] T009 [P] [US1] Write test in `src/SunnySunday.Tests/Parsing/ClippingsParserTests.cs`: given a standard `My Clippings.txt` input with 3 highlights across 2 books, `ClippingsParser.ParseAsync(TextReader)` returns a `ParseResult` with correct total entry count and highlights extractable from the books list. Use the exact Kindle format: title/author line, metadata line, blank line, content, `==========` separator. See `specs/003-highlight-parser/quickstart.md` for format examples.
- [X] T010 [P] [US1] Write test: given a clipping entry with multi-line highlight text (content spanning 3+ lines between blank line and separator), the full text is captured as a single highlight with newlines preserved.
- [X] T011 [P] [US1] Write tests for title/author extraction edge cases: (a) standard `Book Title (Author Name)` → title=`Book Title`, author=`Author Name`; (b) author as `Last, First` in parens → author=`Last, First`; (c) title with nested parentheses like `The Art of War (Annotated) (Sun Tzu)` → title=`The Art of War (Annotated)`, author=`Sun Tzu`; (d) no parentheses at all → title=full line, author=`null`; (e) empty parens `Some Book ()` → title=`Some Book`, author=`null`. See `specs/003-highlight-parser/research.md` section 2 for the full edge case table.
- [X] T012 [P] [US1] Write tests for metadata line parsing and entry type handling: (a) `- Your Highlight on Location 100-105 | Added on Thursday, January 1, 2026 12:00:00 AM` → location=`Location 100-105`, date parsed, text used as-is; (b) `- Your Note on ...` → text gets `[my note]` prefix prepended; (c) `- Your Bookmark on ...` → entry is excluded from ParseResult.Books entirely. See `specs/003-highlight-parser/research.md` section 3.
- [X] T013 [P] [US1] Write test for `ClippingsParser.ParseAsync(string filePath)` overload: create a temp file with valid Kindle content, parse via file path, verify same result as TextReader overload. Clean up temp file after test.

### Implementation for User Story 1

- [X] T014 [US1] Create `ClippingsParser` static class in `src/SunnySunday.Core/Parsing/ClippingsParser.cs` with two public static methods: `Task<ParseResult> ParseAsync(TextReader reader, ILogger? logger = null)` and `Task<ParseResult> ParseAsync(string filePath, ILogger? logger = null)`. The file-path overload creates a `StreamReader` (UTF-8, BOM-detected) and delegates to the TextReader overload. The optional `ILogger` is used to log warnings for malformed entries. Initial implementation can return an empty `ParseResult`.
- [X] T015 [US1] Implement entry splitting in `ParseAsync(TextReader)`: read lines via `ReadLineAsync()`, accumulate lines into a buffer, and split entries on the `==========` separator line. Track a 1-based entry index counter. Each accumulated block of lines between separators is one raw clipping entry.
- [X] T016 [US1] Implement title/author extraction: given the first line of an entry, extract the title and author. Author is the content of the **last** parenthesized group `(...)` on the line. If no parentheses are found, the entire line (trimmed) is the title and author is `null`. Empty parentheses `()` should also yield `null` author. Trim both title and author. See `specs/003-highlight-parser/research.md` section 2.
- [X] T017 [US1] Implement metadata line parsing: given the second line of an entry, use regex `^- Your (?<type>Highlight|Note|Bookmark) on (?<location>.+?) \| Added on (?<date>.+)$` to extract the entry type (internal use only), location string, and date string. Parse the date with format `dddd, MMMM d, yyyy h:mm:ss tt` using `CultureInfo.InvariantCulture` into `DateTimeOffset?`; if date parsing fails, store `null` (do not skip the entry). Set `IsNote = true` when type is "Note". See `specs/003-highlight-parser/research.md` section 3.
- [X] T018 [US1] Implement content text extraction: the content starts at line index 3 of the entry (after title, metadata, blank line) and continues until the end of the entry block. Join all content lines with `\n`. Trim the final result. Empty content (bookmarks) yields an empty string.
- [X] T019 [US1] Assemble the full parse pipeline: for each entry, build a `RawClipping` from the extracted fields. Filter out bookmarks (detected from metadata type). For notes (`IsNote == true`), prepend `[my note] ` to the text. Convert remaining entries to `ParsedHighlight` objects. For now (before US2/US3), return a `ParseResult` with all highlights grouped into books by `(Title, Author)` pair using a dictionary, populate `TotalEntriesProcessed`, and set `DuplicatesRemoved = 0`.
- [X] T020 [US1] Run `dotnet test src/SunnySunday.Tests/SunnySunday.Tests.csproj --filter "FullyQualifiedName~Parsing"` — all US1 tests must pass.

**Checkpoint**: Parser can extract highlights from valid `My Clippings.txt` input. This is the MVP — a usable parser even without dedup/error handling.

---

## Phase 4: User Story 2 — Deduplicate Highlights (Priority: P2)

**Goal**: After parsing, remove duplicate highlights. A duplicate is defined as an exact case-sensitive match on the `(Title, Author, Text)` tuple. The first occurrence is kept.

**Independent Test**: Provide input with known duplicates and verify only unique highlights remain in the output.

**Functional Requirements**: FR-006

### Tests for User Story 2

- [ ] T021 [P] [US2] Write test in `src/SunnySunday.Tests/Parsing/ClippingsParserTests.cs`: given input where the same highlight text appears twice for the same book and author, the result contains only one instance of that highlight and `DuplicatesRemoved == 1`.
- [ ] T022 [P] [US2] Write test: given two highlights with identical text but from different books (different title or author), both highlights are retained — they are not considered duplicates.
- [ ] T023 [P] [US2] Write test: given two highlights from the same book where one is a substring of the other (e.g., "War is peace" vs "War is peace. Freedom is slavery."), both are retained as distinct highlights. Only exact text matches are duplicates.
- [ ] T024 [P] [US2] Write test: given 10 clippings where 3 are duplicates, `ParseResult.DuplicatesRemoved == 3` and the total unique highlights across all books equals 7.

### Implementation for User Story 2

- [ ] T025 [US2] Implement deduplication in `src/SunnySunday.Core/Parsing/ClippingsParser.cs`: after building `RawClipping` list and filtering bookmarks, use a `HashSet<(string, string?, string)>` keyed on `(Title, Author, Text)` with `StringComparison.Ordinal` semantics to deduplicate. Keep the first occurrence (file order). Increment a duplicates counter. Wire `DuplicatesRemoved` into the returned `ParseResult`.
- [ ] T026 [US2] Run `dotnet test src/SunnySunday.Tests/SunnySunday.Tests.csproj --filter "FullyQualifiedName~Parsing"` — all US1 and US2 tests must pass.

**Checkpoint**: Parser now deduplicates highlights. Existing US1 tests still pass (dedup with zero duplicates is a no-op).

---

## Phase 5: User Story 3 — Group Highlights by Book and Author (Priority: P3)

**Goal**: Organize deduplicated highlights into `ParsedBook` objects, each identified by the `(Title, Author)` pair. Each book contains its highlights in file order. Empty books (all bookmarks) are not emitted.

**Independent Test**: Provide multi-book input and verify correct grouping in the output structure.

**Functional Requirements**: FR-007, FR-010

### Tests for User Story 3

- [ ] T027 [P] [US3] Write test in `src/SunnySunday.Tests/Parsing/ClippingsParserTests.cs`: given 6 highlights across 3 books by different authors, `ParseResult.Books` contains exactly 3 `ParsedBook` entries, each with the correct title, author, and highlight count.
- [ ] T028 [P] [US3] Write test: given highlights from two different books by the same author, the result contains 2 separate `ParsedBook` entries (grouped by title + author pair, not author alone).
- [ ] T029 [P] [US3] Write test: given a book whose only entries are bookmarks (no highlights or notes), that book does not appear in `ParseResult.Books` (no empty books).

### Implementation for User Story 3

- [ ] T030 [US3] Verify grouping logic in `src/SunnySunday.Core/Parsing/ClippingsParser.cs`: the grouping by `(Title, Author)` should already be implemented from T019. Ensure that: (a) books are emitted in first-seen order; (b) highlights within each book are in file order; (c) books with zero highlights after filtering are excluded. Adjust implementation if needed.
- [ ] T031 [US3] Run `dotnet test src/SunnySunday.Tests/SunnySunday.Tests.csproj --filter "FullyQualifiedName~Parsing"` — all US1, US2, and US3 tests must pass.

**Checkpoint**: Parser produces correctly grouped output. All previous stories still pass.

---

## Phase 6: User Story 4 — Handle Malformed Entries Gracefully (Priority: P4)

**Goal**: Skip malformed or incomplete clipping entries without crashing. Log skipped entries as warnings via `ILogger`. Valid entries surrounding malformed ones are still parsed correctly.

**Independent Test**: Provide input with intentionally malformed entries mixed with valid ones and verify valid highlights are extracted and warnings are logged.

**Functional Requirements**: FR-008, FR-009, FR-011

### Tests for User Story 4

- [ ] T032 [P] [US4] Write test in `src/SunnySunday.Tests/Parsing/ClippingsParserTests.cs`: given 10 valid entries and 1 malformed entry (e.g., missing metadata line — only title line then separator), all 10 valid highlights are extracted. Use a mock `ILogger` to verify a warning was logged with the correct entry index and a descriptive reason.
- [ ] T033 [P] [US4] Write test: given an empty input (empty string or no content), `ParseResult.Books` is empty and no exception is thrown.
- [ ] T034 [P] [US4] Write test: given input containing only `==========` separators and no actual clipping content, the result is empty books (no valid entries, blank entries silently skipped).
- [ ] T035 [P] [US4] Write test: given a clipping entry where the title line has no parentheses (missing author), the entry is parsed with best-effort: title is the full first line (trimmed) and author is `null`. The entry is NOT skipped — it appears in the result.
- [ ] T036 [P] [US4] Write test: given a clipping entry with an unrecognized type on the metadata line (e.g., `- Your Clip on Location 50 | Added on ...`), the entry is skipped and a warning is logged. Valid surrounding entries are still parsed.

### Implementation for User Story 4

- [ ] T037 [US4] Implement skip-and-log logic in `src/SunnySunday.Core/Parsing/ClippingsParser.cs`: wrap per-entry parsing in a try-catch or validation gate. If an entry has fewer than 2 lines, or the metadata line does not match the expected regex, log a warning via `ILogger` with the 1-based entry index, a descriptive reason (e.g., "Missing metadata line", "Unrecognized clipping type"), and an excerpt of the raw entry text (first 200 characters). Continue to the next entry.
- [ ] T038 [US4] Implement best-effort parsing for partial entries: if the title line has no author parentheses, still parse the entry with `Author = null` (do not skip). If the date cannot be parsed, still parse the entry with `AddedOn = null`. Only skip entries where the structure is completely unrecognizable (< 2 lines, or metadata regex fails entirely).
- [ ] T039 [US4] Handle edge cases for empty and whitespace-only entries: blank lines between separators should be silently skipped (no warning logged for completely empty entries). Entries with only whitespace content (no title line) should also be silently skipped.
- [ ] T040 [US4] Run `dotnet test src/SunnySunday.Tests/SunnySunday.Tests.csproj --filter "FullyQualifiedName~Parsing"` — all US1, US2, US3, and US4 tests must pass.

**Checkpoint**: Parser is robust against malformed input. All previous stories still pass.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Edge case hardening, performance validation, and documentation

- [ ] T041 [P] Write edge case tests in `src/SunnySunday.Tests/Parsing/ClippingsParserTests.cs`: (a) input with UTF-8 BOM prefix parses correctly (StreamReader handles BOM automatically, but verify via file-path overload with a BOM-prefixed temp file); (b) book titles and authors with Unicode characters (CJK, diacritics, RTL) parse correctly; (c) highlight text with special characters (quotes, angle brackets, ampersands) is preserved verbatim.
- [ ] T042 [P] Write performance test in `src/SunnySunday.Tests/Parsing/ClippingsParserTests.cs`: programmatically generate a `My Clippings.txt` string with 10,000 clipping entries, parse via `StringReader`, and assert completion within 5 seconds (SC-005). Use `[Fact]` with a `Stopwatch` or `[Fact(Timeout = 5000)]`.
- [ ] T043 [P] Update `docs/ARCHITECTURE.md` to document the `SunnySunday.Core/Parsing/` component: its purpose (pure function parser for My Clippings.txt), public API surface (`ClippingsParser.ParseAsync`), key types (`ParseResult`, `ParsedBook`, `ParsedHighlight`), and design decisions (no dependencies, streaming, skip-and-log, notes as `[my note]`-prefixed highlights).
- [ ] T044 Run full `dotnet test src/SunnySunday.Tests/SunnySunday.Tests.csproj` — all tests (including existing SchemaBootstrapTests) must pass. Run `dotnet build src/SunnySunday.slnx` clean. Validate that the quickstart.md code examples in `specs/003-highlight-parser/quickstart.md` are consistent with the implemented API.

---

## Dependencies & Execution Order

### Phase Dependencies

```
Phase 1: Setup ──────────────────────────────► Phase 2: Foundational (Data Types)
                                                         │
                                                         ▼
                                              ┌──────────────────────┐
                                              │  Phase 3: US1 (P1)   │ 🎯 MVP
                                              │  Parse Highlights     │
                                              └──────────┬───────────┘
                                                         │
                                          ┌──────────────┼──────────────┐
                                          ▼              ▼              ▼
                                   Phase 4: US2   Phase 5: US3   Phase 6: US4
                                   Deduplicate    Group by Book  Malformed
                                          │              │              │
                                          └──────────────┼──────────────┘
                                                         ▼
                                              Phase 7: Polish
```

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 — BLOCKS all user stories
- **US1 (Phase 3)**: Depends on Phase 2 — BLOCKS US2, US3, US4
- **US2 (Phase 4)**: Depends on US1 — can run in parallel with US3 and US4
- **US3 (Phase 5)**: Depends on US1 — can run in parallel with US2 and US4
- **US4 (Phase 6)**: Depends on US1 — can run in parallel with US2 and US3
- **Polish (Phase 7)**: Depends on all user stories being complete

### Within Each User Story

1. Tests MUST be written and FAIL before implementation begins
2. Implementation tasks are sequential (each builds on previous)
3. Final task in each phase is a test-run gate

### Parallel Opportunities

- **Phase 2**: All type definition tasks (T002–T007) can run in parallel — each is a separate file
- **Phase 3**: All test-writing tasks (T009–T013) can run in parallel — different test methods
- **Phase 4–6**: US2, US3, US4 phases can run in parallel with each other (different concerns in the same parser file, but different logic sections)
- **Phase 7**: Edge case tests (T041), perf test (T042), and docs (T043) can run in parallel

---

## Parallel Example: Phase 2 (Foundational)

```
# All type files can be created simultaneously:
T002: ClippingType.cs
T003: RawClipping.cs
T004: ParsedHighlight.cs
T005: ParsedBook.cs
T006: ParseWarning.cs
T007: ParseResult.cs
# Then verify build:
T008: dotnet build
```

## Parallel Example: User Story 1 Tests

```
# All test methods can be written simultaneously:
T009: Basic parsing test
T010: Multi-line highlight test
T011: Title/author edge cases
T012: Metadata parsing + bookmark filter
T013: File-path overload test
# Then implement sequentially: T014 → T015 → T016 → T017 → T018 → T019 → T020
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational data types
3. Complete Phase 3: User Story 1 — basic parsing
4. **STOP and VALIDATE**: Parser extracts highlights from valid input
5. This is a usable parser even without dedup/grouping refinement/error handling

### Incremental Delivery

1. Setup + Foundational → types compile
2. US1 → Parser works on valid input (MVP!)
3. US2 → Duplicates removed → higher data quality
4. US3 → Highlights grouped by book → ready for sync API
5. US4 → Robust against malformed input → production-ready
6. Polish → Edge cases, performance, docs → ship-ready

### Single-Developer Flow

Since all code lives in two files (`ClippingsParser.cs` + `ClippingsParserTests.cs`), the recommended flow is sequential by phase: Phase 1 → 2 → 3 → 4 → 5 → 6 → 7. Within each phase, write all tests first, then implement.

---

## Summary

| Metric | Count |
|--------|-------|
| **Total tasks** | 44 |
| **Phase 1 — Setup** | 1 |
| **Phase 2 — Foundational** | 7 |
| **Phase 3 — US1 (P1)** | 12 |
| **Phase 4 — US2 (P2)** | 6 |
| **Phase 5 — US3 (P3)** | 5 |
| **Phase 6 — US4 (P4)** | 9 |
| **Phase 7 — Polish** | 4 |
| **Parallelizable tasks** | 27 |
| **Source files created** | 8 (7 in Parsing/, 1 test file) |

## Notes

- All parser code uses `namespace SunnySunday.Core.Parsing;` file-scoped namespace
- All test code uses `namespace SunnySunday.Tests.Parsing;` file-scoped namespace
- `RawClipping` is `internal` — only `ClippingsParser` uses it
- All public output types (`ParsedHighlight`, `ParsedBook`, `ParseWarning`, `ParseResult`) are immutable records
- No new NuGet packages required — parser uses only .NET BCL (`System.IO`, `System.Text.RegularExpressions`, `System.Globalization`)
- No changes to existing `SunnySunday.Core/Models/` classes
- Commit after each completed phase or logical task group
