# ---------------------------------------------------------------------------
# 运行时组件冲突静态检查（GameObject 构造器）
#
# 为什么需要它：`new GameObject(name, typeof(A), typeof(B), ...)` 里的组件冲突
# **编译器抓不到**，只在运行时才报错，例如：
#   · "Can't add component 'RectTransform' to X because such a component is
#      already added"  —— UI 组件（Graphic / Selectable / Canvas / Mask ...）
#      已经自动带上 RectTransform，列表里再写一次就冲突。
#   · "Can't add 'Button' to X because a 'Dropdown' is already added" ——
#      Dropdown 与 Button 都是 Selectable，不能共存。
#
# 本脚本扫描 Assets/Scripts/UI 下的源码，按内置规则表报告冲突。
# 规则表按需扩充即可（见 $AutoAdds / $MutuallyExclusive）。
#
# 用法（由 check-ui.ps1 自动调用，也可单独运行）：
#   powershell -NoProfile -ExecutionPolicy Bypass -File Tools/UiCompileCheck/check-components.ps1
# ---------------------------------------------------------------------------
[CmdletBinding()]
param(
    [string]$ScriptsDir,
    [switch]$Quiet
)

$ErrorActionPreference = 'Stop'

if (-not $ScriptsDir) {
    $projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
    $ScriptsDir = Join-Path $projectRoot 'Assets\Scripts\UI'
}

# ---------- 规则表 ----------

# 这些组件一旦出现在同一 GameObject 上，就会自动带上 RectTransform。
$AutoAddsRectTransform = @(
    'Image', 'RawImage', 'Text', 'TextMeshProUGUI', 'ScrollRect', 'Mask',
    'RectMask2D', 'Canvas', 'CanvasScaler', 'GraphicRaycaster', 'CanvasRenderer',
    'Button', 'Toggle', 'Dropdown', 'Slider', 'Scrollbar', 'InputField',
    'HorizontalLayoutGroup', 'VerticalLayoutGroup', 'GridLayoutGroup',
    'ContentSizeFitter', 'LayoutElement', 'AspectRatioFitter',
    'CanvasGroup', 'Outline', 'Shadow', 'Selectable'
)

# 互斥的 Selectable 派生类：一个 GameObject 只能挂一个。
$SelectableTypes = @('Button', 'Toggle', 'Dropdown', 'Slider', 'Scrollbar', 'InputField')

function Get-TypeShortName([string]$t) { return ($t -replace '^.*\.', '').Trim() }

$problems = @()
$scanned = 0
$calls = 0

Get-ChildItem $ScriptsDir -Recurse -Filter *.cs | ForEach-Object {
    $scanned++
    $text = [System.IO.File]::ReadAllText($_.FullName)
    $lines = $text -split "`r?`n"

    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]

        # 只处理带组件列表的构造调用（类型列表可能跨行，这里做简单括号配对）
        if ($line -notmatch 'new\s+GameObject\s*\(') { continue }
        if ($line -notmatch 'typeof') { continue }

        # 收集从这一行开始、括号配平的整段文本
        $buf = $line
        $depth = ($buf.ToCharArray() | Where-Object { $_ -eq '(' }).Count -
                 ($buf.ToCharArray() | Where-Object { $_ -eq ')' }).Count
        $j = $i
        while ($depth -gt 0 -and $j + 1 -lt $lines.Count) {
            $j++
            $buf += ' ' + $lines[$j]
            $depth += ($lines[$j].ToCharArray() | Where-Object { $_ -eq '(' }).Count -
                      ($lines[$j].ToCharArray() | Where-Object { $_ -eq ')' }).Count
        }

        $calls++
        $types = [regex]::Matches($buf, 'typeof\s*\(\s*([A-Za-z0-9_\.]+)\s*\)') |
                 ForEach-Object { Get-TypeShortName $_.Groups[1].Value }

        if ($types.Count -le 1) { continue }

        # 规则 1：RectTransform 与"会自动添加 RectTransform 的组件"冲突
        if ($types -contains 'RectTransform') {
            $also = $types | Where-Object { $AutoAddsRectTransform -contains $_ }
            if ($also) {
                $problems += [pscustomobject]@{
                    File = $_.Name
                    Line = $i + 1
                    Kind = 'RectTransform 重复'
                    Detail = "RectTransform 与 $($also -join ', ') 同时出现在组件列表里"
                }
            }
        }

        # 规则 2：多个 Selectable 派生类互斥
        $selectables = $types | Where-Object { $SelectableTypes -contains $_ }
        if ($selectables.Count -gt 1) {
            $problems += [pscustomobject]@{
                File = $_.Name
                Line = $i + 1
                Kind = '多个 Selectable'
                Detail = "$($selectables -join ' + ') 不能共存（一个 GameObject 只能有一个 Selectable）"
            }
        }
    }
}

if (-not $Quiet) {
    Write-Host "组件冲突检查：扫描 $scanned 个文件，检查 $calls 处 GameObject 构造。"
}

if ($problems.Count -eq 0) {
    if (-not $Quiet) { Write-Host "未发现组件冲突。" -ForegroundColor Green }
    exit 0
}

Write-Host ""
foreach ($p in $problems) {
    Write-Host ("  {0}:{1}  [{2}]  {3}" -f $p.File, $p.Line, $p.Kind, $p.Detail) -ForegroundColor Red
}
Write-Host ""
Write-Host "发现 $($problems.Count) 处组件冲突（运行时才会报错，请修掉）。" -ForegroundColor Red
exit 1
