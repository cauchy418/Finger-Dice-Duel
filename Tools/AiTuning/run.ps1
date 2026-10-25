param([int]$Train=128,[int]$Holdout=320)
$ErrorActionPreference='Stop'
$taskRoot=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$taskBin=Join-Path $PSScriptRoot 'bin'
New-Item -ItemType Directory -Force -Path $taskBin | Out-Null
Copy-Item -LiteralPath (Join-Path $taskRoot 'Tools/HeadlessTests/bin/HeadlessTests.dll') -Destination $taskBin -Force
Copy-Item -LiteralPath (Join-Path $taskRoot 'Tools/HeadlessTests/bin/HeadlessTests.runtimeconfig.json') -Destination (Join-Path $taskBin 'AiTuning.runtimeconfig.json') -Force
$taskBcl='C:/Program Files/Tuanjie/Hub/Editor/2022.3.62t15-x86_64/Editor/Data/MonoBleedingEdge/lib/mono/4.8-api'
$taskArgs=@((Join-Path $taskRoot 'Tools/HeadlessTests/bin/csc/csc.dll'),'-nologo','-target:exe','-langversion:9.0','-nostdlib+',('-out:'+(Join-Path $taskBin 'AiTuning.dll')),('-r:'+(Join-Path $taskBin 'HeadlessTests.dll')))
foreach($taskRef in @('mscorlib.dll','System.dll','System.Core.dll','Facades/netstandard.dll','Facades/System.Runtime.dll')){$taskArgs+=('-r:'+(Join-Path $taskBcl $taskRef))}
$taskArgs+=(Join-Path $PSScriptRoot 'Program.cs')
& dotnet @taskArgs
if($LASTEXITCODE -ne 0){exit $LASTEXITCODE}
$env:DOTNET_TieredCompilation='0';$env:COMPlus_TieredCompilation='0'
& dotnet (Join-Path $taskBin 'AiTuning.dll') $Train $Holdout
exit $LASTEXITCODE
