# ---------------------------------------------------------------------------
# 表现层（UI）编译检查
#
# 为什么需要它：Unity/Tuanjie 的编辑器批处理模式在本机不可用（Tuanjie.exe 直接崩溃），
# 所以无法用 SaveAssets/刷新 Assembly-CSharp 的方式验证表现层脚本。
# 本脚本改用**真实的引擎程序集**（UnityEngine.*.dll、UnityEngine.UI.dll）直接编译
# Assets/Scripts 下的全部脚本，能在秒级发现：
#   · 写错的 Unity API / 名字空间
#   · 需要引用的引擎模块缺失
#   · 手误的类型与签名错误
#
# 它**不能**替代在编辑器里真跑一次：运行期的布局、点击、字体渲染等问题仍需实机验证。
#
# 用法：
#   powershell -NoProfile -ExecutionPolicy Bypass -File Tools/UiCompileCheck/check-ui.ps1
# ---------------------------------------------------------------------------
[CmdletBinding()]
param(
    [string]$EditorRoot = $env:FDD_EDITOR_ROOT,
    [switch]$Quiet
)

$ErrorActionPreference = 'Stop'

# ---------- 1. 定位引擎 ----------
if (-not $EditorRoot) {
    foreach ($root in @('C:\Program Files\Tuanjie\Hub\Editor', 'C:\Program Files\Unity\Hub\Editor')) {
        if (-not (Test-Path $root)) { continue }

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
    throw "找不到引擎 Data 目录。请用 -EditorRoot 指定。"
}

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$csc         = Join-Path $projectRoot 'Tools\HeadlessTests\bin\csc\csc.dll'
$bcl         = Join-Path $EditorRoot 'MonoBleedingEdge\lib\mono\4.8-api'
$unityMgd    = Join-Path $EditorRoot 'Managed\UnityEngine'
$uiDll       = Join-Path $projectRoot 'Library\ScriptAssemblies\UnityEngine.UI.dll'

if (-not (Test-Path $csc)) {
    throw "找不到编译器：$csc`n请先运行一次 Tools/HeadlessTests/run-tests.ps1（它会复制 Roslyn）。"
}
if (-not (Test-Path $uiDll)) {
    throw "找不到 UnityEngine.UI.dll：$uiDll`n请先在编辑器中打开一次工程以完成包导入。"
}

# ---------- 2. 引擎模块引用 ----------
$engineModules = @(
    'UnityEngine.dll',
    'UnityEngine.CoreModule.dll',
    'UnityEngine.UIModule.dll',
    'UnityEngine.UIElementsModule.dll',
    'UnityEngine.IMGUIModule.dll',
    'UnityEngine.TextRenderingModule.dll',
    'UnityEngine.InputLegacyModule.dll',
    'UnityEngine.JSONSerializeModule.dll',
    'UnityEngine.ImageConversionModule.dll',
    'UnityEngine.AnimationModule.dll',
    'UnityEngine.AudioModule.dll',
    'UnityEngine.Physics2DModule.dll',
    'UnityEngine.UnityWebRequestModule.dll'
)

$refs = @(
    "$bcl\mscorlib.dll",
    "$bcl\System.dll",
    "$bcl\System.Core.dll",
    "$bcl\Facades\netstandard.dll",
    "$bcl\Facades\System.Runtime.dll"
)

$missing = @()
foreach ($m in $engineModules) {
    $p = Join-Path $unityMgd $m
    if (Test-Path $p) { $refs += $p } else { $missing += $m }
}
$refs += $uiDll
$refs = $refs | Where-Object { Test-Path $_ }

# ---------- 3. 源文件 ----------
$sources = Get-ChildItem (Join-Path $projectRoot 'Assets\Scripts') -Recurse -Filter *.cs |
           Select-Object -ExpandProperty FullName

if ($sources.Count -eq 0) { throw "Assets\Scripts 下没有找到任何 .cs 文件。" }

$outDir = Join-Path $PSScriptRoot 'bin'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$out = Join-Path $outDir 'UiCompileCheck.dll'

if (-not $Quiet) {
    Write-Host "引擎     : $EditorRoot"
    Write-Host "UI 程序集: $uiDll"
    Write-Host "源文件   : $($sources.Count) 个"
    if ($missing.Count -gt 0) { Write-Host "未找到（跳过）: $($missing -join ', ')" -ForegroundColor Yellow }
    Write-Host ""
}

# ---------- 4. 编译 ----------
$cscArgs = @(
    $csc, '-nologo', '-target:library', '-langversion:9.0', '-nostdlib+',
    '-warn:4', '-warnaserror-',
    "-out:$out"
)
foreach ($r in $refs) { $cscArgs += "-r:$r" }
$cscArgs += $sources

$output = & dotnet @cscArgs 2>&1
$exit = $LASTEXITCODE

if ($output) { $output | ForEach-Object { Write-Host $_ } }

if ($exit -ne 0) {
    Write-Host ""
    Write-Host "表现层编译失败（exit $exit）" -ForegroundColor Red
    exit $exit
}

# ---------- 5. 运行时组件冲突检查 ----------
# 这类错误编译器抓不到（只在运行时 Debug.LogError），所以单独做静态检查：
#   · RectTransform 与 UI 组件重复添加
#   · Dropdown / Button 等多个 Selectable 共存
$conflictScript = Join-Path $PSScriptRoot 'check-components.ps1'
if (Test-Path $conflictScript) {
    & powershell -NoProfile -ExecutionPolicy Bypass -File $conflictScript -Quiet
    if ($LASTEXITCODE -ne 0) {
        Write-Host ""
        Write-Host "表现层存在运行时组件冲突，请先修掉。" -ForegroundColor Red
        exit $LASTEXITCODE
    }
}

if (-not $Quiet) {
    Write-Host "表现层编译通过（$($sources.Count) 个脚本），且无组件冲突。" -ForegroundColor Green
    Write-Host "提醒：这只能证明能编译，界面效果仍需在编辑器里实机确认。" -ForegroundColor Yellow
}
exit 0
