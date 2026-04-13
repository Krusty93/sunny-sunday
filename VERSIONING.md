# Versioning guide

This project uses independent semantic versioning for each component (`core`, `cli`, `server`).
Tag creation, CHANGELOG generation, and GitHub Releases are fully automated.

## How to release a new version

### 1. In your feature branch, bump the version in each modified component

Open the `.csproj` of every component you changed and update `<Version>`:

```xml
<Version>1.3.0</Version>
```

Relevant files:

| Component | File |
|-----------|------|
| `core`   | `src/SunnySunday.Core/SunnySunday.Core.csproj`     |
| `cli`    | `src/SunnySunday.Cli/SunnySunday.Cli.csproj`       |
| `server` | `src/SunnySunday.Server/SunnySunday.Server.csproj` |

Decide the bump level yourself (patch / minor / major) — no commit message format is required.

### 2. Commit and open a PR

```bash
git add src/SunnySunday.Cli/SunnySunday.Cli.csproj
git commit -m "your commit message"
git push origin feature/your-branch
```

The CI will check that every modified component has a bumped version and post a comment on the PR with the result. The merge is blocked until all checks pass.

### 3. Merge the PR — everything else is automatic

After the merge:

- `post-merge.yml` runs `versionize` for each component with a new version, generating the CHANGELOG entry and creating the git tag (format: `<component>/v<version>`)
- The tag push triggers `release.yml`, which builds the CLI self-contained binaries and creates the GitHub Release
- The published GitHub Release triggers `deploy-cli.yml` (Docker image) and `deploy-server.yml` (Docker image)

## Tag format

Tags follow the pattern `<component>/v<version>`:

```
core/v1.1.0
cli/v1.3.0
server/v2.0.0
```

## Adding a new component

1. Create `src/<Name>/<Name>.csproj` with `<Version>0.1.0</Version>`
2. Add `dotnet sln src/SunnySunday.slnx add src/<Name>/<Name>.csproj`
3. Add `{ "name": "<name>", "path": "src/<Name>" }` to `.versionize`
4. The CI gate and `post-merge.yml` pick it up automatically — no workflow changes needed
5. If the new component produces a Docker image, create `.github/workflows/deploy-<name>.yml` following the pattern in `deploy-server.yml`

## Required one-time setup (repository owner)

| Secret | Scope | Used by |
|--------|-------|---------|
| `RELEASE_TOKEN` | classic PAT, `repo` | `post-merge.yml` — pushes tags in a way that triggers downstream workflows |

Branch protection on `main`:

- Require PR before merging
- Required status checks: `Build & Test`, `Version Bump Check`

> **Note:** `GITHUB_TOKEN` cannot trigger other workflows when used to push tags, which is why `RELEASE_TOKEN` is required.
