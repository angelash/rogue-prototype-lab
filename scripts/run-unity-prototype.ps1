param(
    [ValidateSet('Prepare', 'Test', 'Build')]
    [string]$Mode = 'Test',
    [string]$UnityEditorPath = 'D:\Program files\2022.3.62f3c1\Editor\Unity.exe'
)
$ErrorActionPreference = 'Stop'
$ringRepoRoot = Split-Path -Parent $PSScriptRoot
$ringProject = Join-Path $ringRepoRoot 'prototypes\proto-013-ring-toss\game\RingTossWorkshop'
if (-not (Test-Path -LiteralPath $UnityEditorPath -PathType Leaf)) { throw 'Unity editor is missing' }
if ((Get-Item -LiteralPath $UnityEditorPath).VersionInfo.ProductVersion -notlike '2022.3.62f3c1*') {
    throw 'Use the recorded Unity 2022.3.62f3c1 editor'
}
$ringRunRoot = Join-Path $ringRepoRoot ('.local\unity\' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff') + '-' + $Mode)
New-Item -ItemType Directory -Path $ringRunRoot -Force | Out-Null
$ringLogPath = Join-Path $ringRunRoot 'unity.log'
$ringResultsPath = Join-Path $ringRunRoot 'tests.xml'
$ringArguments = @('-batchmode', '-nographics', '-projectPath', ('"' + $ringProject + '"'), '-logFile', ('"' + $ringLogPath + '"'))
if ($Mode -eq 'Test') {
    $ringArguments += @('-runTests', '-testPlatform', 'EditMode', '-testResults', ('"' + $ringResultsPath + '"'))
} else {
    $ringMethod = if ($Mode -eq 'Prepare') { 'RingToss.Editor.ProjectBuilder.Prepare' } else { 'RingToss.Editor.ProjectBuilder.BuildWindows' }
    $ringArguments += @('-quit', '-executeMethod', $ringMethod)
}
Write-Output "UNITY_START mode=$Mode log=$ringLogPath"
$ringProcess = Start-Process -FilePath $UnityEditorPath -ArgumentList $ringArguments -WindowStyle Hidden -PassThru
$ringProcess.WaitForExit()
if ($ringProcess.ExitCode -ne 0) { throw "Unity failed with exit $($ringProcess.ExitCode). Inspect $ringLogPath" }
if ($Mode -eq 'Test') {
    if (-not (Test-Path -LiteralPath $ringResultsPath -PathType Leaf)) { throw 'No test report was generated' }
    [xml]$ringResults = Get-Content -LiteralPath $ringResultsPath -Raw
    $ringTestRun = $ringResults.'test-run'
    if ([int]$ringTestRun.total -lt 1 -or [int]$ringTestRun.failed -gt 0 -or $ringTestRun.result -ne 'Passed') {
        throw "Tests did not pass: $ringResultsPath"
    }
    Write-Output "UNITY_TEST_PASS total=$($ringTestRun.total) passed=$($ringTestRun.passed) report=$ringResultsPath"
}
Write-Output "UNITY_EXIT=0 mode=$Mode"
