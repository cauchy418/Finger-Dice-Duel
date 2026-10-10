# ============================================================
#  push.ps1 — 一键推送到 GitHub（Finger Dice Duel）
#
#  为什么需要它：这台机器上 git 的默认配置推不动，原因有两个 ——
#    1. git 默认用 schannel（Windows TLS 后端），在受限环境里会报
#       `SEC_E_NO_CREDENTIALS`（拿不到证书句柄）；
#    2. 直连 github.com:443 的 HTTPS 会被拦，必须走本机代理。
#
#  所以本脚本用 `openssl` 后端 + 本机代理 + 一次性凭据来推送。
#
#  用法：
#    powershell -NoProfile -ExecutionPolicy Bypass -File Tools/push.ps1 -Token ghp_xxx
#    powershell -NoProfile -ExecutionPolicy Bypass -File Tools/push.ps1 -Token ghp_xxx -Message "提交说明"
#
#  不带 -Message 时只推送已有提交（不新建提交）。
# ============================================================
param(
    [Parameter(Mandatory = $true)]
    [string]$Token,

    # 传了就先把当前改动提交，再推送
    [string]$Message,

    # 本机代理
    [string]$Proxy = "http://127.0.0.1:17890",

    [string]$Owner = "cauchy418",
    [string]$Repo  = "Finger-Dice-Duel"
)

$ErrorActionPreference = 'Continue'

# 仓库根目录（本脚本在 Tools/ 下）
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$env:GIT_TERMINAL_PROMPT = "0"

# 输出里绝不能带 token
function Hide([string]$text) {
    if ([string]::IsNullOrEmpty($text)) { return "" }
    return ($text -replace [regex]::Escape($Token), '***')
}

# ---------- 1. 可选的提交 ----------
if ($Message) {
    & git add -A 2>&1 | Out-Null

    # ⚠️ 用 `git commit -F <文件>` 而不是 `-m <字符串>`。
    #
    # `-m $Message` 在 PowerShell 里会被**再解析一次**：消息里带双引号时，
    # 引号会改变参数边界，把后续内容吞进消息（实测发生过：
    # 一次带引号的提交信息污染了后面的 $url，导致 push 报
    # "url contains a newline in its host component"）。
    # 写成文件就没有这个问题，且支持多行与任意引号。
    $msgFile = Join-Path ([System.IO.Path]::GetTempPath()) "fdd-commit-msg.txt"
    [System.IO.File]::WriteAllText($msgFile, $Message, (New-Object System.Text.UTF8Encoding($true)))

    $commitOut = Hide ((& git commit -F $msgFile 2>&1) | Out-String)
    $commitCode = $LASTEXITCODE
    Remove-Item $msgFile -ErrorAction SilentlyContinue

    if ($commitCode -ne 0) {
        if ($commitOut -match 'nothing to commit') {
            Write-Host "没有需要提交的改动，继续推送。" -ForegroundColor Yellow
        } else {
            Write-Host "git commit 失败：`n$commitOut" -ForegroundColor Red
            exit 1
        }
    } else {
        Write-Host "已提交。" -ForegroundColor Green
    }
}

# ---------- 2. 推送（openssl 后端 + 代理 + 内嵌凭据）----------
$url = "https://oauth2:$Token@github.com/$Owner/$Repo.git"

Write-Host "正在推送（openssl + 代理 $Proxy）……"

$gitArgs = @(
    '-c', 'credential.helper=',
    '-c', 'http.sslBackend=openssl',
    '-c', "http.proxy=$Proxy",
    'push', $url, 'main:main'
)

# 网络偶发 TLS 中断（`unexpected eof while reading`），必须重试。
# 实测这台机器的代理抖动**相当频繁**（连续 5 次失败出现过），
# 所以重试次数给足，并做递增退避。
#
# ⚠️ 不要用 `(& git ... 2>&1) | Out-String` 抓输出：
#    git 把进度写到 stderr，PowerShell 会包成 NativeCommandError 并标红 ——
#    那**不是**失败（退出码仍是 0），只会让人误判。
#    这里让 git 直接输出到控制台（进度本来就有用）。
$ok = $false
for ($i = 1; $i -le 10; $i++) {
    & git @gitArgs
    if ($LASTEXITCODE -eq 0) { $ok = $true; break }

    $wait = [Math]::Min(2 * $i, 12)      # 2,4,6,8,10,12,12,...
    Write-Host "第 $i 次失败（代理偶发 TLS 中断），${wait}s 后重试……" -ForegroundColor Yellow
    Start-Sleep -Seconds $wait
}

if ($ok) {
    Write-Host "推送成功。" -ForegroundColor Green
    exit 0
}

Write-Host "推送失败（已重试 10 次）。若持续失败，请检查代理是否在运行。" -ForegroundColor Red
exit 1
