param(
    [string]$Editor = 'D:\Program files\2022.3.62f3c1\Editor\Unity.exe'
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$candidate = Join-Path $projectRoot 'local\RestoredUnityProject'
if (!(Test-Path -LiteralPath (Join-Path $candidate 'Assets\Scenes\Startup\Startup.unity'))) {
    throw "场景恢复工程尚未准备：$candidate"
}
if (!(Test-Path -LiteralPath $Editor)) { throw "未找到已核实的本地 Unity：$Editor" }
$version = Get-Content -LiteralPath (Join-Path $candidate 'ProjectSettings\ProjectVersion.txt') -Raw
if ($version -notmatch '2022\.3\.62f3c1') { throw '工程版本与本机制作基线不符' }
$log = Join-Path $projectRoot ("local\editor-session-{0}.log" -f (Get-Date -Format 'yyyyMMdd-HHmmss'))
Start-Process -FilePath $Editor -ArgumentList @('-projectPath', ('"{0}"' -f $candidate), '-logFile', ('"{0}"' -f $log)) -WindowStyle Hidden
Write-Output "编辑恢复候选：$candidate"
Write-Output "日志：$log"
