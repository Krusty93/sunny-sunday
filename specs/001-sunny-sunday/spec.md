# Spec: Sunny Sunday — Kindle Highlights Recap Delivery

## What we are building

Sunny Sunday is a self-hosted, open source tool that helps readers retain what they highlight on their Kindle by delivering periodic recap documents directly to their Kindle device.

The tool reads highlights from `My Clippings.txt` — the file a Kindle saves locally when the user highlights text, accessible by connecting the device via USB. It selects a subset of highlights using a spaced repetition algorithm (highlights seen less recently are prioritized), composes a recap document, and sends it as an email attachment to the user's Kindle email address via Amazon's Send-to-Kindle personal document service. The recap renders as a native document on the Kindle, readable on e-ink without eye strain.

The system follows a client/server architecture:
- A **server component** runs as an always-on Docker container on a home server, NAS, or Raspberry Pi. It handles scheduling, spaced repetition selection, recap composition, and email delivery.
- A **client CLI** runs on the user's laptop. It is used to sync highlights from `My Clippings.txt` to the server and to manage settings (schedule, highlight weights, exclusions).

## Who it is for

Readers who highlight and annotate texts on Kindle and want to review those highlights on their e-ink device on daily/weekly basis. The user is comfortable self-hosting a Docker container but is not necessarily a developer. Day-to-day usage after initial setup should require no technical knowledge.

## Core user flows

### Onboarding
1. The user deploys the server Docker container on their home server.
2. The user installs the client CLI on their laptop.
3. The user runs a single setup command, providing only their Kindle email address. All other settings use sensible defaults.
4. The user connects their Kindle via USB, runs the sync command pointing at `My Clippings.txt`.
5. The server starts sending recap documents on the default schedule (weekly).

### Ongoing usage
- The user connects their Kindle via USB periodically and runs the sync command to import new highlights.
- Recaps are delivered automatically on the configured schedule (daily or weekly).
- Each recap contains a selection of highlights chosen via spaced repetition — highlights the user has not seen recently are prioritized.

### Highlight management (via CLI)
- The user can exclude a highlight so it never appears in recaps.
- The user can assign a higher weight to a highlight so it appears more frequently.
- The user can change the recap schedule (daily or weekly) and delivery time.
- The user can configure the number of highlights per recap (min 1, max 15, default 3).
- All settings are managed via CLI commands and stored server-side — no config file editing required.

## What the recap document looks like

- A formatted document sent as an email attachment (format TBD in architecture step: EPUB, PDF, or MOBI).
- Highlights are grouped by book title and author.
- Renders correctly as a native Kindle document on Kindle Paperwhite (any generation).
- Contains only highlights selected for that recap — not all highlights ever collected.

## Highlight selection algorithm

- Spaced repetition: highlights seen less recently are prioritized over recently seen ones.
- Number of highlights per recap: configurable by the user (min 1, max 15, default 3).
- User-defined weights: highlights with a higher weight have a higher probability of being selected.
- Excluded highlights are never selected.
- The system tracks delivery history per highlight (delivery count, last seen date).

## What Sunny Sunday does NOT do

- No mobile app, no web UI.
- No cloud SaaS version — self-hosted only.
- No AI summarization — recaps contain raw highlights, not summaries.
- No scraping of `read.amazon.com` or any Amazon account access.
- No social or sharing features.
- No Readwise integration for MVP (post-MVP connector).
- No support for highlight sources other than `My Clippings.txt` for MVP (Apple Books, Kobo, web clipper are post-MVP).

## Non-functional constraints

- Distributed exclusively via Docker.
- All processing is local — no data sent to third-party services.
- Licensed under MIT or Apache 2.0.
- Recap generation must complete in under 30 seconds for a file with 10,000 highlights.

## Open questions / areas needing clarification

- Recap document format (EPUB, PDF, or MOBI) is an implementation detail — transparent to the user, to be decided during the implementation step based on rendering quality and code complexity.

## Decisions

- Client CLI authentication to server: none — local network is assumed trusted.
- Number of highlights per recap: configurable by the user (min 1, max 15, default 3).
- `sunny sync` auto-detects the Kindle mount path; if not found, prompts the user to enter it interactively.
- SMTP configuration: provided as environment variables to the server container (`SMTP_HOST`, `SMTP_PORT`, `SMTP_USER`, `SMTP_PASSWORD`).
