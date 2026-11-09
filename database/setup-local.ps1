<#
.SYNOPSIS
    Creates the KargaBid database on LocalDB, builds the tables and loads dummy data.

.EXAMPLE
    .\database\setup-local.ps1
    .\database\setup-local.ps1 -Recreate          # drop the whole database first
    .\database\setup-local.ps1 -SkipSeed          # tables only

.NOTES
    Requires sqlcmd (ships with SQL Server / Visual Studio; or `winget install Microsoft.Sqlcmd`).
    Seeded users all have password: Password123!
#>
[CmdletBinding()]
param(
    [string]$Server   = '(localdb)\MSSQLLocalDB',
    [string]$Database = 'KargaBid',
    [switch]$Recreate,
    [switch]$SkipSeed
)

$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path

if (-not (Get-Command sqlcmd -ErrorAction SilentlyContinue)) {
    throw "sqlcmd was not found on PATH. Install it with: winget install Microsoft.Sqlcmd"
}

# Make sure the LocalDB instance exists and is running.
if ($Server -like '(localdb)\*' -and (Get-Command sqllocaldb -ErrorAction SilentlyContinue)) {
    $instance = $Server.Substring('(localdb)\'.Length)
    $existing = sqllocaldb info | ForEach-Object { $_.Trim() }
    if ($existing -notcontains $instance) {
        Write-Host "Creating LocalDB instance '$instance'..."
        sqllocaldb create $instance | Out-Null
    }
    sqllocaldb start $instance | Out-Null
}

function Invoke-Sql {
    param([string]$Db, [string]$Query, [string]$File)
    # -b: fail on error, -I: QUOTED_IDENTIFIER ON (needed for the filtered index)
    $sqlArgs = @('-S', $Server, '-d', $Db, '-b', '-I', '-E')
    if ($File) { $sqlArgs += @('-i', $File) } else { $sqlArgs += @('-Q', $Query) }
    & sqlcmd @sqlArgs
    if ($LASTEXITCODE -ne 0) { throw "sqlcmd failed (exit $LASTEXITCODE)." }
}

if ($Recreate) {
    Write-Host "Dropping database [$Database] (if it exists)..."
    Invoke-Sql -Db master -Query @"
IF DB_ID(N'$Database') IS NOT NULL
BEGIN
    ALTER DATABASE [$Database] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [$Database];
END
"@
}

Write-Host "Creating database [$Database] on $Server (if missing)..."
Invoke-Sql -Db master -Query "IF DB_ID(N'$Database') IS NULL CREATE DATABASE [$Database];"

Write-Host "Creating tables..."
Invoke-Sql -Db $Database -File (Join-Path $here 'schema.sql')

if (-not $SkipSeed) {
    Write-Host "Loading dummy data..."
    Invoke-Sql -Db $Database -File (Join-Path $here 'seed.sql')
}

Write-Host ""
Write-Host "Done. Connection string:" -ForegroundColor Green
Write-Host "  Server=$Server;Database=$Database;Trusted_Connection=True;TrustServerCertificate=True;"
if (-not $SkipSeed) { Write-Host "  Test logins: maria.santos@kargabid.test / Password123! (see database\seed.sql for all)" }
