param(
    [string]$Unity = 'D:\Program files\2022.3.62f3c1\Editor\Unity.exe',
    [string]$Python = 'C:\Python310\python.exe',
    [switch]$Launch
)
$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $PSScriptRoot
$local = Join-Path $project 'local'
$source = Join-Path $local 'HybridUnityProject'
if (!(Test-Path -LiteralPath (Join-Path $source 'Assets\Reconstructed'))) { throw 'Hybrid source project is missing' }
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$log = Join-Path $local ('editor-hybrid-build-' + $stamp + '.log')
$env:UNITY_BURST_DISABLE_COMPILATION = '1'
$process = Start-Process -FilePath $Unity -ArgumentList ('-batchmode -nographics -quit -projectPath "' + $source + '" -executeMethod DuckovHybridEditor.BuildHost -logFile "' + $log + '"') -WindowStyle Hidden -PassThru
while (!$process.WaitForExit(45000)) { Write-Output 'Unity source build is still running.' }
if ($process.ExitCode -ne 0) { throw ('Unity build failed; see ' + $log) }
$compiled = Join-Path $local 'HybridUnityHost\DuckovNativeProbe_Data\Managed'
& (Join-Path $PSScriptRoot 'audit_hybrid_abi.ps1') -Project $project -BuildRoot $compiled
if ($LASTEXITCODE -ne 0) { throw 'Original native asset ABI audit failed' }
& $Python (Join-Path $PSScriptRoot 'compile_hybrid_code.py') --project $project --assembly DuckovRecovery.Runtime --source (Join-Path $source 'Assets\Runtime')
if ($LASTEXITCODE -ne 0) { throw 'Matched-runtime recovery observer compilation failed' }
$candidate = Join-Path $local ('HybridPlayer-build-' + $stamp)
& $Python (Join-Path $PSScriptRoot 'assemble_hybrid_player.py') --project $project --output $candidate --compiled-root $compiled
if ($LASTEXITCODE -ne 0) { throw 'Hybrid native Resources assembly failed' }
# Preserve the exact runnable source build in the offline versioned inputs.
# Active Player directories and Unity build caches are deliberately ignored.
$snapshot = Join-Path $local ('hybrid-source-player-build-' + $stamp)
Copy-Item -LiteralPath $candidate -Destination $snapshot -Recurse
Copy-Item -LiteralPath (Join-Path $local 'HybridUnityHost\build-report.json') -Destination (Join-Path $snapshot 'unity-source-build-report.json')
Copy-Item -LiteralPath (Join-Path $local 'hybrid-abi-audit.json') -Destination (Join-Path $snapshot 'hybrid-abi-audit.json')
$active = Join-Path $local 'HybridPlayer'
if (Test-Path -LiteralPath $active) {
    $resolved = (Resolve-Path -LiteralPath $active).Path
    if (!$resolved.StartsWith((Resolve-Path -LiteralPath $local).Path + '\')) { throw 'Candidate is outside project local directory' }
    Move-Item -LiteralPath $active -Destination (Join-Path $local ('HybridPlayer-previous-' + $stamp))
}
Move-Item -LiteralPath $candidate -Destination $active
Write-Output ('Hybrid game ready: ' + $active)
if ($Launch) { & (Join-Path $PSScriptRoot 'start-hybrid-project.ps1') }
