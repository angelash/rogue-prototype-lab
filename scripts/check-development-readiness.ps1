param(
    [string]$UnityEditorPath = 'D:\Program files\2022.3.62f3c1\Editor\Unity.exe',
    [string]$BlenderPath = 'D:\SteamLibrary\steamapps\common\Blender\blender.exe',
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'
$ringRepoRoot = Split-Path -Parent $PSScriptRoot
$ringUnityExists = Test-Path -LiteralPath $UnityEditorPath -PathType Leaf
$ringUnityEditorDirectory = Split-Path -Parent $UnityEditorPath
$ringPlayerRoot = Join-Path $ringUnityEditorDirectory 'Data\PlaybackEngines\windowsstandalonesupport\Variations'
$ringProjectPath = Join-Path $ringRepoRoot 'prototypes\proto-013-ring-toss\game\RingTossWorkshop'
$ringTools = foreach ($ringToolName in @('git', 'rg', 'pwsh', 'python', 'node', 'dotnet', 'ffmpeg', 'ffprobe')) {
    $ringCommand = Get-Command $ringToolName -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    $ringCommandPath = if ($null -ne $ringCommand) { $ringCommand.Source } else { $null }
    if ($ringToolName -eq 'dotnet' -and $null -eq $ringCommandPath) {
        $ringDotnetFallback = 'C:\Program Files\dotnet\dotnet.exe'
        if (Test-Path -LiteralPath $ringDotnetFallback -PathType Leaf) { $ringCommandPath = $ringDotnetFallback }
    }
    [pscustomobject]@{ Name = $ringToolName; Located = ($null -ne $ringCommandPath); Path = $ringCommandPath }
}
$ringOs = Get-CimInstance Win32_OperatingSystem
$ringCpu = Get-CimInstance Win32_Processor
$ringMemory = Get-CimInstance Win32_ComputerSystem
$ringReport = [ordered]@{
    SchemaVersion = 1
    CheckedAt = [DateTimeOffset]::Now.ToString('o')
    Workspace = $ringRepoRoot
    CheckKind = 'file-and-command-inventory'
    Unity = [ordered]@{
        EditorPath = $UnityEditorPath
        EditorExists = $ringUnityExists
        ProductVersion = $(if ($ringUnityExists) { (Get-Item -LiteralPath $UnityEditorPath).VersionInfo.ProductVersion } else { $null })
        Windows64DevelopmentMono = (Test-Path -LiteralPath (Join-Path $ringPlayerRoot 'win64_player_development_mono\UnityPlayer.dll') -PathType Leaf)
        Windows64ReleaseMono = (Test-Path -LiteralPath (Join-Path $ringPlayerRoot 'win64_player_nondevelopment_mono\UnityPlayer.dll') -PathType Leaf)
        EditorLaunch = 'not-tested-by-this-script'
        License = 'not-tested-by-this-script'
        PackageResolution = 'not-tested-by-this-script'
        PlayerBuild = 'not-tested-by-this-script'
    }
    Blender = [ordered]@{ ExecutablePath = $BlenderPath; ExecutableExists = (Test-Path -LiteralPath $BlenderPath -PathType Leaf); ModelingExportAndRender = 'not-tested-by-this-script' }
    Tools = @($ringTools)
    GameProject = [ordered]@{
        PlannedPath = $ringProjectPath
        AssetsExists = (Test-Path -LiteralPath (Join-Path $ringProjectPath 'Assets') -PathType Container)
        ManifestExists = (Test-Path -LiteralPath (Join-Path $ringProjectPath 'Packages\manifest.json') -PathType Leaf)
        ProjectVersionExists = (Test-Path -LiteralPath (Join-Path $ringProjectPath 'ProjectSettings\ProjectVersion.txt') -PathType Leaf)
    }
    Host = [ordered]@{ OS = $ringOs.Caption; OSVersion = $ringOs.Version; CPU = @($ringCpu | ForEach-Object { $_.Name }); RAMGiB = [math]::Round($ringMemory.TotalPhysicalMemory / 1GB, 2) }
    DesktopInputAndCapture = 'must-be-checked-through-actual-available-tool'
    SteamAppAndAccount = 'not-inspected'
}
$ringJson = $ringReport | ConvertTo-Json -Depth 8
if ($OutputPath) {
    $ringOutputCandidate = if ([IO.Path]::IsPathRooted($OutputPath)) { $OutputPath } else { Join-Path $ringRepoRoot $OutputPath }
    $ringAbsoluteOutput = [IO.Path]::GetFullPath($ringOutputCandidate)
    $ringOutputDirectory = Split-Path -Parent $ringAbsoluteOutput
    if (-not (Test-Path -LiteralPath $ringOutputDirectory -PathType Container)) {
        New-Item -ItemType Directory -Path $ringOutputDirectory -Force | Out-Null
    }
    [IO.File]::WriteAllText($ringAbsoluteOutput, $ringJson, (New-Object Text.UTF8Encoding($false)))
}
$ringJson
