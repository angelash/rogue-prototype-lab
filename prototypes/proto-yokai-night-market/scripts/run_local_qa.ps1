# Isolated command-line QA only. This script performs no native window input.
param([string]$EvidenceSuffix = '', [switch]$VisualAndMatchOnly)
$ErrorActionPreference = 'Stop'
$taskRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$taskLocal = Join-Path $taskRoot 'local'
$qaRoot = Join-Path $taskLocal 'Builds/QA-Visual'
$qaPlayer = Join-Path $qaRoot 'NightMarketQA.exe'
$qaBuild = Get-Content -LiteralPath (Join-Path $taskLocal 'qa-visual-build-report.json') -Raw | ConvertFrom-Json
if ($qaBuild.result -ne 'Succeeded' -or $qaBuild.errors -ne 0) { throw 'QA build has not passed' }

function Run-Check($flag, $aspect, $width, $reportFolder, $reportName, $archiveName, $captureNames) {
    $archiveName += $EvidenceSuffix
    $archive = Join-Path $taskLocal ('QAResults/' + $archiveName)
    if (Test-Path -LiteralPath $archive) { throw ('Preserve existing evidence: ' + $archive) }
    $log = Join-Path $taskLocal ($archiveName + '-final.log')
    $started = Get-Date
    $process = Start-Process -FilePath $qaPlayer -ArgumentList @('-batchmode', $flag, '-screen-width', "$width", '-screen-height', '1080', '-screen-fullscreen', '1', '-logFile', ('"' + $log + '"')) -WindowStyle Hidden -PassThru -Wait
    if ($process.ExitCode -ne 0) { throw ($archiveName + ' exited with ' + $process.ExitCode) }
    $reportPath = Join-Path $qaRoot ($reportFolder + '/' + $reportName)
    if ((Get-Item -LiteralPath $reportPath).LastWriteTime -lt $started) { throw 'Stale QA report' }
    $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
    if (!$report.passed -or $report.errors.Count -ne 0 -or $report.width -ne $width -or $report.height -ne 1080) { throw ('QA failed: ' + $archiveName) }
    if ($flag -eq '--night-visual' -and ($report.captures.Count -ne 128 -or $report.tutorialPages -ne 2)) { throw 'Incomplete visual survey' }
    if ($flag -eq '--night-rules' -and $report.rules.Count -ne 40) { throw 'Incomplete rule suite' }
    New-Item -ItemType Directory -Path $archive | Out-Null
    Copy-Item -LiteralPath $reportPath -Destination (Join-Path $archive $reportName)
    if ($flag -eq '--night-visual') { $captureNames = $report.captures }
    foreach ($name in $captureNames) {
        $imagePath = Join-Path $qaRoot ($reportFolder + '/' + $name)
        if ((Get-Item -LiteralPath $imagePath).LastWriteTime -lt $started) { throw ('Stale QA capture: ' + $name) }
        Copy-Item -LiteralPath $imagePath -Destination (Join-Path $archive $name)
    }
    [ordered]@{case=$archiveName;passed=$report.passed;width=$report.width;height=$report.height;state=$report.state;frames=$report.captures.Count;rules=$report.rules.Count;winner=$report.winner;rounds=$report.rounds} | ConvertTo-Json -Compress
}

Run-Check '--night-visual' '16x9' 1920 'visual-evidence' 'report.json' 'visual-complete-16x9' @()
Run-Check '--night-visual' '4x3' 1440 'visual-evidence' 'report.json' 'visual-complete-4x3' @()
if (!$VisualAndMatchOnly) {
    Run-Check '--night-rules' '16x9' 1920 'smoke-evidence' 'rules.json' 'rules' @()
    Run-Check '--night-smoke' '16x9' 1920 'smoke-evidence' 'report.json' 'smoke-16x9' @('opening.png','last.png')
    Run-Check '--night-smoke' '4x3' 1440 'smoke-evidence' 'report.json' 'smoke-4x3' @('opening.png','last.png')
}
Run-Check '--night-match' '16x9' 1920 'match-evidence' 'report.json' 'match' @('ending.png')
