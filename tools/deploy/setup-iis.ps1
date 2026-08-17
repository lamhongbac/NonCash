#Requires -Version 5.1
<#
.SYNOPSIS
One-time IIS setup for NonCash: app pools, sites, and folder permissions.
.DESCRIPTION
Creates No-Managed-Code app pools and two sites (API + Web) with HTTP bindings.
HTTPS certificate binding must be added afterwards (IIS Manager) using your cert.
Run as Administrator.
.PARAMETER ApiPath   Physical path for the API site.
.PARAMETER WebPath   Physical path for the Web site.
.PARAMETER ApiPort   HTTP port for the API site (default 8001).
.PARAMETER WebPort   HTTP port for the Web site (default 80).
#>
param(
    [string]$ApiPath = "C:\inetpub\noncash\api",
    [string]$WebPath = "C:\inetpub\noncash\web",
    [int]$ApiPort = 8001,
    [int]$WebPort = 80
)

$ErrorActionPreference = "Stop"
if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Run this script as Administrator."
}

Import-Module WebAdministration

function Ensure-AppPool([string]$name) {
    if (-not (Get-Item "IIS:\AppPools\$name" -ErrorAction SilentlyContinue)) {
        New-WebAppPool -Name $name | Out-Null
        Write-Host "Created app pool $name"
    }
    Set-ItemProperty "IIS:\AppPools\$name" -Name managedRuntimeVersion -Value ""   # No Managed Code
}

function Ensure-Site([string]$name, [string]$pool, [string]$path, [int]$port) {
    New-Item -ItemType Directory -Path $path -Force | Out-Null
    if (-not (Get-Item "IIS:\Sites\$name" -ErrorAction SilentlyContinue)) {
        New-Website -Name $name -ApplicationPool $pool -PhysicalPath $path -Port $port -Force | Out-Null
        Write-Host "Created site $name (:$port -> $path)"
    }
    # Read & Execute for the app pool identity
    $acl = Get-Acl $path
    $identity = "IIS AppPool\$pool"
    $rule = New-Object System.Security.AccessControl.FileSystemAccessRule(
        $identity, "ReadAndExecute", "ContainerInherit,ObjectInherit", "None", "Allow")
    $acl.SetAccessRule($rule)
    Set-Acl -Path $path -AclObject $acl
}

Ensure-AppPool "NonCashAPI"
Ensure-AppPool "NonCashWeb"

Ensure-Site "NonCash.API" "NonCashAPI" $ApiPath $ApiPort
Ensure-Site "NonCash.Web" "NonCashWeb" $WebPath $WebPort

Write-Host @"

IIS base setup complete.
NEXT STEPS (manual):
 1. Bind your HTTPS certificate to each site (IIS Manager -> Bindings -> https :443).
 2. Set per-site environment variables in each web.config <aspNetCore> section:
      API: Environment__Name=production, ConnectionStrings__ProductionConnection=..., Jwt__Key=..., Smtp__*=...
      Web: Environment__Name=production, ApiBaseUrls__production=https://api.yourdomain.com/
 3. Deploy the published apps into $ApiPath and $WebPath (see publish.ps1).
"@ -ForegroundColor Yellow
