# ADR-003: SQLite as Storage Engine

**Status:** Accepted
**Date:** 2026-03-31

## Context

The server must persist highlights, recap history, weights, exclusions, and settings. The data model is relational by nature (highlight → book → author). Volume is small (≤10,000 highlights per user for typical usage). The product is self-hosted for personal or family use with no requirement for horizontal scaling.

Alternatives considered:
- **PostgreSQL** — robust, but requires a second Docker container and additional operational overhead.
- **MongoDB** — document-oriented, but the data is inherently relational (exclusions per book/author are expressed naturally as JOIN queries, not document lookups). No schema flexibility benefit at this data volume.
- **Flat files (JSON)** — no transactional guarantees; fragile for structured relational data.

## Decision

Use **SQLite** stored as a single file in the Docker volume (`/data/sunny.db`). No secondary database container required.

## Consequences

- Zero configuration for the user — data persists in the Docker volume automatically.
- Single file makes backup trivial: copy `/data/sunny.db`.
- SQLite's write lock is not a concern for MVP (single user) or family-scale use (2–10 users with infrequent writes).
- **No indexes are added for MVP** (single user). The following indexes will be added post-MVP when multi-user support is introduced:
  - `CREATE INDEX idx_highlights_user ON highlights(user_id)`
  - `CREATE INDEX idx_highlights_user_last_seen ON highlights(user_id, last_seen)`
- If usage grows beyond ~50 concurrent users, migration to PostgreSQL would be required. This is a post-MVP decision and not in scope for the current architecture.
