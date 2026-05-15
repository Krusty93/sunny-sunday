# Quickstart: Client CLI

**Feature**: 006-client-cli
**Date**: 2026-05-02

---

## Prerequisites

- .NET 10 SDK installed (`dotnet --version` → `10.x`)
- Repository cloned and on branch `006-client-cli` (or `task/TXXX-*` task branch)
- Solution builds cleanly from `main`: `dotnet build src/Relego.slnx`
- A running `relego-server` instance (for manual end-to-end testing)

---

## New Dependencies

### CLI project (`Relego.Cli.csproj`)

```bash
dotnet add src/Relego.Cli package Spectre.Console.Cli --version 0.55.0
dotnet add src/Relego.Cli package Microsoft.Extensions.Http --version 10.0.6
dotnet add src/Relego.Cli package Microsoft.Extensions.DependencyInjection --version 10.0.6
```

### Test project (`Relego.Tests.csproj`)

```bash
dotnet add src/Relego.Tests package RichardSzalay.MockHttp --version 7.0.0
```

---

## Build and Run

### Build the entire solution

```bash
dotnet build src/Relego.slnx
```

### Run the CLI

```bash
# Set server URL (required)
export RELEGO_SERVER=http://localhost:5000

# Run via dotnet
dotnet run --project src/Relego.Cli -- sync /path/to/My\ Clippings.txt
dotnet run --project src/Relego.Cli -- status
dotnet run --project src/Relego.Cli -- config show
dotnet run --project src/Relego.Cli -- config schedule daily 08:00
dotnet run --project src/Relego.Cli -- config count 5
dotnet run --project src/Relego.Cli -- exclude highlight 42
dotnet run --project src/Relego.Cli -- exclude list
dotnet run --project src/Relego.Cli -- weight set 7 3
dotnet run --project src/Relego.Cli -- weight list
```

### Run via Docker

```bash
docker build -f Dockerfile.cli -t relego .
docker run --rm -e RELEGO_SERVER=http://host.docker.internal:5000 relego status
```

---

## Run Tests

### All tests

```bash
dotnet test src/Relego.slnx
```

### CLI tests only

```bash
dotnet test src/Relego.slnx --filter "FullyQualifiedName~Cli"
```

### Specific test class

```bash
dotnet test src/Relego.slnx --filter "FullyQualifiedName~Cli.SyncCommand"
```

---

## Environment Variables

| Variable | Required | Description |
|----------|----------|-------------|
| `RELEGO_SERVER` | **Yes** | Base URL of the relego-server instance (e.g., `http://localhost:5000`) |

If `RELEGO_SERVER` is not set, the CLI exits immediately with:
```
Error: RELEGO_SERVER environment variable is not set.
Set it to the server URL, e.g.: export RELEGO_SERVER=http://localhost:5000
```

---

## Manual Testing Workflow

1. **Start the server** (in a separate terminal):
   ```bash
   dotnet run --project src/Relego.Server
   ```

2. **Set the environment variable**:
   ```bash
   export RELEGO_SERVER=http://localhost:5000
   ```

3. **Sync a clippings file**:
   ```bash
   dotnet run --project src/Relego.Cli -- sync tests/fixtures/sample-clippings.txt
   ```

4. **Check status**:
   ```bash
   dotnet run --project src/Relego.Cli -- status
   ```

5. **Configure schedule**:
   ```bash
   dotnet run --project src/Relego.Cli -- config schedule daily 09:00
   dotnet run --project src/Relego.Cli -- config show
   ```

---

## Project Structure

```text
src/Relego.Cli/
├── Program.cs                          # Entry point: validates RELEGO_SERVER, builds DI, runs CommandApp
├── Relego.Cli.csproj              # Project file with package references
├── Infrastructure/
│   ├── SunnyHttpClient.cs              # Typed HttpClient wrapping all API calls
│   ├── HttpClientResilienceExtensions.cs # Polly retry DelegatingHandler
│   ├── KindleDetector.cs               # Cross-platform Kindle mount auto-detection
│   └── TypeRegistrar.cs                # Spectre.Console.Cli → MS DI bridge
├── Commands/
│   ├── SyncCommand.cs                  # relego sync [path]
│   ├── StatusCommand.cs                # relego status
│   ├── Config/
│   │   ├── ConfigShowCommand.cs        # relego config show
│   │   ├── ConfigScheduleCommand.cs    # relego config schedule <cadence> <time>
│   │   └── ConfigCountCommand.cs       # relego config count <n>
│   ├── Exclude/
│   │   ├── ExcludeAddCommand.cs        # relego exclude <type> <id>
│   │   ├── ExcludeRemoveCommand.cs     # relego exclude remove <type> <id>
│   │   └── ExcludeListCommand.cs       # relego exclude list
│   └── Weight/
│       ├── WeightSetCommand.cs         # relego weight set <id> <value>
│       └── WeightListCommand.cs        # relego weight list
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
2. **No local config file**: Server URL is from `RELEGO_SERVER` env var only.
3. **Timezone auto-detection**: `TimeZoneInfo.Local.Id` is sent with schedule updates.
4. **Polly retry at handler level**: Transient errors (408, 429, 5xx) are retried transparently; commands see either success or final failure.
5. **MockHttp for tests**: HTTP responses are mocked at the `HttpMessageHandler` level, testing the full command pipeline without a running server.
