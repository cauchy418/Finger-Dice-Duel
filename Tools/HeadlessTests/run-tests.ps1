# ---------------------------------------------------------------------------
# 无头测试构建 / 运行脚本
#
# 为什么需要它：本项目是团结引擎（Unity 衍生物）工程，但游戏逻辑（Core / Characters /
# Assistant）刻意不依赖任何 Unity 类型。因此可以在编辑器之外用 Roslyn 直接编译并
# 运行整套测试，反馈速度远快于启动编辑器。
#
# 用法：
#   pwsh -File Tools/HeadlessTests/run-tests.ps1
#   pwsh -File Tools/HeadlessTests/run-tests.ps1 -Define FDD_UNITY_TESTS   # 校验 Unity 侧测试代码可编译
#
# 说明：
#   编译器与 BCL 引用均取自本机团结引擎安装目录，不需要额外的 .NET SDK。
# ---------------------------------------------------------------------------
[CmdletBinding()]
param(
    [string]$EditorRoot = $env:FDD_EDITOR_ROOT,
    [string]$Define = "",
    [switch]$Quiet,
    # 只跑某个测试类（对应运行器的过滤参数，例如 -Only AdventureLevel1Tests）。
    # 快速迭代单个 fixture 用；回归仍需跑一次全量。
    [string]$Only = ""
)

$ErrorActionPreference = 'Stop'

# ---------- 1. 定位团结引擎安装 ----------
if (-not $EditorRoot) {
    $editorRoots = @(
        'C:\Program Files\Tuanjie\Hub\Editor',
        'C:\Program Files\Unity\Hub\Editor'
    ) | Where-Object { Test-Path $_ }

    foreach ($root in $editorRoots) {
        $latest = Get-ChildItem $root -Directory -ErrorAction SilentlyContinue |
                  Sort-Object Name -Descending | Select-Object -First 1
        if ($latest) {
            $candidate = Join-Path $latest.FullName 'Editor\Data'
            if (Test-Path (Join-Path $candidate 'DotNetSdkRoslyn\csc.dll')) {
                $EditorRoot = $candidate
                break
            }
        }
    }
}

if (-not $EditorRoot -or -not (Test-Path $EditorRoot)) {
    throw "找不到团结引擎 Data 目录。请用 -EditorRoot 指定（应包含 DotNetSdkRoslyn\csc.dll）。"
}

$cscSource = Join-Path $EditorRoot 'DotNetSdkRoslyn'
$bcl       = Join-Path $EditorRoot 'MonoBleedingEdge\lib\mono\4.8-api'
$unityMgd  = Join-Path $EditorRoot 'Managed\UnityEngine'

foreach ($p in @($cscSource, $bcl, $unityMgd)) {
    if (-not (Test-Path $p)) { throw "缺少必要目录：$p" }
}

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$outDir      = Join-Path $PSScriptRoot 'bin'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

# ---------- 2. 复制 Roslyn（放在同目录的 runtime 下，避免 deps.json 问题） ----------
$cscDir = Join-Path $outDir 'csc'
if (-not (Test-Path (Join-Path $cscDir 'csc.dll'))) {
    New-Item -ItemType Directory -Force -Path $cscDir | Out-Null
    Copy-Item (Join-Path $cscSource '*') $cscDir -Recurse -Force
}
$csc = Join-Path $cscDir 'csc.dll'

# ---------- 3. 收集源文件 ----------
# 注意：Assets\Scripts\UI 是表现层（MonoBehaviour / UnityEngine），无法在无头 .NET 下编译，
# 必须排除。它的编译检查由 Tools\UiCompileCheck\check-ui.ps1 负责。
# 其余目录（Core / Characters / Assistant）刻意不依赖 Unity，所以可以在这里编。
$uiDir = Join-Path $projectRoot 'Assets\Scripts\UI'

$production = Get-ChildItem (Join-Path $projectRoot 'Assets\Scripts') -Recurse -Filter *.cs |
              Where-Object {
                  -not $_.FullName.StartsWith($uiDir, [System.StringComparison]::OrdinalIgnoreCase)
              } |
              Select-Object -ExpandProperty FullName
$tests      = Get-ChildItem (Join-Path $projectRoot 'Assets\Tests') -Recurse -Filter *.cs |
              Select-Object -ExpandProperty FullName
$harness    = Join-Path $PSScriptRoot 'Program.cs'

$sources = @($production) + @($tests) + @($harness)

# 校验 Unity 侧测试代码时不能带上无头运行器：
# Program.cs 的反射调度依赖 shim 里的自定义特性，而 Unity 路径下特性来自 NUnit。
$unityPathCheck = [bool]($Define -and $Define -match 'FDD_UNITY_TESTS')
if ($unityPathCheck) { $sources = @($production) + @($tests) }

if ($sources.Count -eq 0) { throw "没有找到任何源文件。" }

# ---------- 3.5 确保 runtimeconfig.json 存在 ----------
# csc 生成的是裸 IL，dotnet 需要一个 runtimeconfig 才能把它当"框架依赖应用"启动。
# 这里自动补上，避免"清空 bin 之后测试跑不起来"。
if (-not $unityPathCheck) {
    $runtimeConfig = Join-Path $outDir 'HeadlessTests.runtimeconfig.json'
    if (-not (Test-Path $runtimeConfig)) {
        $lines = @(
            '{',
            '  "runtimeOptions": {',
            '    "tfm": "net6.0",',
            '    "framework": {',
            '      "name": "Microsoft.NETCore.App",',
            '      "version": "6.0.0"',
            '    }',
            '  }',
            '}'
        )
        # 必须是无 BOM UTF-8，否则 dotnet 会把 BOM 当成非法 JSON 起始字符
        [System.IO.File]::WriteAllText($runtimeConfig, ($lines -join "`r`n"),
            (New-Object System.Text.UTF8Encoding($false)))
    }
}

# ---------- 4. 编译 ----------
$exe = Join-Path $outDir 'HeadlessTests.dll'

$refs = @(
    "$bcl\mscorlib.dll",
    "$bcl\System.dll",
    "$bcl\System.Core.dll",
    "$bcl\Facades\netstandard.dll",
    "$bcl\Facades\System.Runtime.dll"
) | Where-Object { Test-Path $_ }

# 校验 Unity 侧测试代码（-Define FDD_UNITY_TESTS）时需要 NUnit
if ($unityPathCheck) {
    $nunit = Get-ChildItem (Join-Path $projectRoot 'Library\PackageCache') -Recurse `
                           -Filter nunit.framework.dll -ErrorAction SilentlyContinue |
             Select-Object -First 1 -ExpandProperty FullName

    if (-not $nunit) {
        throw "指定了 FDD_UNITY_TESTS，但找不到 nunit.framework.dll（Library\PackageCache）。请先在编辑器中打开一次工程以完成包导入。"
    }

    $refs += $nunit
    if (-not $Quiet) { Write-Host "NUnit    : $nunit" }

    # 该模式下产物只用来验证"能否编译"，不执行；因此编译为库
    $exe = Join-Path $outDir 'UnityPathCompileCheck.dll'
}

$buildTarget = if ($unityPathCheck) { 'library' } else { 'exe' }

$cscArgs = @(
    $csc, '-nologo', "-target:$buildTarget", '-langversion:9.0', '-nostdlib+',
    '-warn:4', '-warnaserror-',
    "-out:$exe"
)
if ($Define) { $cscArgs += "-define:$Define" }
foreach ($r in $refs) { $cscArgs += "-r:$r" }
$cscArgs += $sources

if (-not $Quiet) {
    Write-Host "编译器   : $csc"
    Write-Host "引用目录 : $bcl"
    Write-Host "源文件   : $($sources.Count) 个（产品 $($production.Count) / 测试 $($tests.Count)）"
    Write-Host "输出     : $exe"
    Write-Host ""
}

$compileOutput = & dotnet @cscArgs 2>&1
$compileExit = $LASTEXITCODE

if ($compileOutput) { $compileOutput | ForEach-Object { Write-Host $_ } }

if ($compileExit -ne 0) {
    Write-Host ""
    Write-Host "编译失败（exit $compileExit）" -ForegroundColor Red
    exit $compileExit
}

# ---------- 5. 运行 ----------
if ($unityPathCheck) {
    if (-not $Quiet) {
        Write-Host ""
        Write-Host "Unity 侧测试代码编译通过（仅校验编译，不执行）。" -ForegroundColor Green
    }
    exit 0
}

if (-not $Quiet) { Write-Host "编译成功，开始运行测试……" -ForegroundColor Green; Write-Host "" }

if ($Only) { & dotnet $exe $Only } else { & dotnet $exe }
exit $LASTEXITCODE
