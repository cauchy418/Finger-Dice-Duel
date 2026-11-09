param(
    [switch]$Run,
    [string]$Date = '2026-11-09',
    [string]$Directory = '2026-11-09-state-ai',
    [string]$Baseline = '',
    [string]$RuntimeVersion = '8.0.15'
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)
$OutputEncoding = [Console]::OutputEncoding
$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
Set-Location $projectRoot
$sampleRoot = "Tools/Statistics/$Directory"
$buildRoot = 'Tools/HeadlessTests/bin/state-ai-sample'
if (-not $Baseline) {
    $Baseline = & python Tools/Statistics/statistics_common.py baseline
    if ($LASTEXITCODE -ne 0) { throw 'No published baseline found; specify -Baseline' }
}
if (-not $Run) {
    Write-Output "PLAN ONLY: $sampleRoot; expected 58 roles, 532 duals, 66,964 games."
    Write-Output 'Modes: audit; 1v1 33060; 2v2 26448; dual 4256; eight 600; identity 2000; identity-preferred 600.'
    Write-Output "Baseline: $Baseline"
    Write-Output 'Run again with -Run only after all gameplay and AI changes are finished.'
    exit 0
}
if (Test-Path -LiteralPath $sampleRoot) { throw "Refusing to overwrite existing round: $sampleRoot" }
if (-not (Test-Path -LiteralPath $Baseline)) { throw "Missing baseline: $Baseline" }
$env:DOTNET_PROCESSOR_COUNT = '1'
$env:DOTNET_TieredCompilation = '0'
$env:COMPlus_TieredCompilation = '0'
$env:PYTHONDONTWRITEBYTECODE = '1'
$env:FDD_REVISION_ROOT = $sampleRoot
# Compile once, verify the fixture, then reuse this exact binary for every mode.
# Repeated Roslyn launches on this host can fail inside the .NET runtime.
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/HeadlessTests/run-tests.ps1 -Only AiStatePolicyTests -SkipStatistics -OutputDirectory $buildRoot -RuntimeVersion $RuntimeVersion
if ($LASTEXITCODE -ne 0) { throw 'Sampling build validation failed' }
$samplerBinary = Join-Path $buildRoot 'HeadlessTests.dll'
$samplerHash = (Get-FileHash -LiteralPath $samplerBinary -Algorithm SHA256).Hash
python Tools/Statistics/statistics_common.py capture "$sampleRoot/provenance.json"
if ($LASTEXITCODE -ne 0) { throw 'Provenance capture failed' }
$manifestPath = Join-Path $sampleRoot 'provenance.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$manifest | Add-Member -NotePropertyName sampler_assembly_sha256 -NotePropertyValue $samplerHash
$manifest | Add-Member -NotePropertyName sampler_runtime_version -NotePropertyValue $RuntimeVersion
[System.IO.File]::WriteAllText((Join-Path $projectRoot $manifestPath), ($manifest | ConvertTo-Json -Depth 8), (New-Object System.Text.UTF8Encoding($false)))

$baselineHash = (Get-FileHash -LiteralPath $Baseline -Algorithm SHA256).Hash
Copy-Item -LiteralPath $Baseline -Destination "$sampleRoot/baseline-results.json"
foreach ($mode in @('audit', '1v1', '2v2', 'dual', 'eight', 'identity', 'identity-preferred')) {
    python Tools/Statistics/statistics_common.py verify "$sampleRoot/provenance.json"
    if ($LASTEXITCODE -ne 0) { throw 'Sources changed; rerun the full round' }
    if ($mode -eq 'identity-preferred') {
        Copy-Item -LiteralPath "$sampleRoot/identity-roles.tsv" -Destination 'Assets/Resources/Art/Battle/v1/Adventure/identity-role-rates.txt' -Force
    }
    $env:FDD_REVISION_MODE = $mode
    if ((Get-FileHash -LiteralPath $samplerBinary -Algorithm SHA256).Hash -ne $samplerHash) { throw 'Sampler binary changed during the round' }
    dotnet --fx-version $RuntimeVersion $samplerBinary RevisionBalanceTests |
        Tee-Object -Variable modeLog
    $modeLog | Out-File -LiteralPath "$sampleRoot/$mode.log" -Encoding utf8
    if ($LASTEXITCODE -ne 0) { throw "Sampling failed: $mode" }
}
dotnet --fx-version $RuntimeVersion $samplerBinary --skip-statistics |
    Tee-Object -Variable regressionLog
$regressionLog | Out-File -LiteralPath "$sampleRoot/regression.log" -Encoding utf8
if ($LASTEXITCODE -ne 0) { throw 'Regression failed' }
python Tools/Statistics/statistics_common.py verify "$sampleRoot/provenance.json"
if ($LASTEXITCODE -ne 0) { throw 'Sources changed; rerun the full round' }
if ((Get-FileHash -LiteralPath $Baseline -Algorithm SHA256).Hash -ne $baselineHash) { throw 'Baseline changed during sampling' }
python Tools/Statistics/summarize-current.py --date $Date --directory $Directory --state-ai --baseline "$sampleRoot/baseline-results.json"
if ($LASTEXITCODE -ne 0) { throw 'Summary validation failed' }
