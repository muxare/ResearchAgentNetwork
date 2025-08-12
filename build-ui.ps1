# Builds the SvelteKit UI under `ui/app/` and deploys it into `ResearchAgentNetwork.Web/wwwroot/<BasePath>`
# Usage examples:
#   pwsh ./build-ui.ps1                  # builds with BasePath /v2 and copies to wwwroot/v2
#   pwsh ./build-ui.ps1 -BasePath /v3    # builds with BasePath /v3 and copies to wwwroot/v3
#   pwsh ./build-ui.ps1 -SkipInstall     # skip npm install/ci

param(
    [string]$BasePath = "/v2",
    [switch]$SkipInstall,
    [switch]$ForceClean
)

$ErrorActionPreference = "Stop"

function Write-Info($msg) { Write-Host "[info] $msg" -ForegroundColor Cyan }
function Write-Err($msg) { Write-Host "[error] $msg" -ForegroundColor Red }

# Resolve paths relative to repo root
$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $repoRoot

$uiDir = Join-Path $repoRoot "ui/app"
$webRoot = Join-Path $repoRoot "ResearchAgentNetwork.Web"
if (-not (Test-Path $webRoot)) { Write-Err "Cannot find ResearchAgentNetwork.Web at $webRoot"; exit 1 }

if (-not (Test-Path $uiDir)) { Write-Err "UI directory not found: $uiDir"; exit 1 }

# Check Node/npm
try {
    $nodeVersion = node --version 2>$null
    if (-not $nodeVersion) { throw "Node.js not found" }
    Write-Info "Node $nodeVersion"
} catch {
    Write-Err "Node.js is required. Install from https://nodejs.org and try again."
    exit 1
}

Set-Location $uiDir

# Optional force clean to avoid Windows file locks
if ($ForceClean) {
    Write-Info "Force cleaning node_modules and build artifacts"
    try { Remove-Item -Recurse -Force (Join-Path $uiDir "node_modules") -ErrorAction SilentlyContinue } catch {}
    try { Remove-Item -Recurse -Force (Join-Path $uiDir ".svelte-kit") -ErrorAction SilentlyContinue } catch {}
    try { Remove-Item -Recurse -Force (Join-Path $uiDir "build") -ErrorAction SilentlyContinue } catch {}
    try { Remove-Item -Force (Join-Path $uiDir "package-lock.json") -ErrorAction SilentlyContinue } catch {}
}

if (-not $SkipInstall) {
    if (Test-Path (Join-Path $uiDir "package-lock.json")) {
        Write-Info "Installing dependencies (npm ci)"
        npm ci
        if ($LASTEXITCODE -ne 0) {
            Write-Err "Dependency install failed (npm ci). Files may be locked by another process (e.g. antivirus) or open in your editor. Try closing apps or pass -SkipInstall."
            exit 1
        }
    } else {
        Write-Info "Installing dependencies (npm install)"
        npm install
        if ($LASTEXITCODE -ne 0) {
            Write-Err "Dependency install failed (npm install). Try closing apps or pass -SkipInstall."
            exit 1
        }
    }
} else {
    Write-Info "Skipping dependency install per -SkipInstall"
    # Preflight: ensure required plugin exists
    $pluginPath = Join-Path $uiDir "node_modules/@sveltejs/vite-plugin-svelte/index.js"
    if (-not (Test-Path $pluginPath)) {
        Write-Err "Missing dependencies detected (e.g. vite-plugin-svelte). Re-run without -SkipInstall or pass -ForceClean to reinstall."
        exit 1
    }
}

# Build with base path for side-by-side hosting
Write-Info "Building SvelteKit with BASE_PATH = $BasePath"
$env:BASE_PATH = $BasePath
npm run build
if ($LASTEXITCODE -ne 0) {
    Write-Err "Build failed. See errors above. Try -ForceClean, then re-run without -SkipInstall."
    exit 1
}

# Copy build to wwwroot/<BasePath>
$trimBase = $BasePath.TrimStart('/').TrimEnd('/')
if ([string]::IsNullOrWhiteSpace($trimBase)) { $trimBase = "" }

$target = if ($trimBase -eq "") { Join-Path $webRoot "wwwroot" } else { Join-Path (Join-Path $webRoot "wwwroot") $trimBase }

Write-Info "Deploy target: $target"
if (-not (Test-Path $target)) { New-Item -ItemType Directory -Path $target | Out-Null }

Write-Info "Cleaning target"
Get-ChildItem -Path $target -Force -Recurse | Remove-Item -Force -Recurse -ErrorAction SilentlyContinue

$buildDir = Join-Path $uiDir "build"
if (-not (Test-Path $buildDir)) { Write-Err "Build directory not found: $buildDir"; exit 1 }

Write-Info "Copying build artifacts"
Copy-Item -Recurse -Force (Join-Path $buildDir '*') $target

Write-Host "Done. UI available under base path: $BasePath" -ForegroundColor Green

