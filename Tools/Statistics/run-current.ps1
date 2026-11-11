param(
    [switch]$Run,
    [string]$Date = '2026-11-11',
    [string]$Directory = '2026-11-11-ai-activation',
    [string]$Baseline = '',
    [string]$RuntimeVersion = '8.0.15',
    [ValidateRange(0, [long]::MaxValue)]
    [long]$ProcessorAffinity = 0
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)
$OutputEncoding = [Console]::OutputEncoding
$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
Set-Location $projectRoot
$sampleRoot = "Tools/Statistics/$Directory"
$buildRoot = 'Tools/HeadlessTests/bin/state-ai-sample'

function Invoke-PinnedSampler {
    param([string]$Binary, [string]$Runtime, [string]$Fixture, [string]$LogPath,
          [long]$Affinity, [string]$DiagnosticsRoot)
    New-Item -ItemType Directory -Force -Path $DiagnosticsRoot | Out-Null
    $rawPath = Join-Path $DiagnosticsRoot ((Split-Path -Leaf $LogPath) + '.raw')
    $errorPath = Join-Path $DiagnosticsRoot ((Split-Path -Leaf $LogPath) + '.stderr')
    $arguments = @('--fx-version', $Runtime, ('"' + [System.IO.Path]::GetFullPath($Binary) + '"'), $Fixture)
    $process = Start-Process -FilePath 'dotnet' -ArgumentList $arguments -WindowStyle Hidden -PassThru `
        -RedirectStandardOutput $LogPath -RedirectStandardError $errorPath
    try {
        # Keep the process handle alive so ExitCode survives WaitForExit on Windows PowerShell.
        $null = $process.Handle
        $process.ProcessorAffinity = [IntPtr]$Affinity
        if ($process.ProcessorAffinity.ToInt64() -ne $Affinity) { throw 'Processor affinity was not applied' }
        $process.WaitForExit()
        $exitCode = $process.ExitCode
    } catch {
        if (-not $process.HasExited) { $process.Kill(); $process.WaitForExit() }
        throw
    } finally {
        $process.Dispose()
    }
    # Standalone child stdout on this host uses CP936. Preserve its bytes under ignored bin,
    # then decode strictly and publish UTF-8; reject any lossy conversion.
    Copy-Item -LiteralPath $LogPath -Destination $rawPath -Force
    $bytes = [System.IO.File]::ReadAllBytes([System.IO.Path]::GetFullPath($LogPath))
    $utf8 = New-Object System.Text.UTF8Encoding($false, $true)
    try { $logText = $utf8.GetString($bytes) } catch [System.Text.DecoderFallbackException] {
        $encoding = [System.Text.Encoding]::GetEncoding(936,
            [System.Text.EncoderFallback]::ExceptionFallback, [System.Text.DecoderFallback]::ExceptionFallback)
        $logText = $encoding.GetString($bytes)
        if ([Convert]::ToBase64String($encoding.GetBytes($logText)) -ne [Convert]::ToBase64String($bytes)) {
            throw 'Child stdout cannot be converted losslessly from CP936'
        }
    }
    [System.IO.File]::WriteAllText([System.IO.Path]::GetFullPath($LogPath), $logText, $utf8)
    Write-Output $logText
    if ($exitCode -ne 0) { throw "Sampler failed ($Fixture), exit $exitCode; stderr: $errorPath" }
}
if (-not $Baseline) {
    $Baseline = & python Tools/Statistics/statistics_common.py baseline
    if ($LASTEXITCODE -ne 0) { throw 'No published baseline found; specify -Baseline' }
}
if (-not $Run) {
    Write-Output "PLAN ONLY: $sampleRoot; expected 58 roles, 532 duals, 66,964 games."
    Write-Output 'Modes: audit; 1v1 33060; 2v2 26448; dual 4256; eight 600; identity 2000; identity-preferred 600.'
    Write-Output "Baseline: $Baseline"
    Write-Output "Processor affinity: $ProcessorAffinity (0 = unrestricted)"
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
$manifest | Add-Member -NotePropertyName sampler_processor_affinity -NotePropertyValue $ProcessorAffinity
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
    if ($ProcessorAffinity) {
        Invoke-PinnedSampler -Binary $samplerBinary -Runtime $RuntimeVersion -Fixture RevisionBalanceTests `
            -LogPath "$sampleRoot/$mode.log" -Affinity $ProcessorAffinity -DiagnosticsRoot "$buildRoot/$Directory-diagnostics"
    } else {
        dotnet --fx-version $RuntimeVersion $samplerBinary RevisionBalanceTests |
            Tee-Object -Variable modeLog
        $modeLog | Out-File -LiteralPath "$sampleRoot/$mode.log" -Encoding utf8
        if ($LASTEXITCODE -ne 0) { throw "Sampling failed: $mode" }
    }
}
if ($ProcessorAffinity) {
    Invoke-PinnedSampler -Binary $samplerBinary -Runtime $RuntimeVersion -Fixture --skip-statistics `
        -LogPath "$sampleRoot/regression.log" -Affinity $ProcessorAffinity -DiagnosticsRoot "$buildRoot/$Directory-diagnostics"
} else {
    dotnet --fx-version $RuntimeVersion $samplerBinary --skip-statistics |
        Tee-Object -Variable regressionLog
    $regressionLog | Out-File -LiteralPath "$sampleRoot/regression.log" -Encoding utf8
    if ($LASTEXITCODE -ne 0) { throw 'Regression failed' }
}
python Tools/Statistics/statistics_common.py verify "$sampleRoot/provenance.json"
if ($LASTEXITCODE -ne 0) { throw 'Sources changed; rerun the full round' }
if ((Get-FileHash -LiteralPath $Baseline -Algorithm SHA256).Hash -ne $baselineHash) { throw 'Baseline changed during sampling' }
python Tools/Statistics/summarize-current.py --date $Date --directory $Directory --state-ai --baseline "$sampleRoot/baseline-results.json"
if ($LASTEXITCODE -ne 0) { throw 'Summary validation failed' }
