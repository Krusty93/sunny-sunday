# Developer Experience Design — Sunny Sunday

**Version:** 0.2 — Draft
**Date:** 2026-03-31
**Status:** Draft

---

## Overview

Sunny Sunday consists of two components with distinct installation and usage patterns:

- **Server** (`sunny-server`) — Always-on Docker container deployed on a home server, NAS, or Raspberry Pi. Handles scheduling, spaced repetition, recap composition, and email delivery.
- **Client CLI** (`sunny`) — Installed on the user's laptop. Used to sync highlights from `My Clippings.txt` and manage settings.

The guiding DX principle: **zero friction after a one-time setup**. Onboarding requires one Docker command, one environment variable, one sync command.

---

## Installation

### Server

```sh
docker run -d \
  --name sunny-server \
  --restart unless-stopped \
  -e KINDLE_EMAIL=your-address@kindle.com \
  -e SMTP_HOST=smtp.example.com \
  -e SMTP_PORT=587 \
  -e SMTP_USER=user@example.com \
  -e SMTP_PASSWORD=yourpassword \
  -p 8080:8080 \
  -v sunny-data:/data \
  ghcr.io/krusty93/sunny-sunday:latest
```

That's it. The server is running and will start sending recaps on the default schedule (weekly, every Sunday at 18:00).

### Client CLI

**Option A — Docker (no install required):**
```sh
docker run --rm ghcr.io/krusty93/sunny-sunday:latest sunny <command>
```

**Option B — Download binary:**
```sh
# macOS (Apple Silicon)
curl -L https://github.com/Krusty93/sunny-sunday/releases/latest/download/sunny-darwin-arm64 -o /usr/local/bin/sunny
chmod +x /usr/local/bin/sunny

# macOS (Intel)
curl -L https://github.com/Krusty93/sunny-sunday/releases/latest/download/sunny-darwin-amd64 -o /usr/local/bin/sunny
chmod +x /usr/local/bin/sunny

# Linux
curl -L https://github.com/Krusty93/sunny-sunday/releases/latest/download/sunny-linux-amd64 -o /usr/local/bin/sunny
chmod +x /usr/local/bin/sunny

# Windows (via winget)
winget install Krusty93.SunnySunday
```

---

## Configuration

All configuration is passed as environment variables to the server container. The client reads `SUNNY_SERVER` from the environment to locate the server.

```sh
docker run -d \
  --name sunny-server \
  --restart unless-stopped \
  -e KINDLE_EMAIL=your-address@kindle.com \
  -e SUNNY_SERVER=http://192.168.1.10:8080 \
  -p 8080:8080 \
  -v sunny-data:/data \
  ghcr.io/krusty93/sunny-sunday:latest
```

On the client side, set `SUNNY_SERVER` once in your shell profile:

```sh
# ~/.zshrc or ~/.bashrc (macOS/Linux)
export SUNNY_SERVER=http://192.168.1.10:8080

# Windows (PowerShell profile)
$env:SUNNY_SERVER = "http://192.168.1.10:8080"
```

No other configuration is required to get started.

---

## Onboarding flow (first-time setup)

```
Step 1 — Deploy server (see above)
Step 2 — Set SUNNY_SERVER in your shell profile
Step 3 — Connect Kindle via USB
Step 4 — Sync highlights
```

```sh
sunny sync /Volumes/Kindle/documents/My\ Clippings.txt
```

Expected output:
```
✓ Connected to server at http://192.168.1.10:8080
✓ Parsed 1,243 highlights from 47 books
✓ 1,198 new highlights imported (45 duplicates skipped)
→ Next recap: Sunday, Apr 5 at 18:00
```

If no path is specified, `sunny sync` auto-detects the Kindle mount path. If not found, it prompts the user:

```sh
sunny sync
```

```
⚠ Kindle not found at default paths.
  Kindle connected and mounted? Enter the path to My Clippings.txt, or press Enter to cancel:
  > /media/user/Kindle/documents/My Clippings.txt
✓ Connected to server at http://192.168.1.10:8080
✓ Parsed 1,243 highlights from 47 books
✓ 1,198 new highlights imported (45 duplicates skipped)
→ Next recap: Sunday, Apr 5 at 18:00
```

Total time to first recap: ~2 minutes.

---

## Day-to-day usage

### Sync new highlights (after connecting Kindle via USB)

```sh
sunny sync          # Auto-detect Kindle mount path; prompts if not found
sunny sync <path>   # Explicit path to My Clippings.txt
```

---

## Settings management

### Schedule

```sh
sunny config schedule daily          # Send recap every day at 18:00 (default time)
sunny config schedule daily 08:00    # Send recap every day at 08:00
sunny config schedule weekly         # Send recap every Sunday at 18:00 (default)
sunny config schedule weekly 20:00   # Send recap every Sunday at 20:00
sunny config schedule show           # Print current schedule
```

### Exclude highlights / books / authors

```sh
# Exclude a specific highlight by ID
sunny exclude highlight <id>

# Exclude all highlights from a book
sunny exclude book "The Pragmatic Programmer"

# Exclude all highlights from an author
sunny exclude author "David Foster Wallace"

# Re-include a previously excluded highlight
sunny exclude remove highlight <id>

# Re-include a previously excluded book
sunny exclude remove book "The Pragmatic Programmer"

# Re-include a previously excluded author
sunny exclude remove author "David Foster Wallace"

# List all exclusions
sunny exclude list
```

### Highlight weights

```sh
# Set weight for a highlight (default: 1, range: 1–5)
sunny weight set <id> 3

# Show weight distribution across highlights
sunny weight list
```

### Highlights per recap

```sh
sunny config count 5      # Show 5 highlights per recap (default: 3, min: 1, max: 15)
sunny config count show   # Print current setting
```

### Status

```sh
sunny status
```

```
Server:       http://192.168.1.10:8080 ✓ online
Highlights:   1,198 total · 12 excluded · 34 weighted
Highlights/recap: 3 (default)
Last recap:   Mar 30, 2026 at 18:00 (3 highlights delivered)
Next recap:   Apr 5, 2026 at 18:00
Schedule:     weekly (Sunday at 18:00)
```

---

## Error messages

Errors are actionable — they tell the user exactly what to do.

### Server unreachable

```
✗ Cannot connect to server at http://192.168.1.10:8080
  Is the server running? Check with: docker ps | grep sunny-server
  Is SUNNY_SERVER set correctly? Current value: http://192.168.1.10:8080
```

### File not found

```
✗ File not found: /Volumes/Kindle/documents/My Clippings.txt
  Is your Kindle connected via USB?
  Looking for the file at a different path? Run: sunny sync <path>
```

### Email delivery failed

```
✗ Failed to deliver recap to your-address@kindle.com
  Reason: SMTP authentication failed
  Check your KINDLE_EMAIL and SMTP settings on the server:
    docker exec sunny-server env | grep SMTP
```

### Empty clippings file

```
⚠ No highlights found in My Clippings.txt
  This can happen if the file is empty or in an unexpected format.
  Expected format: Kindle's native My Clippings.txt (UTF-8, BOM prefix per entry)
```

---

## Full CLI reference

| Command | Description |
|---|---|
| `sunny sync [path]` | Import highlights from `My Clippings.txt` (auto-detects Kindle path if omitted) |
| `sunny status` | Show server status, next recap, highlight stats |
| `sunny config schedule <daily\|weekly> [HH:MM]` | Set recap schedule and optional delivery time |
| `sunny config schedule show` | Show current schedule |
| `sunny config count <1-15>` | Set number of highlights per recap |
| `sunny config count show` | Show current highlights-per-recap setting |
| `sunny exclude highlight <id>` | Exclude a highlight from all recaps |
| `sunny exclude book <title>` | Exclude all highlights from a book |
| `sunny exclude author <name>` | Exclude all highlights from an author |
| `sunny exclude remove highlight <id>` | Re-include a previously excluded highlight |
| `sunny exclude remove book <title>` | Re-include a previously excluded book |
| `sunny exclude remove author <name>` | Re-include a previously excluded author |
| `sunny exclude list` | List all exclusions |
| `sunny weight set <id> <1-5>` | Set weight for a highlight |
| `sunny weight list` | Show all weighted highlights |
| `sunny version` | Print client and server version |

---

## Decisions

- `sunny sync` auto-detects the Kindle mount path on macOS, Linux, and Windows. If not found, it prompts the user to enter the path interactively.
- Server authentication is not required — the server is assumed to be on a trusted local network.
