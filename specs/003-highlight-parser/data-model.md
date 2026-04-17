# Data Model: Highlight Parser

**Feature**: 003-highlight-parser
**Phase**: 1 — Design
**Date**: 2026-04-17

## Entity Overview

This feature introduces **parser-specific types** that live in `SunnySunday.Core/Parsing/`. These are distinct from the persistence models in `SunnySunday.Core/Models/` — they represent the raw parsed output before any database interaction.

```
ParseResult
├── List<ParsedBook>
│   ├── string Title
│   ├── string? Author
│   └── List<ParsedHighlight>
│       ├── string Text
│       ├── ClippingType Type
│       ├── string? Location
│       └── DateTimeOffset? AddedOn
└── List<ParseWarning>
    ├── int EntryIndex
    ├── string Reason
    └── string RawExcerpt
```

---

## Entities

### RawClipping

An intermediate representation of a single entry from `My Clippings.txt`, before deduplication or grouping. Internal to the parser — not exposed in the public API.

| Property | Type | Constraints | Notes |
|----------|------|-------------|-------|
| `Title` | `string` | Non-empty | Book title extracted from line 1 |
| `Author` | `string?` | Nullable | Author from last `(...)` on line 1; null if absent |
| `Type` | `ClippingType` | Required | Highlight, Note, or Bookmark |
| `Location` | `string?` | Nullable | Raw location string (e.g., `Location 100-105`) |
| `AddedOn` | `DateTimeOffset?` | Nullable | Parsed date; null if date parsing fails |
| `Text` | `string` | May be empty | Highlight/note text; empty for bookmarks |

**Validation rules**:
- `Title` must be non-empty after trimming
- `Type` must be a recognized value; unrecognized types cause the entry to be skipped with a warning
- Bookmarks (`Type = Bookmark`) are filtered out during grouping (they have no text)

---

### ClippingType

Enum representing the type of clipping entry.

| Value | Description |
|-------|-------------|
| `Highlight` | Text highlighted by the user |
| `Note` | User-written annotation attached to a location |
| `Bookmark` | Position-only marker with no text content |

---

### ParsedHighlight

A single highlight or note in the final output, after deduplication. Immutable.

| Property | Type | Constraints | Notes |
|----------|------|-------------|-------|
| `Text` | `string` | Non-empty | The highlight or note text |
| `Type` | `ClippingType` | `Highlight` or `Note` | Never `Bookmark` (filtered) |
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

### ParseWarning

A warning generated when a clipping entry cannot be parsed.

| Property | Type | Constraints | Notes |
|----------|------|-------------|-------|
| `EntryIndex` | `int` | >= 1 | 1-based ordinal position in the file |
| `Reason` | `string` | Non-empty | Human-readable description of why parsing failed |
| `RawExcerpt` | `string` | Truncated | First 200 characters of the raw entry text |

---

### ParseResult

The top-level output of the parser. Immutable.

| Property | Type | Constraints | Notes |
|----------|------|-------------|-------|
| `Books` | `IReadOnlyList<ParsedBook>` | Non-null | May be empty if no valid highlights found |
| `Warnings` | `IReadOnlyList<ParseWarning>` | Non-null | May be empty if all entries parsed successfully |
| `TotalEntriesProcessed` | `int` | >= 0 | Total entries encountered (including skipped) |
| `DuplicatesRemoved` | `int` | >= 0 | Number of duplicate entries removed |

---

## Relationship to Existing Models

The parser types are **separate from** the persistence models in `SunnySunday.Core/Models/`:

| Parser Type | Persistence Model | Mapping (future sync feature) |
|-------------|-------------------|-------------------------------|
| `ParsedBook.Title` | `Book.Title` | Direct copy |
| `ParsedBook.Author` | `Author.Name` | Direct copy; create Author if not exists |
| `ParsedHighlight.Text` | `Highlight.Text` | Direct copy |
| `ParsedHighlight.Type` | — | Not stored (all are treated as highlights in DB) |
| `ParsedHighlight.Location` | — | Not stored in MVP |
| `ParsedHighlight.AddedOn` | `Highlight.CreatedAt` | Maps to CreatedAt |

The sync feature (future) will be responsible for mapping `ParseResult` → persistence models and inserting into SQLite. This feature does not touch the database.

---

## File Layout

```
src/SunnySunday.Core/
└── Parsing/
    ├── ClippingType.cs        # Enum
    ├── RawClipping.cs         # Internal intermediate type
    ├── ParsedHighlight.cs     # Public output type
    ├── ParsedBook.cs          # Public output type
    ├── ParseWarning.cs        # Public output type
    ├── ParseResult.cs         # Public top-level result
    └── ClippingsParser.cs     # Static parser class
```
