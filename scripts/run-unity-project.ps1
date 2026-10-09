param(
    [Parameter(Mandatory=$true)][string]$ProjectPath,
    [ValidateSet('Prepare','Test','Build')][string]$Mode = 'Test',
    [string]$UnityEditorPath = 'D:\Program files\2022.3.62f3c1\Editor\Unity.exe'
)
$ErrorActionPreference = 'Stop'
$batchRepoRoot = Split-Path -Parent $PSScriptRoot
$batchProject = (Resolve-Path -LiteralPath (Join-Path $batchRepoRoot $ProjectPath)).Path
$batchAllowedRoot = [IO.Path]::GetFullPath((Join-Path $batchRepoRoot 'prototypes')) + [IO.Path]::DirectorySeparatorChar
if (-not $batchProject.StartsWith($batchAllowedRoot,[StringComparison]::OrdinalIgnoreCase)) { throw 'Project must stay inside prototypes' }
if (-not (Test-Path -LiteralPath $UnityEditorPath -PathType Leaf)) { throw 'Unity editor missing' }
if ((Get-Item -LiteralPath $UnityEditorPath).VersionInfo.ProductVersion -notlike '2022.3.62f3c1*') { throw 'Use Unity 2022.3.62f3c1' }
$batchRunRoot = Join-Path $batchRepoRoot ('.local\unity\' + (Split-Path $batchProject -Leaf) + '-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff') + '-' + $Mode)
New-Item -ItemType Directory -Path $batchRunRoot -Force | Out-Null
$batchLog = Join-Path $batchRunRoot 'unity.log'
$batchResults = Join-Path $batchRunRoot 'tests.xml'
$batchArguments = @('-batchmode','-nographics','-projectPath',('"'+$batchProject+'"'),'-logFile',('"'+$batchLog+'"'))
if ($Mode -eq 'Test') { $batchArguments += @('-runTests','-testPlatform','EditMode','-testResults',('"'+$batchResults+'"')) }
else {
    $batchMethod = if ($Mode -eq 'Prepare') { 'PrototypeBuild.Editor.ProjectBuilder.Prepare' } else { 'PrototypeBuild.Editor.ProjectBuilder.BuildWindows' }
    $batchArguments += @('-quit','-executeMethod',$batchMethod)
}
Write-Output "UNITY_START project=$batchProject mode=$Mode log=$batchLog"
$batchProcess = Start-Process -FilePath $UnityEditorPath -ArgumentList $batchArguments -WindowStyle Hidden -PassThru
$batchProcess.WaitForExit()
if ($batchProcess.ExitCode -ne 0) { throw "Unity failed exit=$($batchProcess.ExitCode) log=$batchLog" }
if ($Mode -eq 'Test') {
    [xml]$batchXml = Get-Content -LiteralPath $batchResults -Raw
    $batchRun = $batchXml.'test-run'
    if ([int]$batchRun.total -lt 1 -or [int]$batchRun.failed -gt 0 -or $batchRun.result -ne 'Passed') { throw 'Unity tests failed' }
    Write-Output "UNITY_TEST_PASS total=$($batchRun.total) passed=$($batchRun.passed) report=$batchResults"
}
Write-Output "UNITY_EXIT=0 mode=$Mode"
