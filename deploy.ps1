param(
    [ValidateSet("1.21", "1.22")]
    [string]$GameVersion = "1.22",

    [string]$Configuration = "Release",

    [string]$ModsDir = "C:\Users\chris\AppData\Roaming\VintagestoryData\Mods",

    [switch]$NoLaunch,

    [switch]$NoCloseVS
)

$ErrorActionPreference = "Stop"

$ProjectRoot = $PSScriptRoot
$ProjectFile = Join-Path $ProjectRoot "joyofsailing2.csproj"
$VersionKey = $GameVersion -replace "\.", ""
$VersionEnvName = "VINTAGE_STORY_$VersionKey"
$GamePath = [Environment]::GetEnvironmentVariable($VersionEnvName, "User")

if ([string]::IsNullOrWhiteSpace($GamePath)) {
    throw "Environment variable '$VersionEnvName' is not set."
}

if (-not (Test-Path -LiteralPath $GamePath)) {
    throw "Configured game path '$GamePath' does not exist."
}

$VSExePath = Join-Path $GamePath "Vintagestory.exe"
if (-not (Test-Path -LiteralPath $VSExePath)) {
    throw "Could not find Vintagestory.exe in '$GamePath'."
}

Write-Host "Checking for running Vintage Story process..." -ForegroundColor Cyan
$vsProcess = Get-Process -Name "Vintagestory" -ErrorAction SilentlyContinue
if ($vsProcess) {
    if ($NoCloseVS) {
        Write-Host "Vintage Story is running. Leaving it open because -NoCloseVS was specified."
    }
    else {
        Write-Host "Vintage Story is running. Stopping process..."
        Stop-Process -Name "Vintagestory" -Force
        Start-Sleep -Seconds 2
    }
}

Write-Host "Building Joy of Sailing against Vintage Story $GameVersion..." -ForegroundColor Cyan
dotnet build $ProjectFile "-p:GameVersion=$GameVersion" "-p:GamePath=$GamePath" "-c" $Configuration
if ($LASTEXITCODE -ne 0) {
    throw "Build failed."
}

$sourceDir = Join-Path $ProjectRoot "bin\$Configuration\$GameVersion\ModPackage\JoyOfSailing"
if (-not (Test-Path -LiteralPath $sourceDir)) {
    throw "Packaged mod directory not found at '$sourceDir'."
}

$modInfoPath = Join-Path $sourceDir "modinfo.json"
$modInfo = Get-Content -LiteralPath $modInfoPath -Raw | ConvertFrom-Json
$version = [string]$modInfo.version
$zipFileName = "joyofsailing_$version.zip"
$zipFilePath = Join-Path $ModsDir $zipFileName

if (-not (Test-Path -LiteralPath $ModsDir)) {
    New-Item -ItemType Directory -Path $ModsDir | Out-Null
}

Write-Host "Deploying mod as '$zipFileName' to '$ModsDir'..." -ForegroundColor Cyan
Get-ChildItem -Path $ModsDir -Filter "joyofsailing_*.zip" -File -ErrorAction SilentlyContinue | Remove-Item -Force
if (Test-Path -LiteralPath $zipFilePath) {
    Remove-Item -LiteralPath $zipFilePath -Force
}
Compress-Archive -Path (Join-Path $sourceDir "*") -DestinationPath $zipFilePath -Force

if (-not $NoLaunch -and -not ($vsProcess -and $NoCloseVS)) {
    Write-Host "Launching Vintage Story $GameVersion..." -ForegroundColor Green
    Start-Process -FilePath $VSExePath -WindowStyle Hidden
}
else {
    Write-Host "Deployment complete. Launch skipped." -ForegroundColor Green
}
