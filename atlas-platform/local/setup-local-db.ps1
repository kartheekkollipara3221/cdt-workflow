# LOCAL ONLY: makes the local review database (spec 12, 11.3). Safe to run again.
# Needs: SQL Server with Windows authentication, sqlcmd on the PATH.
#   powershell -ExecutionPolicy Bypass -File atlas-platform/local/setup-local-db.ps1
#   powershell -ExecutionPolicy Bypass -File atlas-platform/local/setup-local-db.ps1 -Server ".\SQLEXPRESS" -Recreate
#   -SkipEngine leaves out the engine tables (they need the dotnet ef tool and a build of the engine).
param(
    [string]$Server = "localhost",
    [string]$Database = "CPAtlasPlatformDev",
    [switch]$Recreate,
    [switch]$SkipEngine
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot             # atlas-platform/
$release = Join-Path $root "sql\release-p1"
$local = Join-Path $PSScriptRoot "sql"
$lanId = $env:USERNAME
$displayName = $env:USERNAME

function Invoke-Sql([string]$File, [string]$Db) {
    Write-Host "  $([IO.Path]::GetFileName($File))"
    $arguments = @("-S", $Server, "-E", "-C", "-b", "-I", "-d", $Db, "-i", $File)
    $output = & sqlcmd @arguments
    if ($LASTEXITCODE -ne 0) { $output | Write-Host; throw "sqlcmd failed on $File (exit $LASTEXITCODE)." }
}

Write-Host "Database $Database on $Server"
if ($Recreate) {
    & sqlcmd -S $Server -E -C -b -Q "IF DB_ID(N'$Database') IS NOT NULL BEGIN ALTER DATABASE [$Database] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$Database]; END"
    if ($LASTEXITCODE -ne 0) { throw "Could not drop $Database." }
}
& sqlcmd -S $Server -E -C -b -Q "IF DB_ID(N'$Database') IS NULL CREATE DATABASE [$Database];"
if ($LASTEXITCODE -ne 0) { throw "Could not create $Database." }

Write-Host "Core tables (local copy of the target dbo.CP_Atlas_* tables)"
Invoke-Sql (Join-Path $local "000-cp-atlas-core-tables.sql") $Database

Write-Host "Release P1 scripts 020-045"
foreach ($name in @("020-script-log.sql", "030-active-user-roles-view.sql", "040-ttdv2-app.sql", "045-ttdv2-audit-reference-columns.sql")) {
    $source = Join-Path $release $name
    $temp = Join-Path ([IO.Path]::GetTempPath()) ("atlas-" + $name)
    (Get-Content -Raw $source).Replace("<RUN_BY>", $lanId) | Set-Content -Encoding utf8 $temp
    Invoke-Sql $temp $Database
    Remove-Item $temp
}

Write-Host "Target tables of release P2 (local copy: CFP.Inflow, CFP.InflowAttachment, AIN.AccountInventory)"
Invoke-Sql (Join-Path $local "020-local-cfp-ain-tables.sql") $Database

Write-Host "Release P2 scripts 100-140"
$releaseP2 = Join-Path $root "sql\release-p2"
foreach ($name in @("100-cfp-inflow-columns.sql", "110-cfp-inflow-attachment-columns.sql", "120-cfp-audit-log.sql", "130-cfpinflow-app.sql", "140-ain-account-inventory-rmcc.sql")) {
    $source = Join-Path $releaseP2 $name
    $temp = Join-Path ([IO.Path]::GetTempPath()) ("atlas-" + $name)
    (Get-Content -Raw $source).Replace("<RUN_BY>", $lanId) | Set-Content -Encoding utf8 $temp
    Invoke-Sql $temp $Database
    Remove-Item $temp
}

if (-not $SkipEngine) {
    # The EF tool applies each engine migration that the database does not have. It adds no sample workflow.
    # Not the idempotent script: on an empty database, one of its batches uses a column before it exists.
    Write-Host "Engine tables (engine EF migrations)"
    $repo = Split-Path -Parent $root
    $connection = "Server=$Server;Database=$Database;Integrated Security=true;TrustServerCertificate=True;Encrypt=False"
    Push-Location $repo
    try {
        & dotnet tool restore | Out-Null
        & dotnet ef database update --project src/WorkflowEngine.Infrastructure --startup-project src/WorkflowEngine.Console --connection $connection
        if ($LASTEXITCODE -ne 0) { throw "dotnet ef database update failed (exit $LASTEXITCODE)." }
    }
    finally { Pop-Location }
}

Write-Host "Local seed (users, roles, Get_Application, sample time sheets)"
# Text replacement, not sqlcmd -v: a Windows login can contain a space.
$seed = Join-Path ([IO.Path]::GetTempPath()) "atlas-900-local-seed.sql"
(Get-Content -Raw (Join-Path $local "900-local-seed.sql")).Replace('$(CurrentLanId)', $lanId.Replace("'", "''")).Replace('$(CurrentDisplayName)', $displayName.Replace("'", "''")) |
    Set-Content -Encoding utf8 $seed
Invoke-Sql $seed $Database
Remove-Item $seed

Write-Host "Done. Current user '$lanId' has the TTDV2 role Owner and the CFPINFLOW role Write."
