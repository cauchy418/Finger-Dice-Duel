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

    $commitOut = Hide ((& git commit -m $Message 2>&1) | Out-String)
    if ($LASTEXITCODE -ne 0) {
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

# 网络偶发 TLS 中断（unexpected eof），重试若干次即可
$lastOut = ""
for ($i = 1; $i -le 5; $i++) {
    $lastOut = Hide ((& git @gitArgs 2>&1) | Out-String)
    $code = $LASTEXITCODE

    if ($code -eq 0) {
        Write-Host $lastOut.Trim() -ForegroundColor Green
        Write-Host "推送成功。" -ForegroundColor Green
        exit 0
    }

    $brief = ($lastOut -split "`r?`n" | Where-Object { $_ -match 'fatal|error|denied' } | Select-Object -First 1)
    Write-Host "第 $i 次失败：$brief" -ForegroundColor Yellow
    Start-Sleep -Seconds 3
}

Write-Host "推送失败（已重试 5 次）：`n$lastOut" -ForegroundColor Red
exit 1
