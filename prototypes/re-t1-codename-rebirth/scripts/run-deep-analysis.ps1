param(
    [string]$IlspyExe = 'F:/workspace/rogue-prototype-lab/prototypes/re-crunch-time/local/tools/ilspy/ilspycmd.exe',
    [string]$JavaExe = 'C:/Program Files/Java/jdk-17/bin/java.exe',
    [switch]$SkipMedia
)
$ErrorActionPreference='Stop'
$env:PYTHONUTF8='1'
$taskRoot=Split-Path $PSScriptRoot -Parent
function Invoke-AnalysisPython([string]$name) {
    & python (Join-Path $PSScriptRoot $name)
    if ($LASTEXITCODE -ne 0) { throw "Failed analysis step: $name" }
}
if (-not (Test-Path -LiteralPath $IlspyExe)) { throw 'ILSpy CLI path missing; pass -IlspyExe. No dependency installation is implicit.' }
Invoke-AnalysisPython 'decode_qxgm.py'
& dotnet --roll-forward Major "$taskRoot/local/tools/il2cppdumper/Il2CppDumper.dll" "$taskRoot/local/apk/lib/arm64-v8a/libil2cpp.so" "$taskRoot/local/decoded/global-metadata.dat" "$taskRoot/local/il2cpp-arm64"
if ($LASTEXITCODE -ne 0) { throw 'IL2CPP declaration dump failed' }
Invoke-AnalysisPython 'decode_bundles.py'
Invoke-AnalysisPython 'inspect_bundles.py'
Invoke-AnalysisPython 'decode_hotfix.py'
& dotnet build "$PSScriptRoot/metadata-index/MetadataIndex.csproj" -c Release -o "$taskRoot/local/tools/metadata" --nologo
if ($LASTEXITCODE -ne 0) { throw 'Metadata index tool build failed' }
foreach ($taskProfile in @('debug','release')) {
    $taskRefs="$taskRoot/local/source-references/$taskProfile"
    $taskAotRefs="$taskRoot/local/managed-references/$taskProfile"
    foreach ($taskDir in @($taskRefs,$taskAotRefs)) {
        New-Item -ItemType Directory -Force -Path $taskDir | Out-Null
        Get-ChildItem -LiteralPath "$taskRoot/local/il2cpp-arm64/DummyDll" -Filter '*.dll' | Copy-Item -Destination $taskDir
    }
    Get-ChildItem -LiteralPath "$taskRoot/local/managed/$taskProfile/hotfix" -Filter '*.dll' | Copy-Item -Destination $taskRefs
    Get-ChildItem -LiteralPath "$taskRoot/local/managed/$taskProfile" -Filter '*.dll' -Recurse | Copy-Item -Destination $taskAotRefs
    foreach ($taskGroup in @('hotfix','aotmeta')) {
        foreach ($taskDll in Get-ChildItem -LiteralPath "$taskRoot/local/managed/$taskProfile/$taskGroup" -Filter '*.dll') {
            $taskMetadata="$taskRoot/local/managed-metadata/$taskProfile/$taskGroup/$($taskDll.BaseName).json"
            New-Item -ItemType Directory -Force -Path (Split-Path $taskMetadata) | Out-Null
            & dotnet "$taskRoot/local/tools/metadata/MetadataIndex.dll" $taskDll.FullName $taskMetadata
            if ($LASTEXITCODE -ne 0) { throw 'Managed IL indexing failed' }
            $taskReference=if ($taskGroup -eq 'hotfix') { $taskRefs } else { $taskAotRefs }
            & $IlspyExe --disable-updatecheck -p --nested-directories -r $taskReference -o "$taskRoot/local/source/all/$taskProfile/$taskGroup/$($taskDll.BaseName)" $taskDll.FullName
            if ($LASTEXITCODE -ne 0) { throw 'Managed decompilation failed' }
        }
    }
}
Invoke-AnalysisPython 'repair_base_trees.py'
Invoke-AnalysisPython 'decode_config.py'
Invoke-AnalysisPython 'inspect_payloads.py'
if (-not $SkipMedia) {
    Invoke-AnalysisPython 'export_media.py'
    Invoke-AnalysisPython 'repair_texture_exports.py'
    Invoke-AnalysisPython 'export_fmod_samples.py'
}
Invoke-AnalysisPython 'build_deep_catalog.py'
Invoke-AnalysisPython 'link_content.py'
& "$PSScriptRoot/run-android-analysis.ps1" -JavaExe $JavaExe
Invoke-AnalysisPython 'verify_deep.py'
Write-Output 'Deep static pipeline complete. Browser UI QA is a separate recorded pass; no APK execution implied.'
