param(
    [string]$Project = (Split-Path -Parent $PSScriptRoot),
    [string]$Editor = 'D:\Program files\2022.3.62f3c1\Editor',
    [string]$BuildRoot = ''
)
$ErrorActionPreference = 'Stop'
[void][Reflection.Assembly]::LoadFrom((Join-Path $Editor 'Data\Managed\Unity.Cecil.dll'))
$local = Join-Path $Project 'local'
if (!$BuildRoot) { $BuildRoot = Join-Path $local 'HybridBuild' }
$names = @('TeamSoda.Duckov.Core','TeamSoda.Duckov.Utilities','TeamSoda.MiniLocalizor',
    'ItemStatsSystem','SodaLocalization','Plugins','Assembly-CSharp','KAnimationCore.Runtime','FlexReader','PackageTools')
function AllTypes($types) {
    foreach ($type in $types) {
        $type
        if ($type.HasNestedTypes) { AllTypes $type.NestedTypes }
    }
}
function SerializedFields($type) {
    foreach ($field in $type.Fields) {
        if ($field.IsStatic -or $field.IsLiteral -or $field.IsInitOnly -or $field.IsNotSerialized) { continue }
        $attrs = @($field.CustomAttributes | ForEach-Object {$_.AttributeType.FullName})
        if ($field.IsPublic -or $attrs -contains 'UnityEngine.SerializeField' -or $attrs -contains 'UnityEngine.SerializeReference') {
            $field.Name + ':' + $field.FieldType.FullName
        }
    }
}
function PublicMethods($type) {
    foreach ($method in $type.Methods) {
        if (!$method.IsPublic -and !$method.IsFamily -and !$method.IsFamilyOrAssembly) { continue }
        $method.Name + '`' + $method.GenericParameters.Count + '(' + (($method.Parameters | ForEach-Object {$_.ParameterType.FullName}) -join ',') + '):' + $method.ReturnType.FullName
    }
}
$results = @()
foreach ($name in $names) {
    $originalPath = Join-Path $local ('build-copy\Duckov_Data\Managed\' + $name + '.dll')
    $compiledPath = Join-Path $BuildRoot ($name + '\' + $name + '.dll')
    if (!(Test-Path -LiteralPath $compiledPath)) { $compiledPath = Join-Path $BuildRoot ($name + '.dll') }
    $original = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($originalPath)
    $compiled = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($compiledPath)
    $newTypes = @{}
    foreach ($type in (AllTypes $compiled.MainModule.Types)) { $newTypes[$type.FullName] = $type }
    $missingTypes = @(); $fieldDifferences = @(); $missingMethods = @(); $typeDifferences = @(); $count = 0; $fieldCount = 0
    foreach ($type in (AllTypes $original.MainModule.Types)) {
        if ($type.FullName.Contains('<') -or $type.Name -eq '<Module>' -or $type.Name.Contains('__JobReflectionRegistrationOutput__')) { continue }
        $count++
        $new = $newTypes[$type.FullName]
        if (!$new) { $missingTypes += $type.FullName; continue }
        if ($type.BaseType.FullName -cne $new.BaseType.FullName -or
            ((@($type.Interfaces | ForEach-Object {$_.InterfaceType.FullName}) | Sort-Object) -join '|') -cne
            ((@($new.Interfaces | ForEach-Object {$_.InterfaceType.FullName}) | Sort-Object) -join '|')) {
            $typeDifferences += $type.FullName
        }
        $oldFields = @(SerializedFields $type); $newFields = @(SerializedFields $new); $fieldCount += $oldFields.Count
        if (($oldFields -join '|') -cne ($newFields -join '|')) {
            $fieldDifferences += [pscustomobject]@{type=$type.FullName; original=$oldFields; compiled=$newFields}
        }
        $newMethods = @(PublicMethods $new)
        foreach ($method in (PublicMethods $type)) {
            if ($newMethods -cnotcontains $method) { $missingMethods += $type.FullName + '::' + $method }
        }
    }
    $identity = $original.Name.FullName -ceq $compiled.Name.FullName
    $result = [pscustomobject]@{assembly=$name; identityMatches=$identity; typesChecked=$count;
        serializedFieldsChecked=$fieldCount; missingTypes=$missingTypes; serializedFieldDifferences=$fieldDifferences;
        missingPublicMethods=$missingMethods; inheritanceDifferences=$typeDifferences;
        passed=($identity -and !$missingTypes.Count -and !$fieldDifferences.Count -and !$missingMethods.Count -and !$typeDifferences.Count)}
    $results += $result
    Write-Output ($name + ': types=' + $count + ' fields=' + $fieldCount + ' missingTypes=' + $missingTypes.Count + ' fieldDifferences=' + $fieldDifferences.Count + ' missingMethods=' + $missingMethods.Count + ' identity=' + $identity)
    $original.Dispose(); $compiled.Dispose()
}
$output = Join-Path $local 'hybrid-abi-audit.json'
$results | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $output -Encoding utf8
if (@($results | Where-Object {!$_.passed}).Count) { exit 1 }
exit 0
