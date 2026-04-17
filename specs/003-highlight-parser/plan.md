# Implementation Plan: Highlight Parser

**Branch**: `003-highlight-parser` | **Date**: 2026-04-17 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/003-highlight-parser/spec.md`

## Summary

Parse Kindle `My Clippings.txt` files into structured highlight data. The parser is a pure function in `SunnySunday.Cli/Parsing/` that reads the file line-by-line, extracts book/author/highlight/metadata, deduplicates by exact `(title, author, text)` match, groups highlights by book, and returns an immutable `ParseResult`. Notes are treated as highlights with a `[my note]` prefix. Malformed entries are logged as warnings. No database, no HTTP, no side effects.

## Technical Context

**Language/Version**: C# / .NET 10 (`net10.0` TFM)
**Primary Dependencies**: Microsoft.Extensions.Logging (for `ILogger<T>` — warning logs for malformed entries)
**Storage**: N/A — parser is a pure function with no persistence
**Testing**: xUnit (via `SunnySunday.Tests`)
**Target Platform**: Cross-platform (.NET 10 runtime)
**Project Type**: CLI-exclusive logic in `SunnySunday.Cli`
**Performance Goals**: 10,000 clippings parsed, deduplicated, and grouped in < 5 seconds
**Constraints**: Single-pass streaming, no external dependencies beyond .NET BCL
**Scale/Scope**: Typical file: 500–5,000 clippings; stress test: 50,000+ clippings

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Client/Server Separation | **PASS** | Parser lives in `Cli` (CLI-exclusive); not shared with server |
| II. CLI-First, No GUI | **PASS** | No UI in this feature; parser is logic-only |
| III. Zero-Config Onboarding | **PASS** | No configuration required; parser takes a file path |
| IV. Local Processing Only | **PASS** | Pure local file parsing, no network calls |
| V. Tests Ship with Code | **PASS** | Unit tests included in implementation plan |
| VI. Simplicity / YAGNI | **PASS** | No external dependencies, no parser combinator library, no fuzzy matching |
| Tech: C# / .NET 10 only | **PASS** | All code is C# |
| Tech: No raw Console.WriteLine | **PASS** | Parser returns data; no console output |
| Exclusion: No AI summarization | **PASS** | Not applicable |

**Post-design re-check**: All gates still pass. Parser types are plain C# records/classes with no dependencies. No new NuGet packages introduced.

## Project Structure

### Documentation (this feature)

```text
specs/003-highlight-parser/
├── plan.md              # This file
├── research.md          # My Clippings.txt format analysis & decisions
├── data-model.md        # Parser-specific type definitions
├── quickstart.md        # Developer quick-start guide
└── tasks.md             # Implementation tasks (created by /speckit.tasks)
```

### Source Code (repository root)

```text
src/SunnySunday.Cli/
├── Program.cs
└── Parsing/                   # Parser types and logic
    ├── RawClipping.cs         # Internal intermediate parse result
    ├── ParsedHighlight.cs     # Public: single highlight in output
    ├── ParsedBook.cs          # Public: book with grouped highlights
    ├── ParseResult.cs         # Public: top-level result container
    └── ClippingsParser.cs     # Public: static parser entry point

src/SunnySunday.Core/
└── Models/                    # Existing persistence models (UNCHANGED)
    ├── Author.cs
    ├── Book.cs
    ├── Highlight.cs
    ├── Settings.cs
    └── User.cs

src/SunnySunday.Tests/
└── Parsing/                   # NEW — parser unit tests
    └── ClippingsParserTests.cs
```

**Structure Decision**: Parser code goes into a new `Parsing/` namespace within the existing `SunnySunday.Cli` project (CLI-exclusive, not shared with server). Tests go into the existing `SunnySunday.Tests` project under a `Parsing/` folder. This avoids adding a 5th project (YAGNI).

## Complexity Tracking

No constitution violations. No complexity justification needed.
