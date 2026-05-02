# Research: Client CLI

**Feature**: 006-client-cli
**Phase**: 0 — Research
**Date**: 2026-05-02

---

## Research Tasks & Findings

### 1. Spectre.Console.Cli CommandApp with DI Integration

**Decision**: Use `Spectre.Console.Cli` `CommandApp` with a custom `ITypeRegistrar` bridging to `Microsoft.Extensions.DependencyInjection`

**Rationale**:
- Spectre.Console.Cli provides a built-in command tree model with automatic `--help` generation, argument parsing, and validation — exactly what the spec requires (FR-006-01).
- Spectre.Console.Cli does **not** natively integrate with `Microsoft.Extensions.DependencyInjection`. A custom `ITypeRegistrar` / `ITypeResolver` adapter is needed (a well-documented pattern in the Spectre community).
- The adapter is ~40 lines: it wraps an `IServiceCollection` for registration and builds an `IServiceProvider` on first resolve. Commands receive constructor-injected services (e.g., `SunnyHttpClient`).
- `CommandApp` supports hierarchical commands via `AddBranch` (e.g., `config` → `show`, `schedule`, `count`) and positional arguments via `CommandArgument` attributes.
- The CLI does **not** need `IHost` lifecycle (no hosted services, no graceful shutdown). A lightweight `IServiceCollection` → `IServiceProvider` suffices. Using `Microsoft.Extensions.Hosting` would add unnecessary complexity for a synchronous CLI tool.

**Command tree structure**:
```
sunny
├── sync [path]
├── status
├── config
│   ├── show
│   ├── schedule <cadence> <time>
│   └── count <n|show>
├── exclude
│   ├── <type> <id>          (default command)
│   ├── remove <type> <id>
│   └── list
└── weight
    ├── set <id> <value>
    └── list
```

**Alternatives considered**:
- **System.CommandLine**: Microsoft's official CLI library, but it was in beta for years and its API surface changed significantly. Spectre.Console.Cli is stable, widely used, and already a project dependency.
- **Cocona**: DI-integrated CLI framework, but it's a third-party library adding another dependency when Spectre already provides the needed functionality.
- **Full `IHost` + `CommandApp`**: Hosting would provide logging and DI out of the box but adds startup overhead (~200ms) that violates the "commands complete in < 5 seconds" goal. The lightweight DI approach avoids this.

---

### 2. Typed HttpClient with Polly Retry

**Decision**: Register `SunnyHttpClient` as a typed client via `IHttpClientFactory` with a Polly resilience pipeline

**Rationale**:
- `IHttpClientFactory` (from `Microsoft.Extensions.Http`) manages `HttpMessageHandler` lifetimes, avoiding socket exhaustion. Even though the CLI is short-lived, the pattern is idiomatic and adds no meaningful overhead.
- Polly 8.x (already referenced in the project at version 8.6.6 via `PackageVersions.props`) provides `ResiliencePipelineBuilder<HttpResponseMessage>` for retry policies.
- The retry policy handles transient errors: HTTP 408 (Request Timeout), 429 (Too Many Requests), and 5xx (Server Error). Non-transient 4xx errors (400, 404, 409) are **not** retried — they are returned to the calling command for specific error handling.
- Configuration: max 3 attempts total (1 initial + 2 retries), exponential backoff with base delay of 1 second (1s → 2s → 4s).
- `Microsoft.Extensions.Http.Resilience` NuGet package provides the integration layer between `IHttpClientFactory` and Polly 8.x via `AddResilienceHandler`. However, for simplicity and to minimize new dependencies, we can use raw Polly with a delegating handler.

**Decision refined**: Use `Microsoft.Extensions.Http.Polly` or manual `DelegatingHandler` with Polly pipeline. Since `Microsoft.Extensions.Http.Resilience` pulls in several transitive dependencies, we'll use a lightweight custom `DelegatingHandler` that wraps Polly's `ResiliencePipeline<HttpResponseMessage>`.

**Alternatives considered**:
- **No retry (raw HttpClient)**: Violates FR-006-18 and FR-006-19.
- **`Microsoft.Extensions.Http.Resilience`**: Official integration but heavy transitive dependency tree (Microsoft.Extensions.Diagnostics, OpenTelemetry references). Overkill for 3 retries.
- **Polly v7 `PolicyHttpMessageHandler`**: Polly 8.x is already referenced; using v7 policies would require dual references.

---

### 3. Kindle Auto-Detection Strategy

**Decision**: Static filesystem probing with platform-specific known paths

**Rationale**:
- Kindle devices mount as USB mass storage with a consistent directory structure across all generations: `<mount_point>/documents/My Clippings.txt`.
- The mount point varies by OS:
  - **macOS**: `/Volumes/Kindle/` (volume label)
  - **Linux**: `/media/$USER/Kindle/` or `/run/media/$USER/Kindle/` (udisks2 auto-mount)
  - **Windows**: Drive letters D–G with `documents\My Clippings.txt` present
- Detection is a simple `File.Exists()` check across known candidate paths. No filesystem watcher, no USB event monitoring — YAGNI for a CLI invoked on demand.
- If detection fails, the CLI prompts the user via Spectre `TextPrompt<string>`. If stdin is not a TTY (piped/scripted usage), the CLI exits with an error explaining the `[path]` argument.
- The detector does **not** cache results (CLI is short-lived).

**Alternatives considered**:
- **WMI/IOKit/udev queries**: OS-specific APIs to enumerate USB devices. Much more complex, requires platform-specific code paths beyond simple path checking, and adds no practical benefit since the clippings file path is deterministic once the mount point is known.
- **Configuration file for path**: Violates constitution principle II (CLI-first, no config files) and principle III (zero-config).

---

### 4. Error Handling & Exit Codes

**Decision**: Uniform error handling via try/catch in each command with Spectre markup for user-facing errors; exit code 0 for success, 1 for failure

**Rationale**:
- Each `AsyncCommand.ExecuteAsync` wraps its HTTP call in try/catch. Specific exception types drive specific messages:
  - `HttpRequestException` (connection refused/timeout after retries) → "Cannot reach server at {url}. Check SUNNY_SERVER environment variable."
  - `HttpRequestException` with status 4xx → parse JSON error body, display field-level validation messages.
  - `HttpRequestException` with status 5xx (after retry exhaustion) → "Server error. Try again later."
  - `TaskCanceledException` → "Request timed out."
- Exit codes follow Unix convention: 0 = success, non-zero = failure (FR-006-23).
- No stack traces are shown to users. Exceptions are logged at Debug level via `ILogger` (if verbose mode is ever added) but never printed to console.
- Server error responses follow the RFC 7807 Problem Details format (as implemented in feature 004). The CLI parses `title` and `detail` fields for display.

**Alternatives considered**:
- **Global exception handler**: Spectre.Console.Cli supports `SetExceptionHandler`, but per-command handling gives better context-specific messages.
- **Result<T> pattern everywhere**: Would add unnecessary abstraction for a CLI that either succeeds or fails.

---

### 5. DI Approach: Lightweight vs Generic Host

**Decision**: Lightweight `IServiceCollection` without `IHost` — no generic host

**Rationale**:
- The CLI is a short-lived process that runs a single command and exits. It has no hosted services, no background workers, no graceful shutdown requirements.
- `Microsoft.Extensions.Hosting` (`IHost`) adds ~150–200ms startup overhead for host builder, logging pipeline, and lifetime management. This overhead matters for CLI perceived responsiveness (SC-006-02: < 5 seconds).
- DI alone (`IServiceCollection` → `IServiceProvider`) provides everything needed: typed HttpClient registration, service resolution in commands.
- Logging is handled by `Microsoft.Extensions.Logging.Abstractions` (already referenced) with a null logger by default. If verbose mode is needed later, a console logger can be registered without requiring `IHost`.

**Alternatives considered**:
- **Full Generic Host**: Provides structured logging, configuration binding, hosted services. But CLI doesn't need any of these features, and the startup cost is noticeable.
- **No DI (manual construction)**: Would work for a small CLI, but becomes unwieldy with 8+ commands needing the same `SunnyHttpClient`. DI keeps commands focused on their logic.

---

### 6. Testing Strategy: MockHttp for Typed HttpClient

**Decision**: Use `RichardSzalay.MockHttp` to mock HTTP responses in CLI command tests

**Rationale**:
- `RichardSzalay.MockHttp` provides a `MockHttpMessageHandler` that can be plugged into `HttpClient` construction, intercepting all outbound requests and returning configured responses.
- This allows testing each command's logic (argument parsing, response formatting, error handling) without a running server.
- Tests construct a `SunnyHttpClient` with a `MockHttpMessageHandler`-backed `HttpClient`, inject it into the command, and verify behavior.
- No `WebApplicationFactory` needed for CLI tests — the CLI doesn't host a web server. The mock operates at the `HttpMessageHandler` level.
- Spectre.Console provides `TestConsole` for capturing CLI output in tests via `CommandAppTester`.

**Alternatives considered**:
- **WireMock.Net**: Full HTTP mock server. Heavier setup (allocates a port), slower, and unnecessary when handler-level mocking suffices.
- **Integration tests with real server**: Useful for end-to-end validation but too slow for unit-level command tests. Can be added as a separate integration test suite later.
- **NSubstitute/Moq on `SunnyHttpClient`**: Would work but misses HTTP-level behaviors (status codes, headers). MockHttp tests the full HTTP pipeline including Polly retry (when configured in the handler chain).

---

### 7. Server URL Resolution

**Decision**: Read `SUNNY_SERVER` environment variable at CLI startup; validate as absolute URI; fail fast with actionable error

**Rationale**:
- Environment variables are the standard mechanism for configuring Docker-deployed tools (constitution principle VI — Docker distribution).
- Validation occurs once at startup (in `Program.cs` before any command runs): must be a well-formed absolute HTTP(S) URI. Catches common mistakes like missing scheme (`localhost:5000` instead of `http://localhost:5000`).
- No fallback default (e.g., no implicit `http://localhost:5000`). The spec explicitly requires the variable to be set (FR-006-02, FR-006-03). A default would violate the principle of explicit configuration.
- The base address is set on the `HttpClient` at registration time via `client.BaseAddress = new Uri(serverUrl)`.

**Alternatives considered**:
- **Config file (`.sunnyrc`, `appsettings.json`)**: Violates constitution (no config files for CLI).
- **Command-line `--server` flag**: Adds repetitive typing. Environment variable is set once per shell session.
- **Default to localhost:5000**: Would silently connect to wrong endpoint if user forgets to set the variable.
