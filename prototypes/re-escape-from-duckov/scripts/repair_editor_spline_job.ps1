param([string]$Project = (Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference = 'Stop'
$localRoot = Join-Path (Resolve-Path -LiteralPath $Project).Path 'local'
[void][Reflection.Assembly]::LoadFrom('D:\Program files\2022.3.62f3c1\Editor\Data\Managed\Unity.Cecil.dll')
$resolver = [Mono.Cecil.DefaultAssemblyResolver]::new()
$resolver.AddSearchDirectory((Join-Path $localRoot 'build-copy\Duckov_Data\Managed'))
$reader = [Mono.Cecil.ReaderParameters]::new(); $reader.AssemblyResolver = $resolver
$engine = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $localRoot 'build-copy\Duckov_Data\Managed\UnityEngine.CoreModule.dll'),$reader)
$getter = ($engine.MainModule.Types | Where-Object FullName -eq 'UnityEngine.Application').Methods | Where-Object Name -eq 'get_isEditor'
$path = Join-Path $localRoot 'RestoredUnityProject\Assets\Plugins\UISplineRenderer.dll'
$beforeHash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($path,$reader)
$type = $assembly.MainModule.Types | Where-Object FullName -eq 'UI_Spline_Renderer.UISplineRenderer'
if ($type.Methods | Where-Object Name -eq 'DuckovRecoveryScheduleSpline') {
    $assembly.Dispose(); $engine.Dispose(); Write-Output 'Already Editor spline compatible'; exit 0
}
$extrude = $type.Methods | Where-Object Name -eq 'DoExtrudeSplineJob'
$scheduleCall = @($extrude.Body.Instructions | Where-Object { $_.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Call -and $_.Operand.Name -eq 'Schedule' })
if ($scheduleCall.Count -ne 1) { throw 'Unexpected spline Schedule call count' }
$schedule = $scheduleCall[0].Operand
$jobType = $assembly.MainModule.Types | Where-Object FullName -eq $schedule.GenericArguments[0].FullName
$execute = $jobType.Methods | Where-Object Name -eq 'Execute'
$handleType = ($engine.MainModule.Types | Where-Object FullName -eq 'Unity.Jobs.JobHandle')
$complete = $handleType.Methods | Where-Object { $_.Name -eq 'Complete' -and !$_.IsStatic }
$attributes = [Mono.Cecil.MethodAttributes]::Private -bor [Mono.Cecil.MethodAttributes]::Static -bor [Mono.Cecil.MethodAttributes]::HideBySig
$wrapper = [Mono.Cecil.MethodDefinition]::new('DuckovRecoveryScheduleSpline',$attributes,$assembly.MainModule.ImportReference($handleType))
$wrapper.Parameters.Add([Mono.Cecil.ParameterDefinition]::new($jobType))
$wrapper.Parameters.Add([Mono.Cecil.ParameterDefinition]::new($assembly.MainModule.ImportReference($handleType)))
$wrapper.Body.InitLocals=$true
$result = [Mono.Cecil.Cil.VariableDefinition]::new($assembly.MainModule.ImportReference($handleType)); $wrapper.Body.Variables.Add($result)
$il=$wrapper.Body.GetILProcessor(); $player=$il.Create([Mono.Cecil.Cil.OpCodes]::Ldarg_0)
$il.Append($il.Create([Mono.Cecil.Cil.OpCodes]::Call,$assembly.MainModule.ImportReference($getter)))
$il.Append($il.Create([Mono.Cecil.Cil.OpCodes]::Brfalse,$player))
$il.Append($il.Create([Mono.Cecil.Cil.OpCodes]::Ldarga,$wrapper.Parameters[1]))
$il.Append($il.Create([Mono.Cecil.Cil.OpCodes]::Call,$assembly.MainModule.ImportReference($complete)))
$il.Append($il.Create([Mono.Cecil.Cil.OpCodes]::Ldarga,$wrapper.Parameters[0]))
$il.Append($il.Create([Mono.Cecil.Cil.OpCodes]::Call,$execute))
$il.Append($il.Create([Mono.Cecil.Cil.OpCodes]::Ldloca,$result))
$il.Append($il.Create([Mono.Cecil.Cil.OpCodes]::Initobj,$assembly.MainModule.ImportReference($handleType)))
$il.Append($il.Create([Mono.Cecil.Cil.OpCodes]::Ldloc,$result))
$il.Append($il.Create([Mono.Cecil.Cil.OpCodes]::Ret))
$il.Append($player); $il.Append($il.Create([Mono.Cecil.Cil.OpCodes]::Ldarg_1))
$il.Append($il.Create([Mono.Cecil.Cil.OpCodes]::Call,$schedule)); $il.Append($il.Create([Mono.Cecil.Cil.OpCodes]::Ret))
$type.Methods.Add($wrapper); $scheduleCall[0].Operand=$wrapper
$backup=Join-Path $localRoot "editor-compatibility-originals\$beforeHash-UISplineRenderer.dll"
if(!(Test-Path -LiteralPath $backup)){Copy-Item -LiteralPath $path -Destination $backup}
$assembly.Write("$path.editor-compatible"); $assembly.Dispose(); $engine.Dispose()
Move-Item -LiteralPath "$path.editor-compatible" -Destination $path -Force
@{ originalSha256=$beforeHash; sha256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant();
change='Run the same spline job Execute method synchronously in Editor, completing its dependency; retain Player scheduling' } |
ConvertTo-Json | Set-Content -LiteralPath (Join-Path $localRoot 'editor-spline-job-repair.json') -Encoding utf8
Write-Output 'Editor spline job compatibility applied'
