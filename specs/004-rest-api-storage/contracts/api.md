# API Contracts: REST API & Storage Layer

**Feature**: 004-rest-api-storage
**Date**: 2026-04-18

## Overview

All endpoints return JSON. Error responses use RFC 9457 ProblemDetails format.
No authentication for MVP (ADR-004). Single-user; user auto-created on first request.

Base URL: `http://<server>:<port>`

---

## POST /sync

Bulk import highlights from a parsed clippings file.

### Request

```json
{
  "books": [
    {
      "title": "string (required, non-empty)",
      "author": "string | null",
      "highlights": [
        {
          "text": "string (required, non-empty, non-whitespace)",
          "addedOn": "string (ISO-8601) | null"
        }
      ]
    }
  ]
}
```

### Response — 200 OK

```json
{
  "newHighlights": 0,
  "duplicateHighlights": 0,
  "newBooks": 0,
  "newAuthors": 0
}
```

### Response — 422 Unprocessable Entity

```json
{
  "title": "One or more validation errors occurred.",
  "errors": {
    "books[0].highlights[0].text": ["Highlight text must not be empty or whitespace."]
  }
}
```

---

## GET /settings

Read current user settings (returns defaults if not explicitly configured).

### Response — 200 OK

```json
{
  "schedule": "daily",
  "deliveryDay": null,
  "deliveryTime": "18:00",
  "count": 3,
  "kindleEmail": ""
}
```

---

## PUT /settings

Update user settings. Only provided fields are updated.

### Request

```json
{
  "schedule": "weekly",
  "deliveryDay": "monday",
  "deliveryTime": "09:00",
  "count": 5,
  "kindleEmail": "user@kindle.com"
}
```

All fields are optional. Only include the fields to update.

### Response — 200 OK

Returns the full updated settings (same shape as GET /settings).

### Response — 422 Unprocessable Entity

```json
{
  "title": "One or more validation errors occurred.",
  "errors": {
    "count": ["Count must be between 1 and 15."],
    "kindleEmail": ["Invalid email format."]
  }
}
```

---

## GET /status

Server status with aggregate counts and schedule info.

### Response — 200 OK

```json
{
  "totalHighlights": 100,
  "totalBooks": 10,
  "totalAuthors": 5,
  "excludedHighlights": 3,
  "excludedBooks": 1,
  "excludedAuthors": 0,
  "nextRecap": "2026-04-19T18:00:00+00:00"
}
```

`nextRecap` is `null` when no schedule is configured or no highlights exist.

---

## POST /highlights/{id}/exclude

Exclude a highlight from recaps.

### Response — 204 No Content

### Response — 404 Not Found

```json
{
  "title": "Not Found",
  "detail": "Highlight 42 not found."
}
```

---

## DELETE /highlights/{id}/exclude

Re-include a previously excluded highlight.

### Response — 204 No Content

### Response — 404 Not Found

Same format as above.

---

## POST /books/{id}/exclude

Exclude a book (and all its highlights) from recaps.

### Response — 204 No Content

### Response — 404 Not Found

```json
{
  "detail": "Book 7 not found."
}
```

---

## DELETE /books/{id}/exclude

Re-include a previously excluded book.

### Response — 204 No Content

### Response — 404 Not Found

---

## POST /authors/{id}/exclude

Exclude an author (and all their books/highlights) from recaps.

### Response — 204 No Content

### Response — 404 Not Found

---

## DELETE /authors/{id}/exclude

Re-include a previously excluded author.

### Response — 204 No Content

### Response — 404 Not Found

---

## GET /exclusions

List all current exclusions grouped by category.

### Response — 200 OK

```json
{
  "highlights": [
    { "id": 42, "text": "First 100 chars of highlight...", "bookTitle": "Deep Work" }
  ],
  "books": [
    { "id": 7, "title": "Atomic Habits", "authorName": "James Clear", "highlightCount": 15 }
  ],
  "authors": [
    { "id": 3, "name": "Cal Newport", "bookCount": 2 }
  ]
}
```

---

## PUT /highlights/{id}/weight

Set a highlight's weight (1–5).

### Request

```json
{
  "weight": 5
}
```

### Response — 204 No Content

### Response — 404 Not Found

### Response — 422 Unprocessable Entity

```json
{
  "title": "One or more validation errors occurred.",
  "errors": {
    "weight": ["Weight must be between 1 and 5."]
  }
}
```

---

## GET /highlights/weights

List all highlights with non-default weights (weight ≠ 3).

### Response — 200 OK

```json
[
  { "id": 42, "text": "First 100 chars...", "bookTitle": "Deep Work", "weight": 5 },
  { "id": 99, "text": "Another highlight...", "bookTitle": "Atomic Habits", "weight": 1 }
]
```

---

## Common Error Codes

| Status | Meaning | When |
|--------|---------|------|
| 200 | OK | Successful read or sync |
| 204 | No Content | Successful mutation (exclude, weight, etc.) |
| 404 | Not Found | Entity ID doesn't exist |
| 422 | Unprocessable Entity | Validation failure |
| 500 | Internal Server Error | Unexpected server error |
