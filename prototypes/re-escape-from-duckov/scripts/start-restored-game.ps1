param(
    [string]$PlayerDirectory,
    [string]$LogFile
)
$ErrorActionPreference = 'Stop'
if (-not $PlayerDirectory) {
    $PlayerDirectory = Join-Path (Split-Path -Parent $PSScriptRoot) 'local/Player'
}
$duckovDirectory = (Resolve-Path -LiteralPath $PlayerDirectory).Path
$duckovExecutable = Join-Path $duckovDirectory 'DuckovRestored.exe'
$duckovManifest = Get-Content -LiteralPath (Join-Path $duckovDirectory 'native-player-inputs.json') -Raw | ConvertFrom-Json
if ($duckovManifest.probeSceneOnly -or $duckovManifest.unity -ne '2022.3.62f3c1') {
    throw 'The directory must contain the locally built full-scene restoration candidate.'
}
$duckovEngineHash = (Get-FileHash -LiteralPath (Join-Path $duckovDirectory 'UnityPlayer.dll') -Algorithm SHA256).Hash.ToLowerInvariant()
if ($duckovEngineHash -ne $duckovManifest.hostUnityPlayerSHA256) {
    throw 'UnityPlayer differs from the registered local host build.'
}
if (-not $LogFile) {
    $LogFile = Join-Path (Split-Path -Parent $PSScriptRoot) ('local/game-session-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.log')
}
if (Test-Path -LiteralPath $LogFile) { throw 'Choose a new log filename; prior evidence is retained.' }
if ($LogFile.Contains('"')) { throw 'A log path cannot contain a quote.' }
$duckovArguments = @('-logFile', ('"' + $LogFile + '"'))
Start-Process -FilePath $duckovExecutable -WorkingDirectory $duckovDirectory -ArgumentList $duckovArguments -WindowStyle Hidden -PassThru
