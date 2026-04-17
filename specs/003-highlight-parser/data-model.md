# Data Model: Highlight Parser

**Feature**: 003-highlight-parser
**Phase**: 1 — Design
**Date**: 2026-04-17

## Entity Overview

This feature introduces **parser-specific types** that live in `SunnySunday.Cli/Parsing/`. The parser is CLI-exclusive logic and does not need to be shared with the server.

```
ParseResult
├── List<ParsedBook>
│   ├── string Title
│   ├── string? Author
│   └── List<ParsedHighlight>
│       ├── string Text
│       ├── string? Location
│       └── DateTimeOffset? AddedOn
├── int TotalEntriesProcessed
└── int DuplicatesRemoved
```

Parse warnings (malformed entries) are logged to the console via Serilog — not collected in the result.

---

## Entities

### RawClipping

An intermediate representation of a single entry from `My Clippings.txt`, before deduplication or grouping. Internal to the parser — not exposed in the public API.

| Property | Type | Constraints | Notes |
|----------|------|-------------|-------|
| `Title` | `string` | Non-empty | Book title extracted from line 1 |
| `Author` | `string?` | Nullable | Author from last `(...)` on line 1; null if absent |
| `IsNote` | `bool` | Required | True if the metadata line says "Note"; used to prepend `[my note]` prefix |
| `Location` | `string?` | Nullable | Raw location string (e.g., `Location 100-105`) |
| `AddedOn` | `DateTimeOffset?` | Nullable | Parsed date; null if date parsing fails |
| `Text` | `string` | May be empty | Highlight/note text; empty for bookmarks |

**Validation rules**:
- `Title` must be non-empty after trimming
- Bookmarks (detected from metadata line) are filtered out during grouping (they have no text)
- Notes have `[my note]` prepended to their text before output

---

### ParsedHighlight

A single highlight in the final output, after deduplication. Notes are included as highlights with a `[my note]` prefix on their text. Immutable.

| Property | Type | Constraints | Notes |
|----------|------|-------------|-------|
| `Text` | `string` | Non-empty | The highlight text. Notes have `[my note]` prefix. |
| `Location` | `string?` | Nullable | Raw location string for informational use |
| `AddedOn` | `DateTimeOffset?` | Nullable | When the clipping was created on the Kindle |

---

### ParsedBook

A book with its associated highlights, after deduplication and grouping. Immutable.

| Property | Type | Constraints | Notes |
|----------|------|-------------|-------|
| `Title` | `string` | Non-empty | Book title |
| `Author` | `string?` | Nullable | Author name; null if not present in file |
| `Highlights` | `IReadOnlyList<ParsedHighlight>` | Non-empty | At least one highlight (empty books are not emitted) |

**Invariants**:
- A `ParsedBook` is never emitted with zero highlights
- Books are identified by the `(Title, Author)` pair for grouping purposes

---

### ParseResult

The top-level output of the parser. Immutable.

| Property | Type | Constraints | Notes |
|----------|------|-------------|-------|
| `Books` | `IReadOnlyList<ParsedBook>` | Non-null | May be empty if no valid highlights found |
| `TotalEntriesProcessed` | `int` | >= 0 | Total entries encountered (including skipped) |
| `DuplicatesRemoved` | `int` | >= 0 | Number of duplicate entries removed |

Parse warnings for malformed entries are logged to the console (via `ILogger` / Serilog) during parsing — they are not part of the result structure.

---

## Relationship to Existing Models

The parser types are **separate from** the persistence models in `SunnySunday.Core/Models/`:

| Parser Type | Persistence Model | Mapping (future sync feature) |
|-------------|-------------------|-------------------------------|
| `ParsedBook.Title` | `Book.Title` | Direct copy |
| `ParsedBook.Author` | `Author.Name` | Direct copy; create Author if not exists |
| `ParsedHighlight.Text` | `Highlight.Text` | Direct copy (includes `[my note]` prefix for notes) |
| `ParsedHighlight.Location` | — | Not stored in MVP |
| `ParsedHighlight.AddedOn` | `Highlight.CreatedAt` | Maps to CreatedAt |

The sync feature (future) will be responsible for mapping `ParseResult` → persistence models and inserting into SQLite. This feature does not touch the database.

---

## File Layout

```
src/SunnySunday.Core/
└── Parsing/
    ├── RawClipping.cs         # Internal intermediate type
    ├── ParsedHighlight.cs     # Public output type
    ├── ParsedBook.cs          # Public output type
    ├── ParseResult.cs         # Public top-level result
    └── ClippingsParser.cs     # Static parser class
```
