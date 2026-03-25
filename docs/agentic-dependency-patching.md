# Agentic Dependency Patching Workflow

## Overview

This workflow automatically keeps all NuGet packages and .NET SDK version references up to date by delegating the entire update process to the **GitHub Copilot coding agent**.

Instead of applying updates itself, the workflow detects what is outdated and opens a GitHub Issue assigned to Copilot. The agent then handles everything: updating packages, fixing breaking changes, updating SDK references, running tests, and opening a pull request.

---

## Trigger

| Trigger | Schedule / Condition |
|---------|----------------------|
| Scheduled | Every **Monday at 06:00 UTC** |
| Manual | `workflow_dispatch` from the Actions tab, with an optional **dry-run** mode |

---

## Flow

```
┌─────────────────────────────────────────────────┐
│  Trigger (schedule or manual)                   │
└────────────────────┬────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────┐
│  Setup .NET SDK (latest 10.0.x patch)           │
└────────────────────┬────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────┐
│  dotnet list package --outdated                 │
│  --include-prerelease                           │
└────────────────────┬────────────────────────────┘
                     │
          ┌──────────┴──────────┐
          │ Updates found?      │
         No                    Yes
          │                     │
          ▼                     ▼
       Done ✅       ┌──────────────────────┐
                     │ Dry-run mode?        │
                     │ → print & stop       │
                     └──────────┬───────────┘
                                │ No
                                ▼
                     ┌──────────────────────┐
                     │ Open issue already   │
                     │ exists? → skip       │
                     └──────────┬───────────┘
                                │ No
                                ▼
                     ┌──────────────────────┐
                     │ Open GitHub Issue    │
                     │ assigned to Copilot  │
                     │ coding agent         │
                     └──────────┬───────────┘
                                │
                     ┌──────────▼───────────┐
                     │  Copilot agent acts  │
                     │  autonomously (see   │
                     │  Agent Instructions) │
                     └──────────────────────┘
```

---

## Agent Instructions

The issue opened by the workflow instructs the Copilot coding agent to carry out these steps in order:

### 1 — Update NuGet packages

```bash
dotnet tool install --global dotnet-outdated-tool
dotnet outdated Mcp.sln --upgrade --pre-release Auto
```

**`--pre-release Auto` rules:**
- Package currently on a **prerelease** → upgrade to the latest prerelease of the **same major version**
- Package currently on a **stable** version → upgrade to the latest **stable** version only
- **Never** jump to a higher major version (e.g. do not move `Microsoft.Extensions.*` from `10.x` to `11.0.0-preview.*`)

### 2 — Fix breaking changes

- Run `dotnet build Mcp.sln --configuration Release`
- Fix only the code broken by the updated packages — no unrelated refactoring
- Pay special attention to `ModelContextProtocol` and `ModelContextProtocol.AspNetCore`, which moved from `0.x` preview to `1.x` stable and may have breaking API changes

### 3 — Update .NET SDK version in CI workflow files

- Run `dotnet --version` to get the installed SDK version (e.g. `10.0.201`)
- In every `.github/workflows/*.yml`, update any pinned patch version that is older than the current SDK
- Leave wildcard entries like `10.0.x` unchanged
- Do **not** change the major.minor (e.g. do not switch `10.0.x` → `11.0.x`)

### 4 — Run tests

```bash
dotnet test Mcp.sln --no-build --configuration Release
```

Fix any test failures caused by the updated packages.

### 5 — Open a pull request

- Branch: `deps/auto-update-YYYY-MM-DD`
- Target: `main`
- Title: `chore: update dependencies (YYYY-MM-DD)`
- Body must include a markdown table of every package that changed (old → new version)
- Labels: `dependencies`, `automated`

---

## Files

| File | Purpose |
|------|---------|
| `.github/workflows/patch-dependencies.yml` | The GitHub Actions workflow definition |
| `docs/agentic-dependency-patching.md` | This document |

---

## One-time setup

Enable the Copilot coding agent in repository settings so that issues can be assigned to it:

**Settings → Copilot → Coding agent → Enable**

---

## Package strategy

| Package group | Current version | Update strategy |
|---------------|----------------|-----------------|
| `ModelContextProtocol` | `0.3.0-preview.4` | Latest stable or prerelease (same major) |
| `ModelContextProtocol.AspNetCore` | `0.3.0-preview.4` | Latest stable or prerelease (same major) |
| `Microsoft.Extensions.*` | `10.x` | Latest stable `10.x` only |
| `Microsoft.AspNetCore.*` | `10.x` | Latest stable `10.x` only |
| `Microsoft.Identity.Web` | `4.x` | Latest stable `4.x` only |
| `Microsoft.Identity.Client` | `4.x` | Latest stable `4.x` only |
| `Azure.AI.OpenAI` | `2.x` | Latest stable `2.x` only |
| `Microsoft.Extensions.AI` | `10.x` | Latest stable `10.x` only |
