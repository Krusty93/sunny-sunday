# API Contracts: Email Delivery Management

**Feature**: 006-email-delivery
**Date**: 2026-04-30

## Overview

Two new endpoints and one updated endpoint. All responses are JSON. Error responses use RFC 9457 ProblemDetails format for validation errors (422) and standard JSON for delivery results.

Base URL: `http://<server>:<port>`

---

## POST /test-delivery

Send a test EPUB to the configured Kindle email to verify the SMTP pipeline.

### Request

No body required.

### Response — 200 OK (success)

```json
{
  "success": true,
  "error": null
}
```

### Response — 200 OK (delivery failure)

```json
{
  "success": false,
  "error": "SMTP authentication failed. Check your SMTP_USER and SMTP_PASSWORD configuration."
}
```

> **Note**: Delivery failures return 200 with `success: false` because the endpoint operated correctly — it attempted delivery and reported the outcome. This is not a server error.

### Response — 422 Unprocessable Entity (pre-condition not met)

```json
{
  "title": "One or more validation errors occurred.",
  "errors": {
    "smtp": ["SMTP is not configured. Missing fields: Username, Password, FromAddress."]
  }
}
```

```json
{
  "title": "One or more validation errors occurred.",
  "errors": {
    "kindleEmail": ["Kindle email is not configured. Set it via PUT /settings first."]
  }
}
```

### Behavior

- Does NOT create or modify `recap_jobs` rows
- Does NOT update `last_seen` or `delivery_count` on any highlights
- Uses the same Polly retry policy as regular recap delivery (3 attempts, exponential backoff)
- Test EPUB contains synthetic placeholder highlights

---

## GET /deliveries

Paginated list of past recap delivery jobs.

### Query Parameters

| Parameter | Type | Default | Constraint | Description |
|-----------|------|---------|------------|-------------|
| `offset` | int | `0` | `>= 0` | Number of records to skip |
| `limit` | int | `20` | `1–100` | Number of records to return |

### Response — 200 OK

```json
{
  "items": [
    {
      "scheduledFor": "2026-04-30T16:00:00.0000000+00:00",
      "status": "delivered",
      "attemptCount": 1,
      "errorMessage": null,
      "deliveredAt": "2026-04-30T16:00:12.0000000+00:00"
    },
    {
      "scheduledFor": "2026-04-29T16:00:00.0000000+00:00",
      "status": "failed",
      "attemptCount": 3,
      "errorMessage": "SMTP connection timed out after 30 seconds.",
      "deliveredAt": null
    }
  ],
  "total": 42,
  "offset": 0,
  "limit": 20
}
```

### Response — 200 OK (empty)

```json
{
  "items": [],
  "total": 0,
  "offset": 0,
  "limit": 20
}
```

### Behavior

- Records ordered by `scheduled_for` descending (most recent first)
- SMTP credentials and internal infrastructure details are never included
- Internal `id` and `user_id` fields are excluded from the response

---

## PUT /settings (updated)

### Updated Validation: `kindleEmail` field

The `kindleEmail` field now requires a Kindle-compatible domain suffix.

**Accepted domains**: `@kindle.com`, `@free.kindle.com`

**Normalization**: Input is trimmed and lowercased before validation and storage.

### Updated Response — 422 Unprocessable Entity

```json
{
  "title": "One or more validation errors occurred.",
  "errors": {
    "kindleEmail": ["Kindle email must end with @kindle.com or @free.kindle.com."]
  }
}
```

> **Note**: The previous generic "Invalid email format." error for `kindleEmail` is replaced with the Kindle-specific message. The existing RFC 5322 format check still runs first — if the input is not a valid email at all, a generic format error is returned.

---

## GET /status (updated)

### Updated Response — 200 OK

```json
{
  "totalHighlights": 150,
  "totalBooks": 12,
  "totalAuthors": 8,
  "excludedHighlights": 5,
  "excludedBooks": 1,
  "excludedAuthors": 0,
  "nextRecap": "2026-05-01T16:00:00.0000000+00:00",
  "lastRecapStatus": "delivered",
  "lastRecapError": null,
  "smtpReady": true
}
```

New field: `smtpReady` (boolean) — `true` when all required SMTP settings (Host, Username, Password, FromAddress) are present and non-empty.
