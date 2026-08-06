param(
    [Parameter(Mandatory = $true)]
    [string]$ConnectionString,

    [Parameter(Mandatory = $true)]
    [ValidateSet('RESET-MISSIONS')]
    [string]$Confirmation
)

$ErrorActionPreference = 'Stop'
$scriptPath = Join-Path $PSScriptRoot 'clean-mission-data.sql'
if (-not (Test-Path -LiteralPath $scriptPath -PathType Leaf)) {
    throw "Le script SQL de nettoyage est introuvable : $scriptPath"
}

$psql = Get-Command psql -ErrorAction SilentlyContinue
if ($null -eq $psql) {
    throw "PostgreSQL psql est requis pour exécuter ce nettoyage."
}

Write-Host "Nettoyage ciblé des missions en cours..."
& $psql.Source $ConnectionString --set ON_ERROR_STOP=on --file $scriptPath
if ($LASTEXITCODE -ne 0) {
    throw "Le nettoyage a échoué. La transaction a été annulée."
}

Write-Host "Nettoyage terminé : missions, affectations, offres et conversations sont vides."
