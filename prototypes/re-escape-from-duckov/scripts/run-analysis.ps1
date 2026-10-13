param(
    [string]$Source = 'E:\SteamLibrary\steamapps\common\Escape from Duckov',
    [string]$Python = 'python',
    [string]$Ilspy = '',
    [string]$Generator = '',
    [switch]$SkipCode
)
$ErrorActionPreference = 'Stop'
$projectPath = Split-Path -Parent $PSScriptRoot
if (-not $Ilspy) { $Ilspy = Join-Path $projectPath 'local/tools/ilspy/ilspycmd.exe' }
if (-not $Generator) { $Generator = Join-Path $projectPath 'local/tools/python' }
if (-not (Test-Path -LiteralPath $Ilspy)) { throw 'Supply an existing official ILSpy executable with -Ilspy; this script does not install software.' }
if (-not (Test-Path -LiteralPath $Generator)) { throw 'Supply the existing TypeTreeGeneratorAPI package directory with -Generator.' }
function Invoke-Stage([string]$Name, [string[]]$Arguments) {
    & $Python (Join-Path $PSScriptRoot $Name) --project $projectPath @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Analysis failed: $Name" }
}
Invoke-Stage 'prepare.py' @('--source', $Source)
$buildInfo = Get-Content -Raw -LiteralPath (Join-Path $projectPath 'evidence/build-summary.json') | ConvertFrom-Json
if ($buildInfo.gameVersion -ne '2.3.30') { throw 'These layout corrections and coverage expectations are pinned to game 2.3.30. Use a new project/input snapshot for a different build.' }
if (-not $SkipCode) {
    $metadataOutput = Join-Path $projectPath 'local/tools/metadata'
    $metadataObj = (Join-Path $projectPath 'local/tools/metadata-obj') + [IO.Path]::DirectorySeparatorChar
    & dotnet build (Join-Path $PSScriptRoot 'metadata-index/MetadataIndex.csproj') -c Release -o $metadataOutput "-p:BaseIntermediateOutputPath=$metadataObj"
    if ($LASTEXITCODE -ne 0) { throw 'Metadata reader build failed.' }
    Invoke-Stage 'analyze_code.py' @('--ilspy', $Ilspy, '--metadata', (Join-Path $metadataOutput 'MetadataIndex.dll'))
}
Invoke-Stage 'analyze_assets.py' @('--generator', $Generator)
Invoke-Stage 'repair_mono.py' @('--generator', $Generator)
Invoke-Stage 'export_media.py' @()
Invoke-Stage 'analyze_audio.py' @()
Invoke-Stage 'analyze_content.py' @()
Invoke-Stage 'native_index.py' @()
Invoke-Stage 'verify.py' @()
Write-Host 'Static analysis complete; inspect separate C#, tree and shader limitation records.'
