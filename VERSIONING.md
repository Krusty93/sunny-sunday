# Versioning guide

This project uses independent semantic versioning for each component (`core`, `cli`, `server`).
Version numbers follow the [Semantic Versioning](https://semver.org/) 2.0.0 specification.
Tag creation and GitHub Releases are fully automated for CLI and server projects.

## How to release a new version

### 1. In your feature branch, bump the version in each modified component

Open the `.csproj` of every component you changed and update `<Version>`:

```xml
<Version>1.3.0</Version>
```

Relevant files:

| Component | File                                               |
|-----------|----------------------------------------------------|
| `core`    | `src/SunnySunday.Core/SunnySunday.Core.csproj`     |
| `cli`     | `src/SunnySunday.Cli/SunnySunday.Cli.csproj`       |
| `server`  | `src/SunnySunday.Server/SunnySunday.Server.csproj` |

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

- `post-merge.yml` discovers projects under `src/` that declare an explicit `<Version>` in their `.csproj` and creates a git tag for each component whose bumped version does not yet have a matching tag (format: `<component>/v<version>`)
- The tag push triggers `release.yml`, which publishes one GitHub Release per component tag using the squash-merge commit titles since the previous component tag, with a link to the originating PR when available
- The published GitHub Release triggers `deploy-cli.yml` (Docker image) and `deploy-server.yml` (Docker image)

`core` is version-tagged for dependency tracking but is excluded from GitHub release notes and release pages.

## Tag format

Tags follow the pattern `<component>/v<version>`:

```text
core/v1.1.0
cli/v1.3.0
server/v2.0.0
```

## Adding a new component

1. Create `src/<Name>/<Name>.csproj` with `<Version>0.1.0</Version>`
2. Add `dotnet sln src/SunnySunday.slnx add src/<Name>/<Name>.csproj`
3. Ensure the project folder and `.csproj` file use the `SunnySunday.<Component>` naming pattern so the workflow discovery action derives the correct component tag name
4. Ensure the `.csproj` declares an explicit `<Version>` value; projects without `<Version>` are ignored by the release automation
5. If the component should be publish a GitHub Release page, update triggers in `.github/workflows/release.yaml` to include it
6. Create the deployment workflow for the specific component, following the triggers in `deploy-server.yml`

## Required one-time setup (repository owner)

| Secret          | Scope                                   | Used by                                                                    |
| --------------- | --------------------------------------- | -------------------------------------------------------------------------- |
| `RELEASE_TOKEN` | fine-grained PAT, `contents:read+write` | `post-merge.yml` — pushes tags in a way that triggers downstream workflows |

Branch protection on `main`:

- Require PR before merging
- Required status checks: `Build & Test`, `Version Bump Check`

> **Note:** `GITHUB_TOKEN` cannot trigger other workflows when used to push tags, which is why `RELEASE_TOKEN` is required.
