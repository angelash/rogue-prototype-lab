param([string]$SourcePath='D:/download/t1_compat_debug-0.9.0-x-202610131448_1782199009743830-1-4399--yf_test.apk',
      [string]$Python='C:/Python310/python.exe')
$ErrorActionPreference='Stop'
$t1Project=[System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
& $Python (Join-Path $PSScriptRoot 'intake.py') --source $SourcePath
if ($LASTEXITCODE -ne 0) { throw 'APK intake failed' }
foreach ($t1Script in @('analyze_manifest.py','analyze_structure.py','analyze_readable_assets.py','build_catalog.py','verify_analysis.py')) {
    & $Python (Join-Path $PSScriptRoot $t1Script)
    if ($LASTEXITCODE -ne 0) { throw "Analysis failed: $t1Script" }
}
