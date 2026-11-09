param(
    [switch]$Run,
    [string]$Date = '2026-11-10',
    [string]$Directory = '2026-11-10-identity',
    [string]$Baseline = '',
    [string]$RuntimeVersion = '8.0.15'
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)
$OutputEncoding = [Console]::OutputEncoding
$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
Set-Location $projectRoot
if (-not $Baseline) {
    $Baseline = & python Tools/Statistics/statistics_common.py baseline
    if ($LASTEXITCODE -ne 0) { throw 'No published baseline found; specify -Baseline' }
}
$Baseline = [System.IO.Path]::GetFullPath($Baseline)
$sampleRoot = "Tools/Statistics/$Directory"
$buildRoot = 'Tools/HeadlessTests/bin/identity-sample'
if (-not $Run) {
    Write-Output "PLAN ONLY: $sampleRoot; NEW identity 2000 + identity-preferred 600 games."
    Write-Output "Carry forward unchanged 1v1 / 2v2 / dual / eight CSV and logs from $Baseline"
    Write-Output 'Audit and regression run anew; all fresh checks reuse one .NET 8.0.15 assembly.'
    Write-Output 'Run again with -Run only after gameplay, tests, UI and tooling are final.'
    exit 0
}
if (Test-Path -LiteralPath $sampleRoot) { throw "Refusing to overwrite existing round: $sampleRoot" }
if (-not (Test-Path -LiteralPath $Baseline)) { throw "Missing baseline: $Baseline" }
$env:DOTNET_PROCESSOR_COUNT = '1'
$env:DOTNET_TieredCompilation = '0'
$env:COMPlus_TieredCompilation = '0'
$env:PYTHONDONTWRITEBYTECODE = '1'
$env:FDD_REVISION_ROOT = $sampleRoot
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/HeadlessTests/run-tests.ps1 -Only AiStatePolicyTests -SkipStatistics -OutputDirectory $buildRoot -RuntimeVersion $RuntimeVersion
if ($LASTEXITCODE -ne 0) { throw 'Sampling build validation failed' }
$samplerBinary = Join-Path $buildRoot 'HeadlessTests.dll'
$samplerHash = (Get-FileHash -LiteralPath $samplerBinary -Algorithm SHA256).Hash
python Tools/Statistics/statistics_common.py capture "$sampleRoot/provenance.json"
if ($LASTEXITCODE -ne 0) { throw 'Provenance capture failed' }
$manifestPath = Join-Path $sampleRoot 'provenance.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$baselineHash = (Get-FileHash -LiteralPath $Baseline -Algorithm SHA256).Hash.ToLowerInvariant()
Copy-Item -LiteralPath $Baseline -Destination "$sampleRoot/baseline-results.json"
$files = @{}
$baselineDirectory = Split-Path -Parent $Baseline
foreach ($mode in @('1v1', '2v2', 'dual', 'eight')) {
    foreach ($suffix in @('.csv', '.log')) {
        $name = $mode + $suffix
        $source = Join-Path $baselineDirectory $name
        Copy-Item -LiteralPath $source -Destination "$sampleRoot/$name"
        $files[$name] = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}
$manifest | Add-Member -NotePropertyName sampler_assembly_sha256 -NotePropertyValue $samplerHash
$manifest | Add-Member -NotePropertyName sampler_runtime_version -NotePropertyValue $RuntimeVersion
$manifest | Add-Member -NotePropertyName carried_forward -NotePropertyValue @{
    baseline_path = $Baseline; baseline_sha256 = $baselineHash; files = $files
}
[System.IO.File]::WriteAllText((Join-Path $projectRoot $manifestPath), ($manifest | ConvertTo-Json -Depth 8), (New-Object System.Text.UTF8Encoding($false)))
foreach ($mode in @('audit', 'identity', 'identity-preferred')) {
    python Tools/Statistics/statistics_common.py verify "$sampleRoot/provenance.json"
    if ($LASTEXITCODE -ne 0) { throw 'Sources changed; restart identity sampling' }
    if ($mode -eq 'identity-preferred') {
        Copy-Item -LiteralPath "$sampleRoot/identity-roles.tsv" -Destination 'Assets/Resources/Art/Battle/v1/Adventure/identity-role-rates.txt' -Force
    }
    $env:FDD_REVISION_MODE = $mode
    if ((Get-FileHash -LiteralPath $samplerBinary -Algorithm SHA256).Hash -ne $samplerHash) { throw 'Sampler binary changed during the round' }
    dotnet --fx-version $RuntimeVersion $samplerBinary RevisionBalanceTests | Tee-Object -Variable modeLog
    $modeLog | Out-File -LiteralPath "$sampleRoot/$mode.log" -Encoding utf8
    if ($LASTEXITCODE -ne 0) { throw "Sampling failed: $mode" }
}
dotnet --fx-version $RuntimeVersion $samplerBinary --skip-statistics | Tee-Object -Variable regressionLog
$regressionLog | Out-File -LiteralPath "$sampleRoot/regression.log" -Encoding utf8
if ($LASTEXITCODE -ne 0) { throw 'Regression failed' }
python Tools/Statistics/statistics_common.py verify "$sampleRoot/provenance.json"
if ($LASTEXITCODE -ne 0) { throw 'Sources changed; restart identity sampling' }
if ((Get-FileHash -LiteralPath $samplerBinary -Algorithm SHA256).Hash -ne $samplerHash) { throw 'Sampler binary changed' }
if ((Get-FileHash -LiteralPath $Baseline -Algorithm SHA256).Hash.ToLowerInvariant() -ne $baselineHash) { throw 'Baseline changed during sampling' }
python Tools/Statistics/summarize-current.py --date $Date --directory $Directory --identity-only --baseline "$sampleRoot/baseline-results.json" --update-roster
if ($LASTEXITCODE -ne 0) { throw 'Identity summary validation failed' }
