param(
    [string]$Project = (Split-Path -Parent $PSScriptRoot),
    [string]$Editor = 'D:\Program files\2022.3.62f3c1\Editor\Unity.exe'
)
$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path -LiteralPath $Project).Path
$localRoot = Join-Path $projectRoot 'local'
$cecilPath = Join-Path (Split-Path -Parent $Editor) 'Data\Managed\Unity.Cecil.dll'
[void][Reflection.Assembly]::LoadFrom($cecilPath)
$enginePath = Join-Path $localRoot 'build-copy\Duckov_Data\Managed\UnityEngine.CoreModule.dll'
$engineAssembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($enginePath)
$appType = $engineAssembly.MainModule.Types | Where-Object FullName -eq 'UnityEngine.Application'
$isEditor = $appType.Methods | Where-Object Name -eq 'get_isEditor'
if (!$isEditor) { throw 'Expected Application.isEditor API was not found' }
$backupRoot = Join-Path $localRoot 'editor-compatibility-originals'
[void](New-Item -ItemType Directory -Path $backupRoot -Force)
$records = @()
foreach ($relative in @('UnityProject\Assets\Plugins\Unity.Burst.dll', 'RestoredUnityProject\Assets\Plugins\Unity.Burst.dll')) {
    $dllPath = Join-Path $localRoot $relative
    if (!(Test-Path -LiteralPath $dllPath)) { continue }
    $beforeHash = (Get-FileHash -LiteralPath $dllPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($dllPath)
    $compiler = $assembly.MainModule.Types | Where-Object FullName -eq 'Unity.Burst.BurstCompiler'
    $helper = $compiler.NestedTypes | Where-Object Name -eq 'BurstCompilerHelper'
    $method = $helper.Methods | Where-Object Name -eq 'IsCompiledByBurst'
    if (!$method -or !$method.HasBody) { throw "Expected Burst helper was not found in $relative" }
    $first = $method.Body.Instructions[0]
    if ($first.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Call -and $first.Operand.Name -eq 'get_isEditor') {
        $assembly.Dispose()
        $records += @{ path=$relative; status='already-compatible'; sha256=$beforeHash }
        continue
    }
    # Only the Editor's native-compiler availability check changes. The original
    # Player branch and every game/Steam method remain intact.
    $backupPath = Join-Path $backupRoot "$beforeHash-Unity.Burst.dll"
    if (!(Test-Path -LiteralPath $backupPath)) { Copy-Item -LiteralPath $dllPath -Destination $backupPath }
    $processor = $method.Body.GetILProcessor()
    $getter = $assembly.MainModule.ImportReference($isEditor)
    $processor.InsertBefore($first, $processor.Create([Mono.Cecil.Cil.OpCodes]::Call, $getter))
    $processor.InsertBefore($first, $processor.Create([Mono.Cecil.Cil.OpCodes]::Brfalse, $first))
    $processor.InsertBefore($first, $processor.Create([Mono.Cecil.Cil.OpCodes]::Ldc_I4_0))
    $processor.InsertBefore($first, $processor.Create([Mono.Cecil.Cil.OpCodes]::Ret))
    $temporary = "$dllPath.editor-compatible"
    $assembly.Write($temporary)
    $assembly.Dispose()
    # Read the emitted IL again before replacing the recovery copy.
    $check = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($temporary)
    $checkMethod = ($check.MainModule.Types | Where-Object FullName -eq 'Unity.Burst.BurstCompiler').NestedTypes |
        Where-Object Name -eq 'BurstCompilerHelper' | ForEach-Object { $_.Methods } | Where-Object Name -eq 'IsCompiledByBurst'
    if ($checkMethod.Body.Instructions[0].Operand.Name -ne 'get_isEditor') { throw 'Emitted Editor guard failed verification' }
    $check.Dispose()
    Move-Item -LiteralPath $temporary -Destination $dllPath -Force
    $records += @{ path=$relative; status='compatible-copy'; originalSha256=$beforeHash;
        sha256=(Get-FileHash -LiteralPath $dllPath -Algorithm SHA256).Hash.ToLowerInvariant();
        method='Unity.Burst.BurstCompiler/BurstCompilerHelper::IsCompiledByBurst';
        change='Application.isEditor returns false before the native Burst availability probe' }
}
$engineAssembly.Dispose()
$records | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $localRoot 'editor-burst-repair.json') -Encoding utf8
$records | Select-Object path,status | Format-Table
