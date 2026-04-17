# Research: Highlight Parser

**Feature**: 003-highlight-parser
**Phase**: 0 — Outline & Research
**Date**: 2026-04-17

---

## 1. My Clippings.txt Format Analysis

### Decision: Use line-based parsing with `==========` separator

**Rationale**: The Kindle `My Clippings.txt` file has a fixed, well-documented structure that has remained stable across Kindle generations since the Kindle 2 (2009). The format is simple enough to parse with line-based splitting — no grammar or parser combinator library is needed.

**Alternatives considered**:
- Regex-only parsing — rejected because multi-line content makes pure regex unwieldy and fragile
- Parser combinator library (e.g., Sprache, Pidgin) — rejected because the format is too simple to justify the dependency; YAGNI per constitution

### Format specification

Each entry in `My Clippings.txt` follows this exact structure:

```
{Title} ({Author})\r\n
- Your {Type} on {Location} | Added on {Date}\r\n
\r\n
{Content (zero or more lines)}\r\n
==========\r\n
```

**Field details**:

| Field | Format | Notes |
|-------|--------|-------|
| Title | Free text | May contain parentheses, quotes, Unicode |
| Author | Inside last `(...)` on title line | May be `Last, First` format; may be absent |
| Type | `Highlight`, `Note`, or `Bookmark` | Prefixed with `Your` |
| Location | `Location NNN-NNN` or `page NNN` | Varies by Kindle model |
| Date | `Added on {DayOfWeek}, {Month} {Day}, {Year} {Time}` | US English locale |
| Content | Multi-line UTF-8 text | Empty for bookmarks |
| Separator | `==========` (10 equals signs) | Always on its own line |

### Line endings

Kindle uses `\r\n` (Windows-style) line endings regardless of host OS. The parser must handle both `\r\n` and `\n` to be robust (e.g., if the user opens and re-saves the file on macOS/Linux).

### UTF-8 BOM

Some Kindle models prepend a UTF-8 BOM (`0xEF 0xBB 0xBF`) to the file. `StreamReader` in .NET handles this automatically when using `new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true)` (the default). No special handling needed.

---

## 2. Author Extraction Edge Cases

### Decision: Extract author from the last parenthesized group on the title line

**Rationale**: The Kindle format always places the author inside the last pair of parentheses on the title line. Some book titles legitimately contain parentheses (e.g., `The Art of War (Annotated) (Sun Tzu)`), so we must use the **last** `(...)` group, not the first.

**Alternatives considered**:
- First parenthesized group — rejected because titles may contain parentheses
- All text after last `(` — this is effectively what we do, taking the content between the last `(` and the closing `)`

### Edge cases handled

| Scenario | Input | Title | Author |
|----------|-------|-------|--------|
| Standard | `The Great Gatsby (F. Scott Fitzgerald)` | `The Great Gatsby` | `F. Scott Fitzgerald` |
| Last, First | `1984 (Orwell, George)` | `1984` | `Orwell, George` |
| Title with parens | `The Art of War (Annotated) (Sun Tzu)` | `The Art of War (Annotated)` | `Sun Tzu` |
| No author | `My Personal Notes` | `My Personal Notes` | `null` / unknown |
| Empty parens | `Some Book ()` | `Some Book` | `null` / unknown (empty treated as absent) |
| Unicode author | `道德经 (老子)` | `道德经` | `老子` |

---

## 3. Metadata Line Parsing

### Decision: Regex-based extraction of type, location, and date

**Rationale**: The metadata line has a predictable structure: `- Your {Type} on {Location} | Added on {Date}`. A single regex with named capture groups handles this cleanly.

**Pattern**: `^- Your (?<type>Highlight|Note|Bookmark) on (?<location>.+?) \| Added on (?<date>.+)$`

### Date parsing

The Kindle date format is: `Thursday, January 1, 2026 12:00:00 AM`

This maps to .NET format string: `dddd, MMMM d, yyyy h:mm:ss tt` with `CultureInfo.InvariantCulture`.

**Edge case**: Some Kindle firmware versions use slightly different date formats (e.g., 24-hour time, different locale). The parser should attempt parsing with the known format and fall back gracefully (store raw string if parsing fails, log a warning).

---

## 4. Deduplication Strategy

### Decision: Exact case-sensitive match on (title, author, text) tuple

**Rationale**: Per spec FR-006, deduplication uses exact text matching. This is the simplest approach and avoids false positives. Kindle re-highlights produce exact duplicates, so fuzzy matching is unnecessary for MVP.

**Alternatives considered**:
- Fuzzy/Levenshtein matching — rejected as over-engineering for MVP; YAGNI
- Hash-based dedup — considered and adopted as implementation detail: use `HashSet<(string, string, string)>` for O(1) lookup
- Substring containment (shorter highlight is subset of longer) — rejected per spec acceptance scenario: "both are retained as distinct highlights"

### Implementation approach

Use a `HashSet` keyed on `(BookTitle, AuthorName, HighlightText)` tuple. Process clippings in file order; skip any entry whose key already exists. This preserves the first occurrence (which is the original highlight; re-highlights come later in the file).

---

## 5. Performance Considerations

### Decision: Stream-based parsing with single pass

**Rationale**: The spec requires 10,000 clippings in < 5 seconds. A single-pass streaming approach (read line by line, accumulate per-entry state, emit on separator) is both simple and fast. No need for memory-mapping or parallel processing.

**Alternatives considered**:
- Read entire file into memory then split — acceptable for typical file sizes (< 50 MB) but streaming is equally simple and uses less memory
- Parallel parsing — rejected as unnecessary; a single-threaded pass over 10K entries takes milliseconds, well under the 5-second target
- Memory-mapped files — rejected as over-engineering

### Memory estimate

- Average clipping: ~500 bytes
- 10,000 clippings: ~5 MB raw text
- Parsed output (strings are interned per book): ~8-10 MB
- Well within acceptable memory for a CLI tool

---

## 6. Error Handling & Warnings

### Decision: Skip-and-warn strategy with structured warning objects

**Rationale**: Per spec FR-008 and FR-009, malformed entries must be skipped with warnings. The parser should never throw on bad input — it returns a result containing both the successfully parsed data and a list of warnings.

**Warning information includes**:
- Entry index (ordinal position in the file, 1-based)
- Raw text of the failed entry (first N characters, truncated for large entries)
- Reason for skipping (e.g., "Missing metadata line", "Unrecognized clipping type")

---

## 7. .NET-Specific Best Practices

### String handling
- Use `StringComparison.Ordinal` for exact matching (deduplication)
- Use `ReadOnlySpan<char>` or `AsSpan()` for substring operations where allocation matters (metadata parsing)
- Avoid excessive string allocations in the hot path (line-by-line parsing)

### Stream vs file path
- Accept `TextReader` as the primary input (testable, flexible)
- Provide a convenience overload that takes a file path and creates a `StreamReader`
- In tests, use `StringReader` — no temp files needed

### Async
- Provide async API (`ParseAsync`) using `StreamReader.ReadLineAsync()`
- The parser is I/O-bound (file read), so async is appropriate
- Sync wrapper can be offered for CLI convenience

### Nullable reference types
- Project already has `<Nullable>enable</Nullable>`
- Author can be `null` in parsed clipping when no parentheses found
- All other fields are non-nullable
