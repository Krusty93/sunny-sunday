# Quick Start: Highlight Parser

**Feature**: 003-highlight-parser
**Date**: 2026-04-17

---

## Prerequisites

- .NET 10 SDK installed (`dotnet --version` → `10.x`)
- Repository cloned and solution builds: `dotnet build src/SunnySunday.slnx`

## Project Structure

All parser code lives in `SunnySunday.Core`:

```
src/SunnySunday.Core/
├── Models/           # Existing persistence models (DO NOT MODIFY)
└── Parsing/          # NEW — parser types and logic
    ├── ClippingType.cs
    ├── RawClipping.cs
    ├── ParsedHighlight.cs
    ├── ParsedBook.cs
    ├── ParseWarning.cs
    ├── ParseResult.cs
    └── ClippingsParser.cs

src/SunnySunday.Tests/
└── Parsing/          # NEW — parser unit tests
    └── ClippingsParserTests.cs
```

## Build & Test

```bash
# Build everything
dotnet build src/SunnySunday.slnx

# Run tests
dotnet test src/SunnySunday.Tests/SunnySunday.Tests.csproj

# Run tests with verbose output
dotnet test src/SunnySunday.Tests/SunnySunday.Tests.csproj --verbosity normal
```

## Usage Example

```csharp
using SunnySunday.Core.Parsing;

// From a file path
var result = await ClippingsParser.ParseAsync("/path/to/My Clippings.txt");

// From a TextReader (useful in tests)
using var reader = new StringReader(clippingsText);
var result = await ClippingsParser.ParseAsync(reader);

// Inspect results
foreach (var book in result.Books)
{
    Console.WriteLine($"{book.Title} by {book.Author ?? "Unknown"}");
    foreach (var highlight in book.Highlights)
    {
        Console.WriteLine($"  - {highlight.Text}");
    }
}

// Check for warnings
foreach (var warning in result.Warnings)
{
    Console.WriteLine($"Warning at entry {warning.EntryIndex}: {warning.Reason}");
}
```

## Test Data

Create test input using the exact Kindle format:

```csharp
var input = """
    The Great Gatsby (F. Scott Fitzgerald)
    - Your Highlight on Location 100-105 | Added on Thursday, January 1, 2026 12:00:00 AM

    In my younger and more vulnerable years my father gave me some advice.
    ==========
    1984 (Orwell, George)
    - Your Highlight on Location 200-210 | Added on Friday, January 2, 2026 1:30:00 PM

    War is peace. Freedom is slavery. Ignorance is strength.
    ==========
    """;

using var reader = new StringReader(input);
var result = await ClippingsParser.ParseAsync(reader);
```

## Key Design Decisions

1. **Pure function** — `ClippingsParser` is a static class with no state, no dependencies, no side effects
2. **TextReader input** — accepts `TextReader` for testability; file-path overload is a convenience wrapper
3. **Async API** — `ParseAsync` uses `ReadLineAsync()` for I/O-bound file reading
4. **Immutable output** — all result types are immutable records or have read-only collections
5. **Skip-and-warn** — malformed entries are skipped, never throw; warnings are collected in `ParseResult.Warnings`
6. **Dedup by exact match** — `(Title, Author, Text)` tuple, case-sensitive, using `HashSet`

## What This Feature Does NOT Do

- No REST API endpoints (separate feature)
- No CLI commands (separate feature)
- No database writes (sync feature)
- No user association (sync feature)
