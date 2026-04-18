# Data Model: REST API & Storage Layer

**Feature**: 004-rest-api-storage
**Phase**: 1 — Design
**Date**: 2026-04-18

## Entity Overview

This feature does **not** introduce new database tables. All tables were defined in feature 002 (core-infrastructure) and already exist via `SchemaBootstrap`. This feature introduces:

1. **Request/Response DTOs** — API contract types in `SunnySunday.Core/Contracts/`
2. **Data access layer** — Repository-style classes using Dapper against the existing schema

## Existing Database Schema (reference)

```
users            (id, kindle_email, created_at)
authors          (id, name)
books            (id, user_id, author_id, title)
highlights       (id, user_id, book_id, text, weight, excluded, last_seen, delivery_count, created_at)
excluded_books   (id, user_id, book_id, excluded_at)
excluded_authors (id, user_id, author_id, excluded_at)
settings         (user_id, schedule, delivery_day, delivery_time, count)
```

## Existing Domain Models (reference)

Located in `SunnySunday.Server/Models/`:

- `Highlight` — `Id, UserId, BookId, Text, Weight, Excluded, LastSeen, DeliveryCount, CreatedAt`
- `Book` — `Id, UserId, AuthorId, Title`
- `Author` — `Id, Name`
- `User` — `Id, KindleEmail, CreatedAt`
- `Settings` — `UserId, Schedule, DeliveryDay, DeliveryTime, Count`

---

## API Contract Types (DTOs)

All DTOs live in `SunnySunday.Core/Contracts/`, shared between Server and CLI. Both projects already reference `SunnySunday.Core`.

### Sync Contracts

#### `SyncRequest`

Payload sent by the CLI after parsing a clippings file. Mirrors the structure of `ParseResult`.

| Property | Type | Constraints | Notes |
|----------|------|-------------|-------|
| `Books` | `List<SyncBookRequest>` | Required, non-null | May be empty (valid edge case) |

#### `SyncBookRequest`

A book with its highlights in the sync payload.

| Property | Type | Constraints | Notes |
|----------|------|-------------|-------|
| `Title` | `string` | Required, non-empty | Book title from clippings |
| `Author` | `string?` | Nullable | Author name; null → "Unknown Author" |
| `Highlights` | `List<SyncHighlightRequest>` | Required, non-empty | At least one highlight per book |

#### `SyncHighlightRequest`

A single highlight in the sync payload.

| Property | Type | Constraints | Notes |
|----------|------|-------------|-------|
| `Text` | `string` | Required, non-empty, non-whitespace | Highlight text |
| `AddedOn` | `DateTimeOffset?` | Nullable | Original Kindle timestamp; null → server uses `DateTimeOffset.UtcNow` |

#### `SyncResponse`

Summary returned after a successful import.

| Property | Type | Constraints | Notes |
|----------|------|-------------|-------|
| `NewHighlights` | `int` | >= 0 | Highlights inserted |
| `DuplicateHighlights` | `int` | >= 0 | Highlights skipped (already existed) |
| `NewBooks` | `int` | >= 0 | Books created |
| `NewAuthors` | `int` | >= 0 | Authors created |

---

### Settings Contracts

#### `SettingsResponse`

Returned by `GET /settings`.

| Property | Type | Constraints | Notes |
|----------|------|-------------|-------|
| `Schedule` | `string` | "daily" or "weekly" | |
| `DeliveryDay` | `string?` | Nullable | Day name when schedule=weekly |
| `DeliveryTime` | `string` | HH:mm format | |
| `Count` | `int` | 1–15 | Highlights per recap |
| `KindleEmail` | `string` | May be empty | From users table |

#### `UpdateSettingsRequest`

Sent by `PUT /settings`. All fields optional — only provided fields are updated.

| Property | Type | Constraints | Notes |
|----------|------|-------------|-------|
| `Schedule` | `string?` | "daily" or "weekly" if provided | |
| `DeliveryDay` | `string?` | Valid day name if schedule=weekly | |
| `DeliveryTime` | `string?` | HH:mm format if provided | |
| `Count` | `int?` | 1–15 if provided | |
| `KindleEmail` | `string?` | Valid email format if provided | |

---

### Status Contracts

#### `StatusResponse`

Returned by `GET /status`.

| Property | Type | Constraints | Notes |
|----------|------|-------------|-------|
| `TotalHighlights` | `int` | >= 0 | |
| `TotalBooks` | `int` | >= 0 | |
| `TotalAuthors` | `int` | >= 0 | |
| `ExcludedHighlights` | `int` | >= 0 | Individually excluded |
| `ExcludedBooks` | `int` | >= 0 | Book-level exclusions |
| `ExcludedAuthors` | `int` | >= 0 | Author-level exclusions |
| `NextRecap` | `string?` | Nullable | Informational; null if not configured |

---

### Exclusion Contracts

#### `ExclusionsResponse`

Returned by `GET /exclusions`.

| Property | Type | Constraints | Notes |
|----------|------|-------------|-------|
| `Highlights` | `List<ExcludedHighlightDto>` | Non-null | |
| `Books` | `List<ExcludedBookDto>` | Non-null | |
| `Authors` | `List<ExcludedAuthorDto>` | Non-null | |

#### `ExcludedHighlightDto`

| Property | Type | Notes |
|----------|------|-------|
| `Id` | `int` | Highlight ID |
| `Text` | `string` | Truncated to first 100 chars for readability |
| `BookTitle` | `string` | |

#### `ExcludedBookDto`

| Property | Type | Notes |
|----------|------|-------|
| `Id` | `int` | Book ID |
| `Title` | `string` | |
| `AuthorName` | `string` | |
| `HighlightCount` | `int` | Number of highlights in this book |

#### `ExcludedAuthorDto`

| Property | Type | Notes |
|----------|------|-------|
| `Id` | `int` | Author ID |
| `Name` | `string` | |
| `BookCount` | `int` | Number of books by this author |

---

### Weight Contracts

#### `SetWeightRequest`

Sent by `PUT /highlights/{id}/weight`.

| Property | Type | Constraints | Notes |
|----------|------|-------------|-------|
| `Weight` | `int` | 1–5 | New weight value |

#### `WeightedHighlightDto`

Returned in the list from `GET /highlights/weights`.

| Property | Type | Notes |
|----------|------|-------|
| `Id` | `int` | Highlight ID |
| `Text` | `string` | Truncated to first 100 chars |
| `BookTitle` | `string` | |
| `Weight` | `int` | Current weight (non-default only) |

---

## Mapping: Parser Output → Sync Request → Database

```
CLI side (SunnySunday.Cli — references SunnySunday.Core.Contracts directly):
  ParseResult.Books[]           →  SyncRequest.Books[]
  ParsedBook.Title              →  SyncBookRequest.Title
  ParsedBook.Author             →  SyncBookRequest.Author
  ParsedHighlight.Text          →  SyncHighlightRequest.Text
  ParsedHighlight.AddedOn       →  SyncHighlightRequest.AddedOn

Server side (sync endpoint):
  SyncBookRequest.Author        →  authors.name (find-or-create)
  SyncBookRequest.Title         →  books.title (find-or-create, scoped to user+author)
  SyncHighlightRequest.Text     →  highlights.text (deduplicate by user+book+text)
  SyncHighlightRequest.AddedOn  →  highlights.created_at (fallback: UTC now)
```

---

## File Layout

```
src/SunnySunday.Server/
├── Contracts/
│   ├── SyncRequest.cs
│   ├── SyncResponse.cs
│   ├── SettingsResponse.cs
│   ├── UpdateSettingsRequest.cs
│   ├── StatusResponse.cs
│   ├── ExclusionsResponse.cs
│   ├── SetWeightRequest.cs
│   └── WeightedHighlightDto.cs
├── Endpoints/
│   ├── SyncEndpoints.cs
│   ├── StatusEndpoints.cs
│   ├── SettingsEndpoints.cs
│   ├── ExclusionEndpoints.cs
│   └── WeightEndpoints.cs
├── Data/
│   ├── UserRepository.cs
│   ├── SyncRepository.cs
│   ├── SettingsRepository.cs
│   ├── StatusRepository.cs
│   ├── ExclusionRepository.cs
│   └── WeightRepository.cs
└── Program.cs                      # Updated: DI registrations + MapGroup calls

src/SunnySunday.Tests/
└── Api/
    ├── SyncEndpointTests.cs
    ├── StatusEndpointTests.cs
    ├── SettingsEndpointTests.cs
    ├── ExclusionEndpointTests.cs
    ├── WeightEndpointTests.cs
    └── TestWebApplicationFactory.cs
```
