<#
.SYNOPSIS
    GamePort Cashier V2 — Local release packaging script.
.DESCRIPTION
    Publishes the application, verifies artifacts, and generates
    Velopack packages locally. Does NOT create GitHub Releases,
    upload files, or create git tags.
.PARAMETER Version
    Semantic version (e.g. 2.0.0-beta.1)
.PARAMETER Channel
    Release channel (default: beta)
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [Parameter(Mandatory = $false)]
    [string]$Channel = "beta"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$srcProject = Join-Path $repoRoot "src\GamePort.Cashier\GamePort.Cashier.csproj"
$testsDir = Join-Path $repoRoot "tests"
$solution = Join-Path $repoRoot "GamePort.Cashier.sln"
$releaseDir = Join-Path $repoRoot "release\$Version"
$publishDir = Join-Path $releaseDir "publish"
$vpkOutputDir = Join-Path $releaseDir "velopack"

Write-Host "=== GamePort Cashier V2 Release ===" -ForegroundColor Cyan
Write-Host "Version: $Version" -ForegroundColor Yellow
Write-Host "Channel: $Channel" -ForegroundColor Yellow
Write-Host ""

# --- Step 1: Validate version format ---
if ($Version -notmatch '^\d+\.\d+\.\d+(-[a-zA-Z0-9.]+)?$') {
    Write-Host "ERROR: Invalid version format '$Version'. Expected: 2.0.0 or 2.0.0-beta.1" -ForegroundColor Red
    exit 1
}
Write-Host "[1/10] Version validated: $Version" -ForegroundColor Green

# --- Step 2: Run tests ---
Write-Host "[2/10] Running tests..." -ForegroundColor Yellow
Push-Location $repoRoot
try {
    $testProjects = Get-ChildItem -Path $testsDir -Recurse -Filter "*.csproj" | Where-Object { $_.Name -match "Test" }
    foreach ($proj in $testProjects) {
        Write-Host "  Testing: $($proj.Name)" -ForegroundColor Cyan
        dotnet test $proj.FullName --verbosity minimal --nologo 2>&1 | Write-Host
        if ($LASTEXITCODE -ne 0) {
            Write-Host "ERROR: Tests in $($proj.Name) failed. Release aborted." -ForegroundColor Red
            exit 1
        }
    }
    Write-Host "[2/10] All tests passed" -ForegroundColor Green
} finally {
    Pop-Location
}

# --- Step 3: Clean release directories ---
Write-Host "[3/10] Cleaning release directories..." -ForegroundColor Yellow
if (Test-Path $releaseDir) { Remove-Item $releaseDir -Recurse -Force }
New-Item -ItemType Directory -Path $publishDir -Force | Out-Null
New-Item -ItemType Directory -Path $vpkOutputDir -Force | Out-Null
Write-Host "[3/10] Cleaned" -ForegroundColor Green

# --- Step 4: Publish ---
Write-Host "[4/10] Publishing (Release, win-x64, self-contained)..." -ForegroundColor Yellow
Push-Location $repoRoot
try {
    dotnet publish $srcProject `
        -c Release `
        -r win-x64 `
        --self-contained true `
        -o $publishDir `
        -p:Version=$Version `
        -p:PublishSingleFile=false `
        -p:PublishTrimmed=false `
        --nologo 2>&1 | Write-Host

    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Publish failed. Release aborted." -ForegroundColor Red
        exit 1
    }
    Write-Host "[4/10] Published to $publishDir" -ForegroundColor Green
} finally {
    Pop-Location
}

# --- Step 5: Verify main executable ---
Write-Host "[5/10] Verifying main executable..." -ForegroundColor Yellow
$mainExe = Join-Path $publishDir "GamePort.Cashier.exe"
if (-not (Test-Path $mainExe)) {
    Write-Host "ERROR: GamePort.Cashier.exe not found in publish output." -ForegroundColor Red
    exit 1
}
$exeSize = (Get-Item $mainExe).Length / 1MB
Write-Host "[5/10] GamePort.Cashier.exe found ($([math]::Round($exeSize,1)) MB)" -ForegroundColor Green

# --- Step 6: Verify native dependencies ---
Write-Host "[6/10] Verifying native dependencies..." -ForegroundColor Yellow
# Self-contained publish flattens runtimes/win-x64/native/* to publish root
$requiredNative = @(
    "assimp.dll",
    "HelixToolkit.Wpf.SharpDX.dll",
    "HelixToolkit.SharpDX.dll",
    "HelixToolkit.SharpDX.Assimp.dll",
    "SharpDX.dll",
    "SharpDX.Direct3D11.dll",
    "Velopack.dll"
)
$missing = @()
foreach ($dll in $requiredNative) {
    $path = Join-Path $publishDir $dll
    if (Test-Path $path) {
        Write-Host "  OK: $dll" -ForegroundColor Green
    } else {
        Write-Host "  MISSING: $dll" -ForegroundColor Red
        $missing += $dll
    }
}
if ($missing.Count -gt 0) {
    Write-Host "ERROR: Missing native dependencies: $($missing -join ', ')" -ForegroundColor Red
    exit 1
}
Write-Host "[6/10] All native dependencies verified" -ForegroundColor Green

# --- Step 7: Verify GLB assets ---
Write-Host "[7/10] Verifying GLB assets..." -ForegroundColor Yellow
$glbFiles = Get-ChildItem -Path $publishDir -Recurse -Filter "*.glb" -ErrorAction SilentlyContinue
if ($glbFiles.Count -eq 0) {
    Write-Host "WARNING: No GLB files found in publish output." -ForegroundColor Yellow
} else {
    foreach ($glb in $glbFiles) {
        $relPath = $glb.FullName.Replace($publishDir, "").TrimStart("\")
        $sizeKB = [math]::Round($glb.Length / 1KB, 0)
        Write-Host "  OK: $relPath ($sizeKB KB)" -ForegroundColor Green
    }
}
Write-Host "[7/10] GLB assets checked" -ForegroundColor Green

# --- Step 8: Security check — scan for secrets in config files ---
Write-Host "[8/10] Scanning for secrets in publish config..." -ForegroundColor Yellow
$configFiles = @()
$appsettings = Join-Path $publishDir "appsettings.json"
if (Test-Path $appsettings) { $configFiles += $appsettings }

$secretPatterns = @("password", "secret", "token", "api.?key", "connectionstring")
$secretFound = $false
foreach ($file in $configFiles) {
    $content = Get-Content $file -Raw
    foreach ($pattern in $secretPatterns) {
        if ($content -match $pattern) {
            # Check if it's a real secret or just a config key name
            if ($content -match "(?i)($pattern)\s*[:=]\s*`"([^`"]+)`"") {
                $matches_found = [regex]::Matches($content, "(?i)($pattern)\s*[:=]\s*`"([^`"]+)`"")
                foreach ($m in $matches_found) {
                    $value = $m.Groups[2].Value
                    if ($value -ne "" -and $value -ne "null" -and $value -ne "{}") {
                        Write-Host "  WARNING: Possible secret in $file (pattern: $pattern)" -ForegroundColor Yellow
                        $secretFound = $true
                    }
                }
            }
        }
    }
}
if ($secretFound) {
    Write-Host "[8/10] WARNING: Review config files before publishing!" -ForegroundColor Yellow
} else {
    Write-Host "[8/10] No obvious secrets found in publish config" -ForegroundColor Green
}

# --- Step 9: Run vpk pack ---
Write-Host "[9/10] Packaging with Velopack (vpk)..." -ForegroundColor Yellow
$iconPath = Join-Path $repoRoot "installer\cashier.ico"
$vpkArgs = @(
    "pack",
    "--packId", "GamePortCashier",
    "--packVersion", $Version,
    "--packDir", $publishDir,
    "--mainExe", "GamePort.Cashier.exe",
    "--packTitle", "GamePort Cashier",
    "--packAuthors", "GamePort",
    "--outputDir", $vpkOutputDir,
    "--channel", $Channel,
    "--runtime", "win-x64",
    "--exclude", "(?i)appsettings\.json|.*\.pdb"
)
if (Test-Path $iconPath) {
    $vpkArgs += @("--icon", $iconPath)
}

& vpk @vpkArgs 2>&1 | Write-Host

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: vpk pack failed (exit code $LASTEXITCODE)." -ForegroundColor Red
    exit 1
}
Write-Host "[9/10] Velopack packaging complete" -ForegroundColor Green

# --- Step 10: Report artifacts ---
Write-Host "[10/10] Generated artifacts:" -ForegroundColor Yellow
Write-Host ""
$artifacts = Get-ChildItem -Path $vpkOutputDir -Recurse -File -ErrorAction SilentlyContinue
if ($artifacts.Count -eq 0) {
    Write-Host "  WARNING: No artifacts found in $vpkOutputDir" -ForegroundColor Yellow
} else {
    Write-Host "  Directory: $vpkOutputDir" -ForegroundColor Cyan
    Write-Host ""
    foreach ($artifact in $artifacts) {
        $relPath = $artifact.FullName.Replace($vpkOutputDir, "").TrimStart("\")
        $sizeMB = [math]::Round($artifact.Length / 1MB, 2)
        $ext = $artifact.Extension.ToLower()
        $purpose = switch ($ext) {
            ".exe"  { "Installer" }
            ".nupkg" { "Velopack update package" }
            ".zip"  { "Velopack full package" }
            ".blockmap" { "Delta blockmap" }
            default { "File" }
        }
        Write-Host ("  {0,-50} {1,10} MB  {2}" -f $relPath, $sizeMB, $purpose)
    }
}

Write-Host ""
Write-Host "=== Release packaging complete ===" -ForegroundColor Cyan
Write-Host "Artifacts location: $vpkOutputDir" -ForegroundColor Yellow
Write-Host "NO GitHub Release created. NO files uploaded. NO git tag created." -ForegroundColor Yellow
