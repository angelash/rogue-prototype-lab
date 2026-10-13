param(
    [switch]$Editor,
    [switch]$SelfTest,
    [switch]$Suite,
    [switch]$Resume,
    [switch]$Combat,
    [switch]$Pixel,
    [string]$Unity = 'D:\Program files\2022.3.62f3c1\Editor\Unity.exe'
)
$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $PSScriptRoot
$local = Join-Path $project 'local'
if ($Editor) {
    $source = Join-Path $local 'HybridUnityProject'
    Start-Process -FilePath $Unity -ArgumentList ('-projectPath "'+$source+'"') -WindowStyle Normal
    return
}
$directory = Join-Path $local 'HybridPlayer'
$manifest = Get-Content -LiteralPath (Join-Path $directory 'hybrid-player-inputs.json') -Raw | ConvertFrom-Json
if ((Get-FileHash -LiteralPath (Join-Path $directory 'UnityPlayer.dll') -Algorithm SHA256).Hash.ToLowerInvariant() -ne $manifest.engineSHA256) { throw 'Native engine differs from the verified input' }
foreach ($assembly in $manifest.assemblies) {
    $expected = if ($assembly.unityCompiledBinarySHA256) { $assembly.unityCompiledBinarySHA256 } else { $assembly.binarySHA256 }
    $path = Join-Path $directory ('DuckovRestored_Data\Managed\' + $assembly.assembly + '.dll')
    if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expected) { throw ('Business code changed after build: ' + $assembly.assembly) }
}
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$log = Join-Path $local ('hybrid-game-' + $stamp + '.log')
$arguments = '-logFile "'+$log+'"'
if ($SelfTest -or $Suite -or $Resume -or $Combat -or $Pixel) {
    $output = Join-Path $local ('hybrid-qa-' + $stamp)
    $arguments += ' --duckov-hybrid-qa "'+$output+'"'
    if ($Suite) { $arguments += ' --duckov-hybrid-suite' }
    if ($Resume) { $arguments += ' --duckov-hybrid-resume' }
    if ($Combat) { $arguments += ' --duckov-hybrid-combat' }
    if ($Pixel) { $arguments += ' --duckov-hybrid-pixel' }
    if ($Suite) {
        # Repeat from an empty, dedicated test slot while preserving its prior fixture.
        $saves = Join-Path $env:USERPROFILE 'AppData\LocalLow\RoguePrototypeLab\DuckovHybridRestorationLab\Saves'
        if (Test-Path -LiteralPath $saves) {
            $fixture = Join-Path $output 'previous-slot-6'
            New-Item -ItemType Directory -Path $fixture -Force | Out-Null
            $root = (Resolve-Path -LiteralPath $saves).Path + '\'
            foreach ($file in Get-ChildItem -LiteralPath $saves -Filter 'Save_6.sav*' -File) {
                if (!$file.FullName.StartsWith($root)) { throw 'QA fixture is outside the dedicated save directory' }
                Move-Item -LiteralPath $file.FullName -Destination (Join-Path $fixture $file.Name)
            }
        }
    }
    New-Item -ItemType Directory -Path $output -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $directory 'hybrid-player-inputs.json') -Destination (Join-Path $output 'tested-build.json')
    Write-Output ('Self-test evidence: ' + $output)
}
Start-Process -FilePath (Join-Path $directory 'DuckovRestored.exe') -WorkingDirectory $directory -ArgumentList $arguments -WindowStyle Normal
Write-Output ('Runtime log: ' + $log)
