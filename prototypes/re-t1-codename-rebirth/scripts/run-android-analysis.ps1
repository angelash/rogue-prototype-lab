param([string]$JavaExe='C:/Program Files/Java/jdk-17/bin/java.exe', [string]$DexdumpExe='D:/androidSdk/build-tools/34.0.0/dexdump.exe')
$ErrorActionPreference='Stop'
$env:PYTHONUTF8='1'
$taskRoot=Split-Path $PSScriptRoot -Parent
New-Item -ItemType Directory -Force -Path "$taskRoot/local/logs","$taskRoot/local/dex-disassembly" | Out-Null
$taskDexFiles=@(Get-ChildItem -LiteralPath "$taskRoot/local/apk" -Filter '*.dex' -File)
if ($taskDexFiles.Count -ne 28) { throw 'Expected the registered 28 DEX files' }
& $JavaExe -Xmx2g -cp "$taskRoot/local/tools/jadx/lib/*" jadx.cli.JadxCLI --no-res -j 4 --respect-bytecode-access-modifiers --use-source-name-as-class-name-alias never --log-level warn -d "$taskRoot/local/android-java" @($taskDexFiles.FullName) *> "$taskRoot/local/logs/jadx.log"
$taskJadxExit=$LASTEXITCODE
foreach ($taskDex in $taskDexFiles) {
    & $JavaExe -Xmx1g -cp "$taskRoot/local/tools/jadx/lib/*" jadx.cli.JadxCLI --no-res -j 4 --decompilation-mode fallback --no-inline-anonymous --no-move-inner-classes --no-debug-info --use-source-name-as-class-name-alias never --log-level error -d "$taskRoot/local/android-fallback/$($taskDex.BaseName)" $taskDex.FullName *> "$taskRoot/local/logs/jadx-fallback-$($taskDex.BaseName).log"
    & $DexdumpExe -d -f $taskDex.FullName *> "$taskRoot/local/dex-disassembly/$($taskDex.BaseName).txt"
    if ($LASTEXITCODE -ne 0) { throw "SDK instruction dump failed: $($taskDex.Name)" }
}
& python "$PSScriptRoot/collect_android_evidence.py"
if ($LASTEXITCODE -ne 0) { throw 'Android evidence accounting failed' }
Write-Output "JADX normal exit=$taskJadxExit; diagnostics and empty-input cases recorded separately. All DEX instruction counts checked."
