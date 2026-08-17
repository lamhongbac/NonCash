#Requires -Version 5.1
<#
.SYNOPSIS
Publishes NonCash.API and NonCash.Web (Release) into artifacts/ for deployment to IIS.
.DESCRIPTION
Produces self-contained-folder publishes (framework-dependent) ready to copy to the server.
Optionally keeps a "previous" copy for rollback when -DeployRoot is supplied.
.PARAMETER DeployRoot
Optional. If set (e.g. C:\inetpub\noncash), the script copies artifacts to $DeployRoot\api and
$DeployRoot\web and preserves the previous version in *_prev folders for rollback.
.PARAMETER SkipPrevious
Skip preserving the previous deployment.
#>
param(
    [string]$DeployRoot = "",
    [switch]$SkipPrevious
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$artifacts = Join-Path $root "artifacts"

Write-Host "==> Publishing NonCash.API ..." -ForegroundColor Cyan
dotnet publish (Join-Path $root "src\NonCash.API\NonCash.API.csproj") -c Release -o (Join-Path $artifacts "api") --nologo
if ($LASTEXITCODE -ne 0) { throw "API publish failed." }

Write-Host "==> Publishing NonCash.Web ..." -ForegroundColor Cyan
dotnet publish (Join-Path $root "src\NonCash.Web\NonCash.Web.csproj") -c Release -o (Join-Path $artifacts "web") --nologo
if ($LASTEXITCODE -ne 0) { throw "Web publish failed." }

Write-Host "==> Publish complete: $artifacts" -ForegroundColor Green

if ($DeployRoot) {
    Write-Host "==> Deploying to $DeployRoot ..." -ForegroundColor Cyan
    foreach ($app in @("api", "web")) {
        $target = Join-Path $DeployRoot $app
        $prev   = Join-Path $DeployRoot "$app`_prev"
        if ((Test-Path $target) -and -not $SkipPrevious) {
            if (Test-Path $prev) { Remove-Item $prev -Recurse -Force }
            Rename-Item $target $prev
        }
        New-Item -ItemType Directory -Path $DeployRoot -Force | Out-Null
        Copy-Item (Join-Path $artifacts $app) $target -Recurse -Force
    }

    Write-Host "==> Recycling app pools ..." -ForegroundColor Cyan
    Import-Module WebAdministration -ErrorAction SilentlyContinue
    foreach ($pool in @("NonCashAPI", "NonCashWeb")) {
        if (Get-Item "IIS:\AppPools\$pool" -ErrorAction SilentlyContinue) {
            Restart-WebAppPool $pool
            Write-Host "   Recycled $pool"
        }
    }
    Write-Host "==> Deploy complete." -ForegroundColor Green
}
