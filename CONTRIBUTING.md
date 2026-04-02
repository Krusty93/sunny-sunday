# Contributing to Sunny Sunday

Thank you for your interest in contributing. This document covers how to report issues, propose changes, and submit pull requests.

---

## Before you start

- Check [open issues](https://github.com/Krusty93/sunny-sunday/issues) to avoid duplicating work.
- For significant changes (new features, breaking changes), open an issue first to discuss the approach before writing code.
- Keep contributions focused — one logical change per pull request.

---

## Development setup

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker](https://docs.docker.com/get-docker/)

### Run the server locally

```sh
cd src/SunnySunday.Server
dotnet run
```

### Run the client CLI locally

```sh
cd src/SunnySunday.Cli
dotnet run -- <command>
```

### Run tests

```sh
dotnet test
```

---

## Pull request guidelines

- Target the `main` branch.
- Include tests for any new behavior.
- Keep the PR description short and factual: what changed and why.
- Do not include unrelated formatting or refactoring changes in the same PR.

---

## Reporting bugs

Open a [GitHub issue](https://github.com/Krusty93/sunny-sunday/issues/new?template=bug_report.md) with:

- What you did
- What you expected
- What happened instead
- Your OS, Docker version, and `sunny version` output

---

## Requesting features

Open a [GitHub issue](https://github.com/Krusty93/sunny-sunday/issues/new?template=feature_request.md) describing the use case and the problem it solves. Focus on the *what* and *why*, not the *how*.

---

## Code of Conduct

This project follows the [Contributor Covenant Code of Conduct](CODE_OF_CONDUCT.md). By participating, you agree to its terms.
