# ActorSkinSelector — Build & Deploy Script
# Usage:  .\build_plugin.ps1
# Builds ActorSkinSelector.dll and auto-copies it to BepInEx/plugins/actorskinselector/.

$ErrorActionPreference = 'Stop'

$projDir  = $PSScriptRoot
$projFile = Join-Path $projDir 'ActorSkinSelector.csproj'

Write-Host "`n=== Building ActorSkinSelector ===" -ForegroundColor Cyan
dotnet build $projFile -c Release --nologo -v minimal

if ($LASTEXITCODE -eq 0) {
    Write-Host "`n=== Build succeeded — ActorSkinSelector.dll deployed to BepInEx/plugins/actorskinselector/ ===" -ForegroundColor Green
} else {
    Write-Host "`n=== Build FAILED ===" -ForegroundColor Red
    exit 1
}
