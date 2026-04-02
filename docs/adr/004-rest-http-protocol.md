# ADR-004: REST HTTP as Client/Server Protocol

**Status:** Accepted
**Date:** 2026-03-31

## Context

The client CLI and server must communicate over the local network. The protocol must be simple to implement, easy to debug, and familiar to open source contributors.

Alternatives considered:
- **gRPC** — efficient and strongly typed, but adds protobuf tooling complexity and is harder to debug without dedicated tooling.
- **WebSockets** — unnecessary for this use case; all operations are request/response, not streaming.

## Decision

Use **REST HTTP** with JSON payloads. The server exposes a JSON REST API on port 8080 (configurable). The client sends HTTP requests using the standard .NET `HttpClient`.

No authentication is required for MVP — the server is assumed to be on a trusted local network (home LAN). Users are responsible for not exposing port 8080 to untrusted networks.

## Consequences

- Any HTTP client can call the API directly (e.g. `curl`), making debugging and manual testing trivial.
- No additional transport or serialization setup required beyond standard .NET.
- No authentication means the server must not be exposed to the public internet. This is a deployment responsibility, not a product feature, for MVP.
- Post-MVP: token-based authentication (e.g. Bearer token via `Authorization` header) can be added without breaking the REST interface.
