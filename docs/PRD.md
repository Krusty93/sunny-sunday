# Product Requirements Document — Sunny Sunday

**Version:** 0.2 — Draft
**Date:** 2026-03-30
**Status:** Draft

---

## Overview

Sunny Sunday is a self-hosted, open source CLI tool that parses Kindle highlights from `My Clippings.txt` and delivers periodic recap documents directly to a Kindle device via Amazon's Send-to-Kindle email service.

---

## Functional Requirements

| ID | Requirement | Priority |
|---|---|---|
| FR-01 | Parse `My Clippings.txt` and extract highlights and annotations | Must |
| FR-02 | Deduplicate highlights (Kindle appends duplicates on re-highlight) | Must |
| FR-03 | Group highlights by book title and author | Must |
| FR-04 | Compose a recap document from the parsed highlights | Must |
| FR-05 | Send the recap as an email attachment to a configured Kindle email address | Must |
| FR-06 | Support configurable recap schedule (daily or weekly) | Must |
| FR-07 | Provide sensible defaults for all settings except the Kindle email address, requiring zero configuration to get started | Must |
| FR-08 | Track recap history per highlight (delivery count, last seen date) | Must |
| FR-09 | Select highlights for each recap using spaced repetition — highlights seen less recently are prioritized | Must |
| FR-10 | Produce output compatible with Kindle's native document rendering (e-ink friendly) | Must |
| FR-11 | Allow the user to assign a weight to each highlight (higher weight = higher probability of appearing in recaps) | Must |
| FR-12 | Allow the user to exclude specific highlights, books, or authors from all future recaps, and to re-include them at any time | Must |
| FR-13 | Expose all settings management via CLI commands — settings are stored server-side, not in a local file edited by the user | Must |
| FR-14 | Allow the user to configure the number of highlights per recap (min 1, max 15, default 3) | Must |

---

## Non-Functional Requirements

| ID | Requirement | Priority |
|---|---|---|
| NFR-01 | Distributed and run exclusively via Docker | Must |
| NFR-02 | No external runtime dependencies beyond a container or a standard language runtime | Should |
| NFR-03 | All user-facing configuration is managed via CLI — no manual file editing required | Must |
| NFR-04 | Licensed under MIT or Apache 2.0 | Must |
| NFR-05 | No data sent to third-party services — all processing is local | Must |
| NFR-06 | Recap generation completes in under 30 seconds for a 10,000-highlight file | Should |
| NFR-07 | Recap document renders correctly on Kindle Paperwhite (any generation) | Must |

---

## User Stories

| ID | Story | Priority |
|---|---|---|
| US-01 | As a Kindle user, I want to connect my Kindle via USB and point the tool at `My Clippings.txt`, so that my highlights are available for recap generation | Must |
| US-02 | As a user, I want to provide only my Kindle email address to get started, with all other settings applied automatically as defaults, so that onboarding requires minimal effort | Must |
| US-03 | As a user, I want to choose between daily and weekly recap delivery via CLI, so that I can adjust the frequency without editing any file manually | Must |
| US-04 | As a user, I want each recap to surface highlights I haven't seen recently, weighted by my preferences, so that repeated exposure helps me retain what matters most to me | Must |
| US-05 | As a user, I want the recap to open as a native document on my Kindle, so that I can read it comfortably on e-ink without eye strain | Must |
| US-06 | As a user, I want to run the tool with a single command after initial setup, so that day-to-day usage requires no technical knowledge | Must |
| US-07 | As a self-hoster, I want to run the server component as a always-on Docker container on my home server or NAS, so that recaps are sent automatically without requiring my laptop to be on | Must |
| US-08 | As a user, I want clear error messages when email delivery fails, so that I can diagnose and fix configuration issues quickly | Should |
| US-09 | As a user, I want to mark a highlight/book/author as excluded via CLI, so that it never appears in my recaps | Must |
| US-10 | As a user, I want to assign a higher weight to specific highlights via CLI, so that they appear more frequently than others | Must |

---

## MoSCoW Prioritization

### Must Have (MVP)

- Parse `My Clippings.txt`, deduplicate, and group by book (FR-01, FR-02, FR-03)
- Compose and send recap document to Kindle email (FR-04, FR-05, FR-10)
- Track recap history per highlight and apply spaced repetition for selection (FR-08, FR-09)
- User-defined highlight weights and exclusions (FR-11, FR-12)
- Configurable schedule: daily or weekly (FR-06)
- Zero-config onboarding with sensible defaults (FR-07)
- CLI-based settings management, stored server-side (FR-13)
- Configurable highlights per recap: min 1, max 15, default 3 (FR-14)
- Docker-only distribution (NFR-01)
- All processing local, no third-party data sharing (NFR-05)
- MIT or Apache 2.0 license (NFR-04)

### Should Have

- Clear error messages for SMTP/delivery failures (US-08)
- Sub-30s performance for large clippings files (NFR-06)
- Minimal runtime dependencies (NFR-02)

### Could Have (post-MVP)

- Readwise integration as optional connector
- Web clipper integration
- Multiple highlight sources (e.g. Apple Books, Kobo)
- Recap format customization (font size, density, layout)

### Won't Have (explicitly out of scope)

- Mobile app
- Web UI
- Cloud SaaS version
- AI summarization of highlights
- Social or sharing features
- Scraping `read.amazon.com`

---

## MVP Definition

The MVP is complete when a user can:

1. Deploy the server component as a Docker container on a home server, NAS, or Raspberry Pi
2. Run the client CLI on their laptop, point it at `My Clippings.txt`, and provide their Kindle email address
3. Receive a correctly formatted recap document on their Kindle on the configured schedule, with highlights selected via spaced repetition
