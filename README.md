# Sunny Sunday

> periodic notes recaps, on your Kindle

Existing solutions deliver periodic recaps only via mobile or web apps. Sunny Sunday delivers them to your Kindle — free, self-hosted, and without a subscription.

---

## How it works

1. Connect your Kindle via USB and run `sunny sync` — highlights are imported from `My Clippings.txt`
2. The server selects a daily or weekly subset of highlights using spaced repetition (weighted by your preferences)
3. A recap document is sent to your Kindle email address via Amazon's Send-to-Kindle service
4. Open the recap on your Kindle like any other document

## Getting started

### 1. Deploy the server

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
  ghcr.io/krusty93/sunnysunday.server:latest
```

Optional supply-chain verification:

```sh
gh attestation verify \
  oci://ghcr.io/krusty93/sunnysunday.server:latest \
  --owner Krusty93
```

### 2. Install the client CLI

**macOS (Apple Silicon)**
```sh
curl -L https://github.com/Krusty93/sunny-sunday/releases/latest/download/sunny-darwin-arm64 -o /usr/local/bin/sunny
chmod +x /usr/local/bin/sunny
```

**macOS (Intel) / Linux**
```sh
curl -L https://github.com/Krusty93/sunny-sunday/releases/latest/download/sunny-darwin-amd64 -o /usr/local/bin/sunny
chmod +x /usr/local/bin/sunny
```

**Windows**
```sh
winget install Krusty93.SunnySunday
```

**Docker (no install)**
```sh
docker run --rm -e SUNNY_SERVER=http://192.168.1.10:8080 ghcr.io/krusty93/sunny-sunday:latest sunny <command>
```

### 3. Sync your highlights

```sh
export SUNNY_SERVER=http://192.168.1.10:8080
sunny sync   # auto-detects Kindle mount path
```

That's it. Your first recap will arrive on the next scheduled delivery (default: every Sunday at 18:00).

---

## CLI reference

| Command | Description |
|---|---|
| `sunny sync [path]` | Import highlights from `My Clippings.txt` |
| `sunny status` | Show server status and next recap |
| `sunny config schedule <daily\|weekly> [HH:MM]` | Set recap schedule |
| `sunny config count <1-15>` | Set highlights per recap (default: 3) |
| `sunny exclude highlight <id>` | Exclude a highlight from all recaps |
| `sunny exclude book <title>` | Exclude all highlights from a book |
| `sunny exclude author <name>` | Exclude all highlights from an author |
| `sunny exclude remove highlight <id>` | Re-include a highlight |
| `sunny exclude remove book <title>` | Re-include a book |
| `sunny exclude remove author <name>` | Re-include an author |
| `sunny exclude list` | List all exclusions |
| `sunny weight set <id> <1-5>` | Set highlight weight |
| `sunny weight list` | Show weighted highlights |
| `sunny version` | Print version |

---

## Documentation

- [Product Requirements Document](docs/PRD.md)
- [Developer Experience Design](docs/DX.md)
- [Architecture](docs/ARCHITECTURE.md)
- [Architecture Decision Records](docs/adr/)

---

## Contributing

Contributions are welcome. See [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

## License

MIT — see [LICENSE](LICENSE).

