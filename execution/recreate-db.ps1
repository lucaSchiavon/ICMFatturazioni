<#
.SYNOPSIS
    Ricrea il database di sviluppo ICMFatturazioni applicando in ordine tutte
    le migration in src/ICMFatturazioni.Web/Migrations/.

.DESCRIPTION
    - Senza -Drop: crea il database se non esiste e applica le migration
      (idempotenti: IF OBJECT_ID/IF NOT EXISTS, quindi rieseguibili a vuoto).
    - Con -Drop: prima forza la disconnessione (SINGLE_USER WITH ROLLBACK
      IMMEDIATE) ed elimina il database, poi lo ricrea da zero.

    I "dati di configurazione" (le 5 lookup ministeriali: Paesi, Province,
    NatureIVA, CondizioniPagamento, ModalitaPagamento) sono nel seed della
    migration 004 e si ripopolano automaticamente.

    NB: l'utente di sviluppo per il login NON è una migration — lo crea l'app
    all'avvio (SeedUtenteSviluppoAsync). Dopo la ricreazione: `dotnet run`.

.PARAMETER Server
    Istanza SQL Server. Default: .\SQLEXPRESS

.PARAMETER Database
    Nome database. Default: ICMFatturazioni

.PARAMETER Drop
    Se presente, elimina il database esistente prima di ricrearlo (DISTRUTTIVO).

.EXAMPLE
    # Applica le migration sul DB esistente (non distruttivo)
    pwsh execution/recreate-db.ps1

.EXAMPLE
    # Ricreazione completa da zero (drop + create + migrate)
    pwsh execution/recreate-db.ps1 -Drop
#>
[CmdletBinding()]
param(
    [string] $Server   = '.\SQLEXPRESS',
    [string] $Database  = 'ICMFatturazioni',
    [switch] $Drop
)

$ErrorActionPreference = 'Stop'

# Cartella delle migration, relativa alla posizione di questo script.
$migrationsDir = Join-Path $PSScriptRoot '..\src\ICMFatturazioni.Web\Migrations'
$migrationsDir = (Resolve-Path $migrationsDir).Path

Write-Host "Server   : $Server"
Write-Host "Database : $Database"
Write-Host "Migrations: $migrationsDir"
Write-Host ''

# Helper: esegue uno statement sul database 'master'.
function Invoke-Master([string] $sql) {
    Invoke-Sqlcmd -ServerInstance $Server -Database 'master' -Query $sql `
        -QueryTimeout 120 -ErrorAction Stop
}

if ($Drop) {
    Write-Host "[DROP] Eliminazione del database $Database (se esiste)..." -ForegroundColor Yellow
    Invoke-Master @"
IF DB_ID(N'$Database') IS NOT NULL
BEGIN
    ALTER DATABASE [$Database] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [$Database];
END
"@
    Write-Host "[DROP] Fatto." -ForegroundColor Yellow
}

# Crea il database se non esiste.
Write-Host "[CREATE] Creazione del database $Database (se non esiste)..."
Invoke-Master "IF DB_ID(N'$Database') IS NULL CREATE DATABASE [$Database];"

# Applica le migration in ordine numerico (001, 002, ...).
$files = Get-ChildItem -Path $migrationsDir -Filter '*.sql' | Sort-Object Name
if (-not $files) { throw "Nessuna migration trovata in $migrationsDir" }

foreach ($f in $files) {
    Write-Host "[MIGRATE] $($f.Name) ..."
    # -I = QUOTED_IDENTIFIER ON (richiesto dagli indici filtrati della 005).
    Invoke-Sqlcmd -ServerInstance $Server -Database $Database -InputFile $f.FullName `
        -QueryTimeout 300 -ErrorAction Stop
}

Write-Host ''
Write-Host "OK - database $Database ricreato e migrato ($($files.Count) migration)." -ForegroundColor Green
Write-Host "Ora avvia l'app (dotnet run) per il seed dell'utente di sviluppo." -ForegroundColor Green
