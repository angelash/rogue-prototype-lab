param([string]$Python = 'python', [int]$Port = 8784)
$ErrorActionPreference = 'Stop'
$projectPath = Split-Path -Parent $PSScriptRoot
& $Python (Join-Path $PSScriptRoot 'serve_explorer.py') --project $projectPath --port $Port
if ($LASTEXITCODE -ne 0) { throw 'Explorer stopped with an error.' }
