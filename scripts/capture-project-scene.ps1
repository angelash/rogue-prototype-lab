param([Parameter(Mandatory=$true)][string]$ProjectPath)
$ErrorActionPreference='Stop'
$labRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$sceneProject=[IO.Path]::GetFullPath((Join-Path $labRoot $ProjectPath))
$prototypeRoot=Join-Path $labRoot 'prototypes'
if(-not $sceneProject.StartsWith($prototypeRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Project must be inside prototypes'}
$product=Split-Path $sceneProject -Leaf
if($product -notmatch '^[A-Za-z][A-Za-z0-9]+$'){throw 'Invalid product name'}
$sceneExe=Join-Path $sceneProject "Builds/Windows64/$product.exe"
if(-not (Test-Path -LiteralPath $sceneExe -PathType Leaf)){throw 'Build the project first'}
$captureDirectory=Join-Path $labRoot ('.local/captures/'+$product+'-'+(Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Path $captureDirectory -Force | Out-Null
$captureLog=Join-Path $captureDirectory 'camera.log'
$sceneProcess=Start-Process -FilePath $sceneExe -ArgumentList @('-captureOnce','-captureDirectory',('"'+$captureDirectory+'"'),'-logFile',('"'+$captureLog+'"')) -WindowStyle Hidden -PassThru
if(-not $sceneProcess.WaitForExit(30000)){Stop-Process -Id $sceneProcess.Id;throw "Own Player capture timed out: $captureLog"}
$sceneProcess.Refresh()
$captureImage=Join-Path $captureDirectory ($product+'-scene.png')
$logText=[IO.File]::ReadAllText($captureLog)
if($sceneProcess.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $captureImage) -or $logText -match '(?m)^\w*Exception:' -or $logText -notmatch 'PLAYER_READY' -or $logText -notmatch 'SCENE_RENDER'){throw "Player capture failed; inspect own log: $captureLog"}
[pscustomobject]@{project=$ProjectPath;capture=$captureImage;log=$captureLog;cameraOnly=$true;nativeInputQA=$false;requiresVisualReview=$true} | ConvertTo-Json -Compress
