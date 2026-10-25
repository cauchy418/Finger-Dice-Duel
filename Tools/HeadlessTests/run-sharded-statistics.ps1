param([int]$Shards=8,[switch]$ReuseShards,[string]$Python=$env:FDD_PYTHON,[string]$Only="",[string]$CacheName='shards')
$ErrorActionPreference='Stop'
if($Shards -lt 1 -or $Shards -gt 16){throw 'Shards must be 1..16'}
if($CacheName -notmatch '^[a-z0-9-]+$'){throw 'Invalid cache directory name'}
$taskRoot=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
Set-Location -LiteralPath $taskRoot
$taskBin=Join-Path $PSScriptRoot "bin/$CacheName"
$env:DOTNET_TieredCompilation='0';$env:COMPlus_TieredCompilation='0'
if(-not $ReuseShards){
& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'run-tests.ps1') -Only BreakerAttackOrderTests -OutputDirectory $taskBin -Quiet
if($LASTEXITCODE -ne 0){throw 'Build failed'}
$taskProcesses=@()
for($taskIndex=0;$taskIndex -lt $Shards;$taskIndex++){
    $taskArgs=@("Tools/HeadlessTests/bin/$CacheName/HeadlessTests.dll",'--dual-shard',"$taskIndex","$Shards","Tools/HeadlessTests/bin/$CacheName/shard-$taskIndex.tsv")
    $taskProcess=Start-Process -FilePath (Get-Command dotnet).Source -ArgumentList $taskArgs -WorkingDirectory $taskRoot -WindowStyle Hidden -RedirectStandardOutput (Join-Path $taskBin "shard-$taskIndex.log") -RedirectStandardError (Join-Path $taskBin "shard-$taskIndex.err") -PassThru
    $null=$taskProcess.Handle
    $taskProcesses+=$taskProcess
}
Write-Output "Started $Shards isolated simulation processes."
while(@($taskProcesses | Where-Object {-not $_.HasExited}).Count -gt 0){Start-Sleep -Seconds 1}
foreach($taskProcess in $taskProcesses){$taskProcess.WaitForExit();if($null -ne $taskProcess.ExitCode -and $taskProcess.ExitCode -ne 0){throw "Shard failed: $($taskProcess.Id)"}}
}
for($taskIndex=0;$taskIndex -lt $Shards;$taskIndex++){
    if((Get-Item -LiteralPath (Join-Path $taskBin "shard-$taskIndex.err")).Length -gt 0){throw "Shard $taskIndex wrote errors"}
    if((Get-Content -LiteralPath (Join-Path $taskBin "shard-$taskIndex.log") -Tail 1) -notmatch "^shard $taskIndex completed [0-9]+ games$"){throw "Shard $taskIndex did not complete"}
}
$taskPython=$Python
if(-not $taskPython){$taskPython='C:/Users/29391/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe';if(-not(Test-Path -LiteralPath $taskPython)){$taskPython=(Get-Command python).Source}}
& $taskPython (Join-Path $PSScriptRoot 'merge-dual-shards.py') $taskBin $Shards
if($LASTEXITCODE -ne 0){throw 'Shard merge failed'}
$env:FDD_DUAL_OUTCOMES=Join-Path $taskBin 'outcomes.tsv'
try{if($Only){& dotnet (Join-Path $taskBin 'HeadlessTests.dll') $Only}else{& dotnet (Join-Path $taskBin 'HeadlessTests.dll')};$taskExit=$LASTEXITCODE}finally{Remove-Item Env:FDD_DUAL_OUTCOMES}
exit $taskExit
