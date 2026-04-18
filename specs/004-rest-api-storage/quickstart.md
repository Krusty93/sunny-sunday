# Quickstart: REST API & Storage Layer

**Feature**: 004-rest-api-storage
**Date**: 2026-04-18

## Prerequisites

- .NET 10 SDK installed (verify: `dotnet --version`)
- Repository cloned and on the `004-rest-api-storage` branch
- Solution builds: `dotnet build src/SunnySunday.slnx`

## New Dependencies

### Server project (`SunnySunday.Server.csproj`)

```bash
cd src/SunnySunday.Server
dotnet add package Dapper
dotnet add package Swashbuckle.AspNetCore
```

### Test project (`SunnySunday.Tests.csproj`)

```bash
cd src/SunnySunday.Tests
dotnet add package Microsoft.AspNetCore.Mvc.Testing
```

## Build & Run

```bash
# Build everything
dotnet build src/SunnySunday.slnx

# Run the server
dotnet run --project src/SunnySunday.Server

# Server listens on http://localhost:5000 (or as configured in launchSettings.json)
```

## Run Tests

```bash
# All tests
dotnet test src/SunnySunday.slnx

# Only API integration tests
dotnet test src/SunnySunday.Tests --filter "FullyQualifiedName~Api"
```

## Swagger UI (Development only)

When running in Development (`ASPNETCORE_ENVIRONMENT=Development`, which is the default for `dotnet run`), the Swagger UI is available at:

```
http://localhost:5000/swagger
```

The OpenAPI JSON spec is at:

```
http://localhost:5000/swagger/v1/swagger.json
```

Swagger is **not** enabled in Production or other environments.

## Manual API Testing

### Sync highlights

```bash
curl -X POST http://localhost:5000/sync \
  -H "Content-Type: application/json" \
  -d '{
    "books": [
      {
        "title": "Deep Work",
        "author": "Cal Newport",
        "highlights": [
          { "text": "Professional activities performed in a state of distraction-free concentration." },
          { "text": "Clarity about what matters provides clarity about what does not." }
        ]
      }
    ]
  }'
```

Expected response (200):
```json
{
  "newHighlights": 2,
  "duplicateHighlights": 0,
  "newBooks": 1,
  "newAuthors": 1
}
```

### Read settings

```bash
curl http://localhost:5000/settings
```

Expected response (200):
```json
{
  "schedule": "daily",
  "deliveryDay": null,
  "deliveryTime": "18:00",
  "count": 3,
  "kindleEmail": ""
}
```

### Update settings

```bash
curl -X PUT http://localhost:5000/settings \
  -H "Content-Type: application/json" \
  -d '{ "kindleEmail": "user@kindle.com", "schedule": "weekly", "deliveryDay": "monday" }'
```

### Server status

```bash
curl http://localhost:5000/status
```

### Exclude a book

```bash
curl -X POST http://localhost:5000/books/1/exclude
```

### List exclusions

```bash
curl http://localhost:5000/exclusions
```

### Set highlight weight

```bash
curl -X PUT http://localhost:5000/highlights/1/weight \
  -H "Content-Type: application/json" \
  -d '{ "weight": 5 }'
```

### List weighted highlights

```bash
curl http://localhost:5000/highlights/weights
```

## Project Structure

```
src/SunnySunday.Server/
├── Contracts/          # Request/response DTOs
├── Data/               # Dapper-based data access
├── Endpoints/          # Minimal API endpoint groups
├── Infrastructure/     # Existing: Database/, Logging/
└── Program.cs          # Composition root

src/SunnySunday.Tests/
├── Api/                # Integration tests (WebApplicationFactory)
├── Infrastructure/     # Existing schema tests
└── Parsing/            # Existing parser tests
```

## Key Design Decisions

| Decision | Choice | Details |
|----------|--------|---------|
| Data access | Dapper + raw SQL | Thin ORM over existing `Microsoft.Data.Sqlite` |
| API contracts | Separate DTOs | `Contracts/` namespace, not domain models |
| Endpoint org | `MapGroup` | One file per domain area in `Endpoints/` |
| Error format | ProblemDetails (RFC 9457) | Built-in `Results.Problem()` / `Results.ValidationProblem()` |
| Validation | Manual inline | Simple helper, no FluentValidation |
| Testing | `WebApplicationFactory` + in-memory SQLite | Full HTTP pipeline, isolated per test |
