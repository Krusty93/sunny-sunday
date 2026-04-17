# Feature Specification: Highlight Parser

**Feature Branch**: `003-highlight-parser`  
**Created**: 2026-04-17  
**Status**: Draft  
**Input**: User description: "Parse the My Clippings.txt file exported from Kindle and extract structured highlight data. This covers PRD requirements FR-01 (Parse My Clippings.txt and extract highlights and annotations), FR-02 (Deduplicate highlights), and FR-03 (Group highlights by book title and author)."

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Parse Highlights from My Clippings.txt (Priority: P1)

A Kindle user connects their device via USB and runs the `sunny sync` command, pointing it at the `My Clippings.txt` file. The system reads the file, parses each clipping entry, and extracts structured highlight data including the book title, author, and highlight text. The user sees a summary of how many highlights were successfully parsed.

**Why this priority**: This is the foundational capability. Without parsing, no other feature (deduplication, grouping, sync) can function. It directly addresses PRD FR-01.

**Independent Test**: Can be fully tested by providing a sample `My Clippings.txt` file and verifying that all valid highlights are extracted with correct book/author/text associations.

**Acceptance Scenarios**:

1. **Given** a standard `My Clippings.txt` file with 50 highlights across 5 books, **When** the parser processes the file, **Then** all 50 highlights are extracted with correct book title, author, and highlight text.
2. **Given** a clipping entry with a multi-line highlight (text spanning multiple lines), **When** the parser processes the entry, **Then** the full multi-line text is captured as a single highlight.
3. **Given** a `My Clippings.txt` file containing highlights, notes, and bookmarks, **When** the parser processes the file, **Then** highlights are extracted as-is, notes are extracted with a `[my note]` prefix prepended to their text, and bookmarks (position-only entries with no text) are skipped.
4. **Given** a clipping entry with metadata containing location range and timestamp, **When** the parser processes the entry, **Then** the location and date information are extracted alongside the highlight text.

---

### User Story 2 — Deduplicate Highlights (Priority: P2)

After parsing, the system identifies and removes duplicate highlights. Kindle appends a new entry every time the user re-highlights or extends a highlight, resulting in duplicates. The user expects only unique highlights in the output, with no repeated content for the same book.

**Why this priority**: Deduplication is essential for data quality. Without it, users would receive duplicate highlights in their recaps, degrading the experience. Directly addresses PRD FR-02.

**Independent Test**: Can be tested by providing a file with known duplicates and verifying that the output contains only unique highlights.

**Acceptance Scenarios**:

1. **Given** a file where the same highlight text appears twice for the same book and author, **When** deduplication runs, **Then** only one instance of the highlight is retained.
2. **Given** two highlights with identical text but from different books, **When** deduplication runs, **Then** both highlights are retained (they are not duplicates).
3. **Given** two highlights from the same book where one is a substring of the other (e.g., re-highlighted with a wider selection), **When** deduplication runs, **Then** both are retained as distinct highlights (only exact text matches are considered duplicates).
4. **Given** a file with 100 clippings where 20 are duplicates, **When** deduplication runs, **Then** the output contains exactly 80 unique highlights.

---

### User Story 3 — Group Highlights by Book and Author (Priority: P3)

After parsing and deduplication, highlights are organized by book. Each book is identified by its title and author. The user receives a structured output where highlights are grouped under their respective books, ready for the sync API.

**Why this priority**: Grouping is necessary for the sync API to correctly associate highlights with books and authors in the server database. Directly addresses PRD FR-03.

**Independent Test**: Can be tested by verifying that parsed highlights are correctly grouped under book/author pairs.

**Acceptance Scenarios**:

1. **Given** 30 highlights across 3 books by different authors, **When** grouping runs, **Then** the output contains 3 book groups, each with its correct highlights.
2. **Given** highlights from two different books by the same author, **When** grouping runs, **Then** the highlights are grouped into two separate book entries (grouped by title + author, not author alone).
3. **Given** the grouped output, **When** the structure is inspected, **Then** each book group contains the book title, author name, and a list of its associated highlights.

---

### User Story 4 — Handle Malformed Entries Gracefully (Priority: P4)

Real-world `My Clippings.txt` files may contain malformed or incomplete entries due to Kindle firmware bugs, file corruption, or manual editing. The parser skips these entries without crashing and logs a warning so the user can investigate if needed.

**Why this priority**: Robustness is important for user trust, but only after core parsing works correctly. A parser that crashes on unexpected input would be unusable in practice.

**Independent Test**: Can be tested by providing a file with intentionally malformed entries mixed with valid ones and verifying that valid highlights are still extracted.

**Acceptance Scenarios**:

1. **Given** a file with one malformed entry (missing separator, truncated text) among 10 valid entries, **When** the parser processes the file, **Then** 10 valid highlights are extracted and the malformed entry is skipped with a warning logged to the console.
2. **Given** an empty `My Clippings.txt` file, **When** the parser processes the file, **Then** the result is an empty collection with no errors.
3. **Given** a file containing only separators and no actual clipping content, **When** the parser processes the file, **Then** the result is an empty collection with no errors.
4. **Given** a file where a clipping entry is missing the author on the title line, **When** the parser processes the entry, **Then** the entry is parsed with best-effort extraction (title without author), not skipped entirely.

---

### Edge Cases

- What happens when the file contains a UTF-8 BOM (byte order mark) at the start?
- How does the parser handle extremely large files (50,000+ clippings)?
- What happens when a book title contains special characters (quotes, parentheses, Unicode)?
- How does the parser handle clippings with empty highlight text (blank content between metadata and separator)?
- What happens when the title/author line uses unexpected formatting (e.g., missing parentheses around author name, or author name with commas like "Last, First")?
- How does the parser handle entries in different languages (CJK characters, RTL scripts, diacritics)?
- What happens when the same highlight text appears for the same book but with different location metadata (re-highlighted at a different page)?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST parse standard `My Clippings.txt` files using the `==========` separator to split individual clipping entries.
- **FR-002**: System MUST extract the book title and author from the first line of each clipping entry.
- **FR-003**: System MUST extract the full highlight or note text, including multi-line content, from each clipping entry.
- **FR-004**: System MUST extract metadata (location range and date) from the second line of each clipping entry. The metadata line is also used internally to detect the entry type (highlight, note, or bookmark) but no type field is exposed in the output.
- **FR-005**: System MUST skip bookmarks (position-only entries with no text). Notes MUST be treated as highlights with a `[my note]` prefix prepended to their text. There is no separate type or special handling for notes anywhere in CLI or server.
- **FR-006**: System MUST deduplicate highlights by matching on the combination of book title, author, and highlight text (exact match, case-sensitive).
- **FR-007**: System MUST group deduplicated highlights by book, where a book is identified by the pair of title and author name.
- **FR-008**: System MUST skip malformed clipping entries that cannot be parsed and continue processing the remainder of the file.
- **FR-009**: System MUST log skipped entries as warnings to the console, including the approximate position in the file and the reason for skipping.
- **FR-010**: System MUST produce an output structure organized by book, where each book contains its title, author name, and list of highlight texts (notes included with `[my note]` prefix), suitable for the sync API payload.
- **FR-011**: System MUST handle empty files and files with no valid clippings by returning an empty result without errors.

### Key Entities

- **Clipping**: A single raw entry from `My Clippings.txt`, containing a title/author line, a metadata line, and content text. Represents the unprocessed input before deduplication.
- **Highlight**: A parsed, deduplicated piece of text from the user's Kindle. Notes are stored as highlights with a `[my note]` prefix — there is no type distinction. Associated with exactly one book.
- **Book**: A publication identified by its title and author name. Contains one or more highlights after grouping.
- **Author**: A person identified by name who wrote one or more books.
- **Parse Result**: The complete output of the parsing process — a collection of books with their associated highlights.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: All valid highlights and notes are extracted from a standard `My Clippings.txt` file with 100% accuracy — no valid entry is missed or incorrectly parsed.
- **SC-002**: Duplicate highlights are reduced to a single entry per unique combination, with zero false positives (no unique highlights incorrectly removed) and zero false negatives (no true duplicates retained).
- **SC-003**: Highlights are correctly grouped by book, with each book associated with its correct author — verified across files containing 20+ distinct books.
- **SC-004**: Malformed entries are skipped without affecting the parsing of valid entries — verified by processing files with 10% intentionally corrupted entries and confirming all valid entries are still extracted.
- **SC-005**: A file with 10,000 clippings is parsed, deduplicated, and grouped within 5 seconds.
- **SC-006**: The parsed output structure maps directly to the expected sync API payload without additional transformation.

## Assumptions

- `My Clippings.txt` uses UTF-8 encoding, which is standard across all Kindle models. The parser handles an optional UTF-8 BOM at the start of the file.
- The file format uses `==========` (ten equals signs) on a dedicated line as the separator between clipping entries. This format has been stable across Kindle generations for over a decade.
- Each clipping entry follows the structure: line 1 = book title and author, line 2 = metadata (type, location, date), line 3 = blank, line 4+ = content text.
- Author name appears in parentheses at the end of the title line (e.g., `Book Title (Author Name)`). If parentheses are absent, the entire line is treated as the title with an unknown author.
- Deduplication uses exact text matching (case-sensitive). Fuzzy matching or near-duplicate detection is out of scope for MVP.
- Bookmarks (entries with no highlight text, only a position marker) are skipped since they carry no textual content useful for recaps.
- Notes (user-written annotations) are treated as highlights with a `[my note]` prefix prepended to their text. There is no separate type enum or special handling for notes in CLI or server — they are simply highlights whose text starts with `[my note]`.
- The parser is a pure function with no side effects: it reads a file or text stream and produces a structured result. It has no dependencies on the server, database, or network.
- Location and date metadata are extracted for informational purposes but are not used for deduplication or grouping in MVP.
- Single-user context: the parser does not associate highlights with a specific user ID (user association happens at the sync level).
- The parser is client-side logic used by the CLI during sync — it runs locally on the user's machine, not on the server.
