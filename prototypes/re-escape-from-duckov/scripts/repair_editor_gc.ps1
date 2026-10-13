param(
    [string]$Project = (Split-Path -Parent $PSScriptRoot),
    [string]$Editor = 'D:\Program files\2022.3.62f3c1\Editor\Unity.exe'
)
$ErrorActionPreference = 'Stop'
$localRoot = Join-Path (Resolve-Path -LiteralPath $Project).Path 'local'
[void][Reflection.Assembly]::LoadFrom((Join-Path (Split-Path -Parent $Editor) 'Data\Managed\Unity.Cecil.dll'))
$resolver = [Mono.Cecil.DefaultAssemblyResolver]::new()
$resolver.AddSearchDirectory((Join-Path $localRoot 'build-copy\Duckov_Data\Managed'))
$resolver.AddSearchDirectory((Join-Path $localRoot 'RestoredUnityProject\Assets\Plugins'))
$reader = [Mono.Cecil.ReaderParameters]::new()
$reader.AssemblyResolver = $resolver
$engine = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $localRoot 'build-copy\Duckov_Data\Managed\UnityEngine.CoreModule.dll'), $reader)
$isEditor = ($engine.MainModule.Types | Where-Object FullName -eq 'UnityEngine.Application').Methods | Where-Object Name -eq 'get_isEditor'
$path = Join-Path $localRoot 'RestoredUnityProject\Assets\Plugins\UISplineRenderer.dll'
$beforeHash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($path, $reader)
$type = $assembly.MainModule.Types | Where-Object FullName -eq 'UI_Spline_Renderer.UISplineRenderer'
if ($type.Methods | Where-Object Name -eq 'DuckovRecoverySetGcMode') {
    $assembly.Dispose(); $engine.Dispose(); Write-Output 'Already Editor GC compatible'; exit 0
}
$method = $type.Methods | Where-Object Name -eq 'DoExtrudeSplineJobAll'
$calls = @($method.Body.Instructions | Where-Object { $_.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Call -and $_.Operand.Name -eq 'set_GCMode' })
if ($calls.Count -ne 2) { throw 'Unexpected UISplineRenderer GC setter call count' }
$setter = $calls[0].Operand
$attributes = [Mono.Cecil.MethodAttributes]::Private -bor [Mono.Cecil.MethodAttributes]::Static -bor [Mono.Cecil.MethodAttributes]::HideBySig
$wrapper = [Mono.Cecil.MethodDefinition]::new('DuckovRecoverySetGcMode', $attributes, $assembly.MainModule.TypeSystem.Void)
$wrapper.Parameters.Add([Mono.Cecil.ParameterDefinition]::new($setter.Parameters[0].ParameterType))
$il = $wrapper.Body.GetILProcessor()
$ret = $il.Create([Mono.Cecil.Cil.OpCodes]::Ret)
$il.Append($il.Create([Mono.Cecil.Cil.OpCodes]::Call, $assembly.MainModule.ImportReference($isEditor)))
$il.Append($il.Create([Mono.Cecil.Cil.OpCodes]::Brtrue, $ret))
$il.Append($il.Create([Mono.Cecil.Cil.OpCodes]::Ldarg_0))
$il.Append($il.Create([Mono.Cecil.Cil.OpCodes]::Call, $setter))
$il.Append($ret)
$type.Methods.Add($wrapper)
foreach ($call in $calls) { $call.Operand = $wrapper }
$backup = Join-Path $localRoot "editor-compatibility-originals\$beforeHash-UISplineRenderer.dll"
if (!(Test-Path -LiteralPath $backup)) { Copy-Item -LiteralPath $path -Destination $backup }
$assembly.Write("$path.editor-compatible")
$assembly.Dispose(); $engine.Dispose()
Move-Item -LiteralPath "$path.editor-compatible" -Destination $path -Force
@{ path=$path; originalSha256=$beforeHash; sha256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant();
   change='Skip the two unsupported GCMode setter calls in Editor only; preserve spline computation and Player behavior' } |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $localRoot 'editor-gc-repair.json') -Encoding utf8
Write-Output 'UISplineRenderer Editor GC compatibility applied'
