#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Agentic dependency update script for McpSecurity.
    Updates NuGet packages (MCP, .NET platform, and all others), updates the .NET SDK
    version referenced in CI workflow files, and emits a structured summary for use
    in GitHub Actions PR / issue bodies.

.PARAMETER DryRun
    Report what would change without writing any files.

.OUTPUTS
    Writes GITHUB_OUTPUT entries (if running inside GitHub Actions):
      packages_changed  – 'true' | 'false'
      update_summary    – markdown table of old → new versions
      sdk_changed       – 'true' | 'false'
      new_sdk_version   – e.g. "10.0.200"
#>
param(
    [switch]$DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ── Helpers ──────────────────────────────────────────────────────────────────

function Write-Step([string]$msg) { Write-Host "`n▶ $msg" -ForegroundColor Cyan }
function Write-Ok([string]$msg)   { Write-Host "  ✔ $msg" -ForegroundColor Green }
function Write-Warn([string]$msg) { Write-Host "  ⚠ $msg" -ForegroundColor Yellow }

function Set-GHOutput([string]$name, [string]$value) {
    if ($env:GITHUB_OUTPUT) {
        # Multiline-safe: use the heredoc delimiter syntax
        $delim = [System.Guid]::NewGuid().ToString('N')
        Add-Content -Path $env:GITHUB_OUTPUT -Value "${name}<<${delim}"
        Add-Content -Path $env:GITHUB_OUTPUT -Value $value
        Add-Content -Path $env:GITHUB_OUTPUT -Value $delim
    }
}

# ── 1. Ensure dotnet-outdated-tool is available ───────────────────────────────

Write-Step "Ensuring dotnet-outdated-tool is installed"
$toolList = dotnet tool list --global 2>&1 | Out-String
if ($toolList -notmatch 'dotnet-outdated-tool') {
    dotnet tool install --global dotnet-outdated-tool | Out-Null
    Write-Ok "Installed dotnet-outdated-tool"
} else {
    Write-Ok "dotnet-outdated-tool already present"
}

# Refresh PATH so the tool is usable immediately
$env:PATH = [System.Environment]::GetEnvironmentVariable('PATH','Machine') + ';' +
            [System.Environment]::GetEnvironmentVariable('PATH','User') + ';' +
            (Join-Path $env:USERPROFILE '.dotnet/tools') + ';' +
            '/root/.dotnet/tools'   # Linux fallback

# ── 2. Snapshot current package versions from all .csproj files ───────────────

Write-Step "Snapshotting current package versions"

$repoRoot  = Resolve-Path (Join-Path $PSScriptRoot '..')
$csprojFiles = Get-ChildItem -Recurse -Path $repoRoot -Filter '*.csproj'

$before = [ordered]@{}
foreach ($proj in $csprojFiles) {
    [xml]$xml = Get-Content $proj.FullName -Encoding UTF8
    $xml.SelectNodes('//PackageReference') | ForEach-Object {
        $key = "$($proj.Name)::$($_.Include)"
        $before[$key] = $_.Version
    }
}
Write-Ok "Snapshotted $($before.Count) package references across $($csprojFiles.Count) projects"

# ── 3. Run dotnet outdated --upgrade ──────────────────────────────────────────
# --pre-release Auto  →  packages already on a prerelease get bumped to the
#                        latest prerelease; stable packages stay on stable.

Write-Step "Running dotnet outdated --upgrade (pre-release: Auto)"
$solutionFile = Get-ChildItem -Path $repoRoot -Filter '*.sln' | Select-Object -First 1
if (-not $solutionFile) { throw "No .sln file found under $repoRoot" }

$outdatedArgs = @(
    'outdated'
    $solutionFile.FullName
    '--pre-release', 'Auto'
)
if (-not $DryRun) { $outdatedArgs += '--upgrade' }

& dotnet @outdatedArgs
$outdatedExit = $LASTEXITCODE

# dotnet-outdated exits 0 (no updates) or 2 (updates applied); 1 = error
if ($outdatedExit -eq 1) { throw "dotnet outdated exited with error code 1" }

# ── 4. Collect changes (diff between before/after) ────────────────────────────

Write-Step "Collecting package version changes"

$after = [ordered]@{}
foreach ($proj in $csprojFiles) {
    [xml]$xml = Get-Content $proj.FullName -Encoding UTF8
    $xml.SelectNodes('//PackageReference') | ForEach-Object {
        $key = "$($proj.Name)::$($_.Include)"
        $after[$key] = $_.Version
    }
}

$changes = @()
foreach ($key in $after.Keys) {
    $oldVer = if ($before.Contains($key)) { $before[$key] } else { '(new)' }
    $newVer = $after[$key]
    if ($oldVer -ne $newVer) {
        $parts = $key -split '::'
        $changes += [pscustomobject]@{
            Project   = $parts[0]
            Package   = $parts[1]
            OldVersion = $oldVer
            NewVersion = $newVer
        }
    }
}

$packagesChanged = $changes.Count -gt 0

if ($packagesChanged) {
    Write-Ok "$($changes.Count) package(s) updated:"
    $changes | Format-Table -AutoSize | Out-String | Write-Host
} else {
    Write-Ok "No package updates found — everything is up to date."
}

# Build markdown summary table
$mdTable = @()
$mdTable += '| Project | Package | Old Version | New Version |'
$mdTable += '|---------|---------|-------------|-------------|'
foreach ($c in $changes) {
    # Highlight MCP packages
    $pkg = if ($c.Package -match 'ModelContextProtocol') { "**$($c.Package)**" } else { $c.Package }
    $mdTable += "| $($c.Project) | $pkg | ``$($c.OldVersion)`` | ``$($c.NewVersion)`` |"
}
$updateSummary = $mdTable -join "`n"

# ── 5. Update .NET SDK version in workflow YAML files ─────────────────────────

Write-Step "Checking .NET SDK version in CI workflow files"

# Resolve the current SDK version from `dotnet --version`
$currentSdk = (dotnet --version).Trim()   # e.g. "10.0.100"
$majorMinor  = ($currentSdk -split '\.')[0..1] -join '.'  # "10.0"

$workflowDir   = Join-Path $repoRoot '.github' 'workflows'
$yamlFiles     = Get-ChildItem -Path $workflowDir -Filter '*.yml' -ErrorAction SilentlyContinue
$sdkChanged    = $false
$newSdkVersion = $currentSdk

foreach ($yml in $yamlFiles) {
    $content = Get-Content $yml.FullName -Raw
    # Match patterns like: 10.0.100  (exact patch release, not the wildcard .x alias)
    # Build the pattern safely to avoid PowerShell misinterpreting regex character classes
    $escapedMM  = [regex]::Escape($majorMinor)
    $pattern    = $escapedMM + '\.\d+'

    if ($content -match $pattern) {
        $existingVersion = $Matches[0]
        # Only rewrite when it's an exact version that differs from what the runner has
        if ($existingVersion -ne $currentSdk -and
            $existingVersion -notmatch '\.x$' -and
            $content -match [regex]::Escape($existingVersion)) {
            if (-not $DryRun) {
                $updated = $content -replace [regex]::Escape($existingVersion), $currentSdk
                Set-Content -Path $yml.FullName -Value $updated -Encoding UTF8 -NoNewline
            }
            Write-Ok "  $($yml.Name): $existingVersion → $currentSdk"
            $sdkChanged = $true
        }
    }
}

# Also keep the wildcard alias (e.g. 10.0.x) up to date with the major.minor
foreach ($yml in $yamlFiles) {
    $content        = Get-Content $yml.FullName -Raw
    $escapedMM      = [regex]::Escape($majorMinor)
    $wildcardPattern = $escapedMM + '\.x'
    if ($content -notmatch $wildcardPattern) {
        # If an older major.minor wildcard is present, update it
        $oldWildcard = '\d+\.\d+\.x'
        if ($content -match $oldWildcard) {
            $found = $Matches[0]
            if ($found -ne "$majorMinor.x") {
                if (-not $DryRun) {
                    $updated = $content -replace [regex]::Escape($found), "$majorMinor.x"
                    Set-Content -Path $yml.FullName -Value $updated -Encoding UTF8 -NoNewline
                }
                Write-Ok "  $($yml.Name): SDK wildcard $found → $majorMinor.x"
                $sdkChanged = $true
            }
        }
    }
}

if (-not $sdkChanged) { Write-Ok "SDK version references are already current ($currentSdk)" }

# ── 6. Emit GitHub Actions outputs ────────────────────────────────────────────

Set-GHOutput 'packages_changed' ($packagesChanged.ToString().ToLower())
Set-GHOutput 'update_summary'   $updateSummary
Set-GHOutput 'sdk_changed'      ($sdkChanged.ToString().ToLower())
Set-GHOutput 'new_sdk_version'  $newSdkVersion

Write-Step "Done"
Write-Host "  packages_changed : $packagesChanged"
Write-Host "  sdk_changed      : $sdkChanged"
