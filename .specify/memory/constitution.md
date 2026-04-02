# Sunny Sunday Constitution

## Core Principles

### I. Client/Server Separation
The system is split into two independently deployable components: a client CLI (`sunny`) and a server (`sunny-server`). The client handles on-demand user interactions (sync, settings). The server handles all automated operations (scheduling, recap generation, email delivery). Neither component shall take over the responsibilities of the other.

### II. CLI-First, No GUI
All user interactions happen via the CLI. No web UI, no interactive configuration files. Settings are stored server-side and managed exclusively through CLI commands. Error messages must be actionable — every error tells the user what went wrong and how to fix it.

### III. Zero-Config Onboarding
The only required input at setup is the Kindle email address. All other settings have sensible defaults. The user must be up and running in under 2 minutes from a cold start.

### IV. Local Processing Only
No data is ever sent to third-party services other than the SMTP relay configured by the user. All highlight processing, spaced repetition selection, and recap composition happen on the user's own infrastructure.

### V. Tests Ship with the Code
Tests are not written before implementation (no TDD), but every PR that introduces new behavior must include the corresponding tests in the same PR. Unit tests cover domain logic (spaced repetition, deduplication, `My Clippings.txt` parsing). Integration tests cover REST endpoints and email delivery.

### VI. Simplicity Over Premature Generalization
MVP supports one user, no indexes, no authentication. Do not add multi-user support, authentication, or performance optimizations until explicitly required. YAGNI.

## Technology Constraints

- **Language:** C# / .NET 10 — no mixing of languages or runtimes
- **Storage:** SQLite at `/data/sunny.db` — no second database container
- **Logging:** Serilog with file sink and SQLite sink — no raw `Console.WriteLine` for diagnostic output
- **Email:** MailKit + user-provided SMTP — no third-party email SaaS
- **Scheduling:** Quartz.NET — no custom scheduler implementations
- **CLI UX:** Spectre.Console — no raw `Console.WriteLine` for user-facing output
- **Protocol:** REST HTTP + JSON — no gRPC, no GraphQL
- **Server distribution:** Docker only — no native packages for the server

## Exclusions (never implement unless explicitly requested)

- Web UI or dashboard
- Authentication / multi-user for MVP
- AI summarization of highlights
- Scraping `read.amazon.com`
- Readwise / third-party highlight source integrations (post-MVP only)
- Cloud SaaS deployment

## Governance

This constitution supersedes all other implementation guidelines. Any deviation requires explicit documentation in an ADR. Amendments require approval before implementation.

**Version:** 1.0 | **Ratified:** 2026-04-02
