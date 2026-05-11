# Sunny Sunday

> periodic notes recaps, on your Kindle

Existing solutions deliver periodic recaps only via mobile or web apps. Sunny Sunday delivers them to your Kindle — free, self-hosted, and without a subscription.

---

## How it works

1. Connect your Kindle via USB and run `sunny sync` — highlights are imported from `My Clippings.txt`
2. The server selects a daily or weekly subset of highlights using spaced repetition (weighted by your preferences)
3. A recap document is sent to your Kindle email address via Amazon's Send-to-Kindle service
4. Open the recap on your Kindle like any other document

> **TUI mode**: Running `sunny` with no arguments in an interactive terminal opens a full-screen TUI.  
> Browse your books, manage settings, and verify SMTP configuration — all without leaving the terminal.

## Getting started

### 1. Connect your Kindle device

Connect your Kindle device to your computer via USB cable.

### 2. Run the server

```sh
docker network create sunnysunday

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
  --network sunnysunday \
  ghcr.io/krusty93/sunnysunday.server:latest
```

Replace the `SMTP_*` values with those for your provider.

> Gmail and Outlook personal accounts do not support SMTP with password authentication.
>
> Use a free SMTP relay like [Resend](https://resend.com/docs/send-with-smtp), [MailerSend](https://www.mailersend.com/help/smtp-relay) or [Mailgun](https://www.mailgun.com/features/smtp-server/) instead. They offer a free tier with a generous limit of free emails. Otherwise, you can use your own SMPT relay server.

### 3. Sync the Kindle highlights

Upload highlights to the server using the CLI. It automatically detects the path to your Kindle:

<details>
  <summary>Docker (suggested - no install)</summary>

  **Windows** (Kindle mounts as drive `D:`):

  ```powershell
  docker run `
    -v "D:\documents:/kindle:ro" `
    --network sunnysunday `
    -e SUNNY_SERVER="http://sunny-server:8080" `
    ghcr.io/krusty93/sunnysunday.cli:latest `
    sync "/kindle/My Clippings.txt"
  ```

  > NB: Follow the [WSL documentation](https://learn.microsoft.com/en-us/windows/wsl/connect-usb) to allow WSL to access the Kindle device. Another simpler option is to copy the `My Clippings.txt` file to your PC (e.g. local path) and mounting the volume:

  ```powershell
  docker run `
    -v "$(PWD):/kindle:ro" `
    --network sunnysunday `
    -e SUNNY_SERVER="http://sunny-server:8080" `
    ghcr.io/krusty93/sunnysunday.cli:latest `
    sync "/kindle/My Clippings.txt"
  ```

  **macOS** (Kindle mounts at `/Volumes/Kindle`):
  ```sh
  docker run \
    -v "/Volumes/Kindle/documents:/kindle:ro" \
    -e SUNNY_SERVER="http://sunny-server:8080" \
    ghcr.io/krusty93/sunnysunday.cli:latest \
    sync "/kindle/My Clippings.txt"
  ```

  **Linux** (Kindle mounts at `/media/$USER/Kindle`):
  ```sh
  docker run \
    -v "/media/$USER/Kindle/documents:/kindle:ro" \
    -e SUNNY_SERVER="http://sunny-server:8080" \
    ghcr.io/krusty93/sunnysunday.cli:latest \
    sync "/kindle/My Clippings.txt"
  ```

</details>

<details>
  <summary>Windows</summary>

#### winget

  ```sh
  winget install Krusty93.SunnySunday sync
  ```

#### Binary

  Replace `<version>` with the actual version number (e.g. `1.0.0`).

  ```powershell
  curl -L https://github.com/Krusty93/sunny-sunday/releases/download/cli%2Fv<version>/sunny-<version>-win-x64 -o ./sunny.exe
  ./sunny.exe sync
  ```

</details>

<details>
  <summary>MacOS</summary>

  Replace `<version>` with the actual version number (e.g. `1.0.0`).

#### Apple Silicon

  ```sh
  curl -L https://github.com/Krusty93/sunny-sunday/releases/download/cli%2Fv<version>/sunny-<version>-osx-arm64 -o /usr/local/bin/sunny
  chmod +x /usr/local/bin/sunny
  sunny sync
  ```

#### Intel

  ```sh
  curl -L https://github.com/Krusty93/sunny-sunday/releases/download/cli%2Fv<version>/sunny-<version>-osx-amd64 -o /usr/local/bin/sunny
  chmod +x /usr/local/bin/sunny
  sunny sync
  ```

</details>

<details>
  <summary>Linux</summary>

  Replace `<version>` with the actual version number (e.g. `1.0.0`).

  ```sh
  curl -L https://github.com/Krusty93/sunny-sunday/releases/download/cli%2Fv<version>/sunny-<version>-linux-x64 -o /usr/local/bin/sunny
  chmod +x /usr/local/bin/sunny
  sunny sync
  ```

</details>

The client automatically connects to `http://localhost:8080`. If you ran the server on a different host machine or port, you can override the default URL exporting the variable `SUNNY_SERVER`:

```sh
# binary
export SUNNY_SERVER=http://192.168.1.10:8080
sunny sync

# Docker
docker run \
  -e SUNNY_SERVER=http://192.168.1.10:8080 \
  ghcr.io/krusty93/sunnysunday.cli:latest sync
```

That's it. Your first recap will arrive on the next scheduled delivery (default: every day at 18:00).

> **My Clippings.txt on a different path?** Override the default location using:
>
> ```sh
> sunny sync <path>
> ```
>

---

## CLI reference

|                   Command                       |              Description                  |
|-------------------------------------------------|-------------------------------------------|
| `sunny`                                         | Open interactive TUI (no args, interactive terminal) |
| `sunny sync [path]`                             | Import highlights from `My Clippings.txt` |
| `sunny status`                                  | Show server status and next recap         |
| `sunny config schedule <daily\|weekly> [HH:MM]` | Set recap schedule                        |
| `sunny config schedule show`                    | Show current schedule                     |
| `sunny config count show`                       | Show current highlights-per-recap setting |
| `sunny config count <1-15>`                     | Set highlights per recap (default: 5)     |
| `sunny config kindle-email <address>`           | Set the Kindle delivery email address     |
| `sunny exclude highlight <id>`                  | Exclude a highlight from all recaps       |
| `sunny exclude book <title>`                    | Exclude all highlights from a book        |
| `sunny exclude author <name>`                   | Exclude all highlights from an author     |
| `sunny exclude remove highlight <id>`           | Re-include a highlight                    |
| `sunny exclude remove book <title>`             | Re-include a book                         |
| `sunny exclude remove author <name>`            | Re-include an author                      |
| `sunny exclude list`                            | List all exclusions                       |
| `sunny weight set <id> <1-5>`                   | Set highlight weight                      |
| `sunny weight list`                             | Show weighted highlights                  |
| `sunny version`                                 | Print version                             |

---

## Documentation

- [Product Requirements Document](docs/PRD.md)
- [Developer Experience Design](docs/DX.md)
- [Architecture](docs/ARCHITECTURE.md)
- [Architecture Decision Records](docs/adr/)

---

## Supply chain verification

Verify Docker image origin via GitHub CLI:

```sh
gh attestation verify \
  oci://ghcr.io/krusty93/sunnysunday.server:latest \
  --owner Krusty93
```

```sh
gh attestation verify \
  oci://ghcr.io/krusty93/sunnysunday.cli:latest \
  --owner Krusty93
```

---

## Contributing

Contributions are welcome. See [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

## License

MIT, see [LICENSE](LICENSE).
