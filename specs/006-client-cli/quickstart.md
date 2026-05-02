# Quickstart: Client CLI

**Feature**: 006-client-cli
**Date**: 2026-05-02

---

## Prerequisites

- .NET 10 SDK installed (`dotnet --version` → `10.x`)
- Repository cloned and on branch `006-client-cli` (or `task/TXXX-*` task branch)
- Solution builds cleanly from `main`: `dotnet build src/SunnySunday.slnx`
- A running `sunny-server` instance (for manual end-to-end testing)

---

## New Dependencies

### CLI project (`SunnySunday.Cli.csproj`)

```bash
dotnet add src/SunnySunday.Cli package Spectre.Console.Cli --version 0.55.0
dotnet add src/SunnySunday.Cli package Microsoft.Extensions.Http --version 10.0.6
dotnet add src/SunnySunday.Cli package Microsoft.Extensions.DependencyInjection --version 10.0.6
```

### Test project (`SunnySunday.Tests.csproj`)

```bash
dotnet add src/SunnySunday.Tests package RichardSzalay.MockHttp --version 7.0.0
```

---

## Build and Run

### Build the entire solution

```bash
dotnet build src/SunnySunday.slnx
```

### Run the CLI

```bash
# Set server URL (required)
export SUNNY_SERVER=http://localhost:5000

# Run via dotnet
dotnet run --project src/SunnySunday.Cli -- sync /path/to/My\ Clippings.txt
dotnet run --project src/SunnySunday.Cli -- status
dotnet run --project src/SunnySunday.Cli -- config show
dotnet run --project src/SunnySunday.Cli -- config schedule daily 08:00
dotnet run --project src/SunnySunday.Cli -- config count 5
dotnet run --project src/SunnySunday.Cli -- exclude highlight 42
dotnet run --project src/SunnySunday.Cli -- exclude list
dotnet run --project src/SunnySunday.Cli -- weight set 7 3
dotnet run --project src/SunnySunday.Cli -- weight list
```

### Run via Docker

```bash
docker build -f Dockerfile.cli -t sunny .
docker run --rm -e SUNNY_SERVER=http://host.docker.internal:5000 sunny status
```

---

## Run Tests

### All tests

```bash
dotnet test src/SunnySunday.slnx
```

### CLI tests only

```bash
dotnet test src/SunnySunday.slnx --filter "FullyQualifiedName~Cli"
```

### Specific test class

```bash
dotnet test src/SunnySunday.slnx --filter "FullyQualifiedName~Cli.SyncCommand"
```

---

## Environment Variables

| Variable | Required | Description |
|----------|----------|-------------|
| `SUNNY_SERVER` | **Yes** | Base URL of the sunny-server instance (e.g., `http://localhost:5000`) |

If `SUNNY_SERVER` is not set, the CLI exits immediately with:
```
Error: SUNNY_SERVER environment variable is not set.
Set it to the server URL, e.g.: export SUNNY_SERVER=http://localhost:5000
```

---

## Manual Testing Workflow

1. **Start the server** (in a separate terminal):
   ```bash
   dotnet run --project src/SunnySunday.Server
   ```

2. **Set the environment variable**:
   ```bash
   export SUNNY_SERVER=http://localhost:5000
   ```

3. **Sync a clippings file**:
   ```bash
   dotnet run --project src/SunnySunday.Cli -- sync tests/fixtures/sample-clippings.txt
   ```

4. **Check status**:
   ```bash
   dotnet run --project src/SunnySunday.Cli -- status
   ```

5. **Configure schedule**:
   ```bash
   dotnet run --project src/SunnySunday.Cli -- config schedule daily 09:00
   dotnet run --project src/SunnySunday.Cli -- config show
   ```

---

## Project Structure

```text
src/SunnySunday.Cli/
├── Program.cs                          # Entry point: validates SUNNY_SERVER, builds DI, runs CommandApp
├── SunnySunday.Cli.csproj              # Project file with package references
├── Infrastructure/
│   ├── SunnyHttpClient.cs              # Typed HttpClient wrapping all API calls
│   ├── HttpClientResilienceExtensions.cs # Polly retry DelegatingHandler
│   ├── KindleDetector.cs               # Cross-platform Kindle mount auto-detection
│   └── TypeRegistrar.cs                # Spectre.Console.Cli → MS DI bridge
├── Commands/
│   ├── SyncCommand.cs                  # sunny sync [path]
│   ├── StatusCommand.cs                # sunny status
│   ├── Config/
│   │   ├── ConfigShowCommand.cs        # sunny config show
│   │   ├── ConfigScheduleCommand.cs    # sunny config schedule <cadence> <time>
│   │   └── ConfigCountCommand.cs       # sunny config count <n>
│   ├── Exclude/
│   │   ├── ExcludeAddCommand.cs        # sunny exclude <type> <id>
│   │   ├── ExcludeRemoveCommand.cs     # sunny exclude remove <type> <id>
│   │   └── ExcludeListCommand.cs       # sunny exclude list
│   └── Weight/
│       ├── WeightSetCommand.cs         # sunny weight set <id> <value>
│       └── WeightListCommand.cs        # sunny weight list
└── Parsing/                            # Existing (no changes)
    ├── ClippingsParser.cs
    ├── ParsedBook.cs
    ├── ParsedHighlight.cs
    ├── ParseResult.cs
    └── RawClipping.cs
```

---

## Key Design Decisions

1. **No `IHost`**: The CLI uses lightweight `IServiceCollection` → `IServiceProvider` without generic host overhead.
2. **No local config file**: Server URL is from `SUNNY_SERVER` env var only.
3. **Timezone auto-detection**: `TimeZoneInfo.Local.Id` is sent with schedule updates.
4. **Polly retry at handler level**: Transient errors (408, 429, 5xx) are retried transparently; commands see either success or final failure.
5. **MockHttp for tests**: HTTP responses are mocked at the `HttpMessageHandler` level, testing the full command pipeline without a running server.
