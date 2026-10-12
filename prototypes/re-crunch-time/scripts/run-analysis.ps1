param(
    [string]$SourcePath = 'E:/SteamLibrary/steamapps/common/Crunch Time',
    [string]$Python = 'C:/Python310/python.exe'
)
$ErrorActionPreference = 'Stop'
$ctProject = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$ctSource = (Resolve-Path -LiteralPath $SourcePath).Path
$ctLocal = Join-Path $ctProject 'local'
$ctCopy = Join-Path $ctLocal 'build-copy'
if ($ctSource -eq $ctCopy) { throw 'Source and working copy must differ' }
foreach ($ctTool in @('dotnet','ffprobe','ffmpeg')) {
    if (-not (Get-Command $ctTool -ErrorAction SilentlyContinue)) { throw "Required executable missing: $ctTool" }
}
& $Python -c "import UnityPy, PIL, fontTools; assert UnityPy.__version__ == '1.25.0'"
if ($LASTEXITCODE -ne 0) { throw 'Require Python with UnityPy 1.25.0, Pillow and fontTools; see scripts/README.md' }
if (-not (Test-Path -LiteralPath $ctCopy)) {
    [System.IO.Directory]::CreateDirectory($ctCopy) | Out-Null
    Get-ChildItem -LiteralPath $ctSource -Force | Copy-Item -Destination $ctCopy -Recurse
}
# Existing copies are validated by SHA256; no deletion or replacement of user files.
$ctIlspy = Join-Path $ctLocal 'tools/ilspy/ilspycmd.exe'
if (-not (Test-Path -LiteralPath $ctIlspy)) {
    dotnet tool install ilspycmd --version 9.1.0.7988 --tool-path (Join-Path $ctLocal 'tools/ilspy') --allow-roll-forward
    if ($LASTEXITCODE -ne 0) { throw 'ILSpy installation failed' }
}
$ctPythonTools = Join-Path $ctLocal 'tools/python'
if (-not (Test-Path -LiteralPath (Join-Path $ctPythonTools 'TypeTreeGeneratorAPI'))) {
    & $Python -m pip install --target $ctPythonTools --no-deps TypeTreeGeneratorAPI==0.0.10
    if ($LASTEXITCODE -ne 0) { throw 'TypeTreeGenerator local installation failed' }
}
dotnet build (Join-Path $PSScriptRoot 'metadata-index/MetadataIndex.csproj') -o (Join-Path $ctLocal 'tools/metadata') ("-p:BaseIntermediateOutputPath="+(Join-Path $ctLocal 'tools/metadata-obj/')) --nologo
if ($LASTEXITCODE -ne 0) { throw 'Metadata tool build failed' }
$ctManaged = Join-Path $ctCopy 'CrunchTime_Data/Managed'
foreach ($ctDll in Get-ChildItem -LiteralPath $ctManaged -Filter '*.dll') {
    $ctName = $ctDll.BaseName
    $ctIndex = if ($ctName -eq 'Assembly-CSharp') { Join-Path $ctLocal 'code-metadata.json' } else { Join-Path $ctLocal ('support-metadata/'+$ctName+'.json') }
    [System.IO.Directory]::CreateDirectory((Split-Path -Parent $ctIndex)) | Out-Null
    dotnet (Join-Path $ctLocal 'tools/metadata/MetadataIndex.dll') $ctDll.FullName $ctIndex
    if ($LASTEXITCODE -ne 0) { throw "Metadata failed: $ctName" }
    $ctOutput = if ($ctName -eq 'Assembly-CSharp') { Join-Path $ctLocal 'decompiled' } else { Join-Path $ctLocal ('decompiled-support/'+$ctName) }
    & $ctIlspy --disable-updatecheck -p -o $ctOutput -r $ctManaged $ctDll.FullName
    if ($LASTEXITCODE -ne 0) { throw "Decompile failed: $ctName" }
    Write-Output "Completed assembly: $ctName"
}
& $ctIlspy --disable-updatecheck -il -o (Join-Path $ctLocal 'il') (Join-Path $ctManaged 'Assembly-CSharp.dll')
if ($LASTEXITCODE -ne 0) { throw 'Game IL disassembly failed' }
& $Python (Join-Path $PSScriptRoot 'analyze_assets.py') --source $ctSource --copy $ctCopy --project $ctProject
if ($LASTEXITCODE -ne 0) { throw 'Asset analysis failed' }
& $Python (Join-Path $PSScriptRoot 'analyze_content.py') --project $ctProject
if ($LASTEXITCODE -ne 0) { throw 'Content analysis failed' }
& $Python (Join-Path $PSScriptRoot 'verify_analysis.py') --source $ctSource --project $ctProject
if ($LASTEXITCODE -ne 0) { throw 'Coverage verification failed' }
Write-Output "Complete. Open $ctLocal/explorer.html and reports/analysis.md"
