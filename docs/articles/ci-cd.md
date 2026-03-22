# Continuous Deployment

This document describes the CI/CD pipeline for Fleece, including release automation and authentication setup.

## Overview

Fleece uses GitHub Actions for continuous integration and deployment:

1. **CI Workflow** (`ci.yml`) - Runs on every push/PR to validate builds and tests
2. **Release Workflow** (`release.yml`) - Publishes to NuGet when a version tag is pushed

## Release Process

The release workflow follows this sequence:

```
Tag push (v*.*.*) → Build → Test → Pack → Publish to NuGet → Update source version
```

### Triggering a Release

1. Ensure all changes are merged to `main`
2. Create and push a version tag:
   ```bash
   git tag v1.2.0
   git push origin v1.2.0
   ```
3. The release workflow automatically:
   - Builds the solution with the tagged version
   - Runs all tests
   - Packs NuGet packages
   - Publishes to NuGet.org
   - Updates `Directory.Build.props` with the released version
   - Commits the version update to `main`

### Version Management

- **Source version**: Stored in `Directory.Build.props` as `<Version>X.Y.Z</Version>`
- **Build version**: Passed via `-p:Version=` parameter (overrides source during CI)
- **Tag convention**: `v` prefix followed by semantic version (e.g., `v1.2.0`, `v1.2.0-beta.1`)

The source version is updated **after** successful NuGet publish to ensure the repository never contains a version that wasn't actually released.

## Authentication Setup

### NuGet API Key

Required for publishing packages to NuGet.org.

1. Go to [NuGet.org API Keys](https://www.nuget.org/account/apikeys)
2. Create a new API key:
   - **Key Name**: `Fleece-GitHub-Actions`
   - **Expiration**: 365 days (set reminder to rotate)
   - **Scopes**: Push new packages and package versions
   - **Glob Pattern**: `Fleece.*`
3. Copy the generated key
4. Add to repository secrets:
   - Go to GitHub repo → Settings → Secrets and variables → Actions
   - Create secret: `NUGET_API_KEY`
   - Paste the API key value

### VERSION_UPDATE_PAT

Required for the workflow to push version updates back to the repository.

A Personal Access Token (PAT) is needed because the default `GITHUB_TOKEN` cannot trigger workflows or push to protected branches.

#### Creating the Token

1. Go to GitHub → Settings → Developer settings → Personal access tokens → Fine-grained tokens
2. Click "Generate new token"
3. Configure the token:
   - **Token name**: `Fleece-Release-Version-Update`
   - **Expiration**: 90 days (or your preferred rotation schedule)
   - **Repository access**: Only select repositories → `nick-boey/Fleece`
   - **Permissions**:
     - Contents: Read and write
4. Click "Generate token"
5. Copy the token immediately (it won't be shown again)

#### Adding as Repository Secret

1. Go to GitHub repo → Settings → Secrets and variables → Actions
2. Click "New repository secret"
3. Name: `VERSION_UPDATE_PAT`
4. Value: Paste the token
5. Click "Add secret"

### Token Rotation

Set calendar reminders to rotate tokens before expiration:

| Secret | Typical Expiration | Rotation Steps |
|--------|-------------------|----------------|
| `NUGET_API_KEY` | 365 days | Regenerate on NuGet.org, update secret |
| `VERSION_UPDATE_PAT` | 90 days | Generate new PAT, update secret, delete old |

## Branch Protection Considerations

If `main` has branch protection rules, you may need to:

1. **Allow the bot to bypass**: Add `github-actions[bot]` to the bypass list for push rules
2. **Or use a PR-based approach**: Modify the workflow to create a PR instead of direct push

The current implementation uses `continue-on-error: true` on the commit step, so a failed push won't fail the release.

## CI Loop Prevention

The version update commit includes `[skip ci]` in the message to prevent triggering additional workflow runs:

```
chore: update version to 1.2.0 [skip ci]
```

## Troubleshooting

### Release workflow fails at "Publish to NuGet"

- **403 Forbidden**: API key lacks push permissions or is expired
- **409 Conflict**: Package version already exists (use `--skip-duplicate` to handle)
- **401 Unauthorized**: Invalid or missing `NUGET_API_KEY` secret

### Version update commit fails

- **403 Forbidden**: `VERSION_UPDATE_PAT` is missing, expired, or lacks write permission
- **Push rejected**: Branch protection rules blocking the push
- **No changes to commit**: Version was already updated (not an error)

### Workflow not triggering

- Ensure the tag follows the `v*.*.*` pattern
- Check that the tag was pushed to the remote (not just created locally)

### Tests pass locally but fail in CI

- Check .NET version compatibility (workflow uses .NET 10 preview)
- Ensure all dependencies are restored properly

## Workflow Files

- **CI**: `.github/workflows/ci.yml`
- **Release**: `.github/workflows/release.yml`

## Step-by-Step: Creating a Release

1. **Verify main is ready**
   ```bash
   git checkout main
   git pull origin main
   dotnet build
   dotnet test
   ```

2. **Determine version number**
   - Check current version in `Directory.Build.props`
   - Follow [Semantic Versioning](https://semver.org/)

3. **Create and push tag**
   ```bash
   git tag v1.2.0
   git push origin v1.2.0
   ```

4. **Monitor the release**
   - Go to Actions tab in GitHub
   - Watch the "Release" workflow progress
   - Verify package appears on NuGet.org

5. **Verify version update**
   - Pull latest main: `git pull origin main`
   - Check `Directory.Build.props` shows the released version
