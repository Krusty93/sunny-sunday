# ADR-001: Client/Server Architecture

**Status:** Accepted
**Date:** 2026-03-31

## Context

The tool must sync highlights from a Kindle (connected via USB to the user's laptop) and send periodic recap emails automatically without requiring the laptop to be on. These two responsibilities require different runtime profiles: the sync is an on-demand operation triggered by the user; the scheduled delivery must run unattended at all times.

## Decision

Split the system into two components:

- A **client CLI** (`sunny`) that runs on the user's laptop for on-demand highlight sync and settings management.
- A **server** (`sunny-server`) that runs as an always-on Docker container on a home server, NAS, or Raspberry Pi for scheduling, recap generation, and email delivery.

## Consequences

- The user must deploy and maintain two components instead of one.
- Onboarding requires deploying a Docker container in addition to installing the CLI.
- The architecture naturally supports future multi-user scenarios (each user has their own settings and recap history).
- The client and server can be updated independently.
- The separation of concerns maps cleanly to the two distinct usage patterns: interactive (sync, settings) and automated (scheduling, delivery).
