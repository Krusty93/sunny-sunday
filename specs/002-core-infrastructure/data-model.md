# Data Model: Core Infrastructure

**Feature**: 002-core-infrastructure
**Phase**: 1 — Design
**Date**: 2026-04-07

## Entity Overview

This feature defines all domain entities for the entire MVP. No entities are added in later features — later features only add behavior on top of this schema.

```
Author 1──* Book 1──* Highlight *──1 User
                                    │
                               *    │
                         Settings ──┘

ExcludedBook    *──1 User
ExcludedAuthor  *──1 User
```

---

## Entities

### Highlight

The core entity. Represents a single text passage marked by a user on their Kindle.

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| `id` | INTEGER | PRIMARY KEY AUTOINCREMENT | |
| `user_id` | INTEGER | NOT NULL, FK → users.id | |
| `book_id` | INTEGER | NOT NULL, FK → books.id | |
| `text` | TEXT | NOT NULL | Raw highlight text from My Clippings.txt |
| `weight` | INTEGER | NOT NULL DEFAULT 3, CHECK(weight BETWEEN 1 AND 5) | User-assigned importance |
| `excluded` | INTEGER | NOT NULL DEFAULT 0 | Boolean: 0=active, 1=excluded |
| `last_seen` | TEXT | NULL | ISO-8601 UTC datetime; NULL if never sent |
| `delivery_count` | INTEGER | NOT NULL DEFAULT 0 | Times sent in a recap |
| `created_at` | TEXT | NOT NULL | ISO-8601 UTC datetime |

**Validation rules**:
- `text` must be non-empty and non-whitespace
- `weight` must be 1–5
- `excluded` treated as boolean; only 0 or 1 valid

**State transitions**:
- `excluded`: 0 → 1 (exclude) and 1 → 0 (re-include) — both transitions allowed via CLI
- `last_seen`: NULL → datetime (set when first delivered; updated on each delivery)
- `weight`: 1–5 freely changeable via CLI

---

### Book

A Kindle book. Parsed from the `My Clippings.txt` metadata.

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| `id` | INTEGER | PRIMARY KEY AUTOINCREMENT | |
| `user_id` | INTEGER | NOT NULL, FK → users.id | |
| `author_id` | INTEGER | NOT NULL, FK → authors.id | |
| `title` | TEXT | NOT NULL | Normalized title from clippings |

**Validation rules**:
- `title` must be non-empty
- (title, user_id) is a logical unique key — enforced by parser deduplication, not DB constraint for MVP

---

### Author

A book author.

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| `id` | INTEGER | PRIMARY KEY AUTOINCREMENT | |
| `name` | TEXT | NOT NULL | Normalized author name from clippings |

**Validation rules**:
- `name` must be non-empty
- `name` is logically unique — enforced by parser deduplication, not DB constraint for MVP

---

### User

A Sunny Sunday user. Single-user for MVP, but modeled as a table for future multi-user support.

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| `id` | INTEGER | PRIMARY KEY AUTOINCREMENT | |
| `kindle_email` | TEXT | NOT NULL UNIQUE | Target Kindle delivery address |
| `created_at` | TEXT | NOT NULL | ISO-8601 UTC datetime |

**Validation rules**:
- `kindle_email` must be a valid email address format

---

### Settings

Per-user delivery configuration.

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| `user_id` | INTEGER | PRIMARY KEY, FK → users.id | 1:1 with users |
| `schedule` | TEXT | NOT NULL DEFAULT 'daily' | 'daily' or 'weekly' |
| `delivery_day` | TEXT | NULL | Day of week: 'monday'…'sunday'. NULL when schedule='daily' |
| `delivery_time` | TEXT | NOT NULL DEFAULT '18:00' | HH:MM local time |
| `count` | INTEGER | NOT NULL DEFAULT 3, CHECK(count BETWEEN 1 AND 15) | Highlights per recap |

**Validation rules**:
- `schedule` must be 'daily' or 'weekly'
- `delivery_day` must be a valid day name when `schedule = 'weekly'`; must be NULL when `schedule = 'daily'`
- `delivery_time` must match HH:MM format (00:00–23:59)
- `count` must be 1–15

---

### ExcludedBook

Records books currently excluded from recaps. Rows are deleted (not soft-deleted) when the user re-includes a book.

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| `id` | INTEGER | PRIMARY KEY AUTOINCREMENT | |
| `user_id` | INTEGER | NOT NULL, FK → users.id | |
| `book_id` | INTEGER | NOT NULL, FK → books.id | |
| `excluded_at` | TEXT | NOT NULL | ISO-8601 UTC datetime |

**Validation rules**:
- (user_id, book_id) is logically unique — inserting a duplicate is a no-op

---

### ExcludedAuthor

Records authors currently excluded from recaps. Rows are deleted (not soft-deleted) when the user re-includes an author.

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| `id` | INTEGER | PRIMARY KEY AUTOINCREMENT | |
| `user_id` | INTEGER | NOT NULL, FK → users.id | |
| `author_id` | INTEGER | NOT NULL, FK → authors.id | |
| `excluded_at` | TEXT | NOT NULL | ISO-8601 UTC datetime |

**Validation rules**:
- (user_id, author_id) is logically unique — inserting a duplicate is a no-op

---

### Logs (Serilog sink)

Auto-created by `Serilog.Sinks.SQLite`. Not a domain entity — listed for schema completeness.

| Column | Type | Notes |
|--------|------|-------|
| `Id` | INTEGER | PRIMARY KEY AUTOINCREMENT |
| `Timestamp` | TEXT | ISO-8601 |
| `Level` | TEXT | Serilog level name |
| `Exception` | TEXT | NULL if no exception |
| `RenderedMessage` | TEXT | Final formatted message |
| `Properties` | TEXT | JSON |

---

## SQLite Schema (DDL)

```sql
CREATE TABLE IF NOT EXISTS users (
    id           INTEGER PRIMARY KEY AUTOINCREMENT,
    kindle_email TEXT    NOT NULL UNIQUE,
    created_at   TEXT    NOT NULL
);

CREATE TABLE IF NOT EXISTS authors (
    id   INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT    NOT NULL
);

CREATE TABLE IF NOT EXISTS books (
    id        INTEGER PRIMARY KEY AUTOINCREMENT,
    user_id   INTEGER NOT NULL REFERENCES users(id),
    author_id INTEGER NOT NULL REFERENCES authors(id),
    title     TEXT    NOT NULL
);

CREATE TABLE IF NOT EXISTS highlights (
    id             INTEGER PRIMARY KEY AUTOINCREMENT,
    user_id        INTEGER NOT NULL REFERENCES users(id),
    book_id        INTEGER NOT NULL REFERENCES books(id),
    text           TEXT    NOT NULL,
    weight         INTEGER NOT NULL DEFAULT 3 CHECK(weight BETWEEN 1 AND 5),
    excluded       INTEGER NOT NULL DEFAULT 0,
    last_seen      TEXT    NULL,
    delivery_count INTEGER NOT NULL DEFAULT 0,
    created_at     TEXT    NOT NULL
);

CREATE TABLE IF NOT EXISTS excluded_books (
    id          INTEGER PRIMARY KEY AUTOINCREMENT,
    user_id     INTEGER NOT NULL REFERENCES users(id),
    book_id     INTEGER NOT NULL REFERENCES books(id),
    excluded_at TEXT    NOT NULL
);

CREATE TABLE IF NOT EXISTS excluded_authors (
    id          INTEGER PRIMARY KEY AUTOINCREMENT,
    user_id     INTEGER NOT NULL REFERENCES users(id),
    author_id   INTEGER NOT NULL REFERENCES authors(id),
    excluded_at TEXT    NOT NULL
);

CREATE TABLE IF NOT EXISTS settings (
    user_id       INTEGER PRIMARY KEY REFERENCES users(id),
    schedule      TEXT    NOT NULL DEFAULT 'daily',
    delivery_day  TEXT    NULL,
    delivery_time TEXT    NOT NULL DEFAULT '18:00',
    count         INTEGER NOT NULL DEFAULT 3 CHECK(count BETWEEN 1 AND 15)
);
```

**Note**: The `Logs` table is created automatically by `Serilog.Sinks.SQLite` — not included in `schema.sql`.

---

## C# Model Classes (SunnySunday.Core/Models/)

| File | Class | Maps to table |
|------|-------|---------------|
| `Highlight.cs` | `Highlight` | `highlights` |
| `Book.cs` | `Book` | `books` |
| `Author.cs` | `Author` | `authors` |
| `User.cs` | `User` | `users` |
| `Settings.cs` | `Settings` | `settings` |

These classes are plain C# records/classes with no ORM annotations. They are used for in-memory representation and will be mapped manually in feature 004 (REST API) and feature 005 (recap engine).
