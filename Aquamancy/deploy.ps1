# Déploie Aquamancy sur le Raspberry Pi
# Prérequis : une clé SSH autorisée sur le Raspberry (connexion sans mot de passe)
# Usage : .\deploy.ps1   (ou .\deploy.ps1 -RemoteHost 192.168.1.60 si l'IP change)
param(
    [string]$RemoteHost = "192.168.1.54",
    [string]$User = "admin",
    [string]$RemoteDir = "/home/admin/Desktop/publish",
    [string]$Service = "aquamancy"
)

$ErrorActionPreference = "Stop"
$project = Join-Path $PSScriptRoot "Aquamancy"
$publishDir = Join-Path $project "bin\Release\net10.0\linux-arm64\publish"
$archive = Join-Path $env:TEMP "aquamancy-publish.tar"
$target = "$User@$RemoteHost"

function Assert-LastExitCode([string]$step) {
    if ($LASTEXITCODE -ne 0) { throw "Échec : $step (code $LASTEXITCODE)" }
}

Write-Host "==> Publication (linux-arm64, self-contained)" -ForegroundColor Cyan
dotnet publish $project -p:PublishProfile=FolderProfile -c Release
Assert-LastExitCode "dotnet publish"

# appsettings.json et Firmwares sont propres au Raspberry (secrets, firmwares envoyés depuis l'app) : on ne les écrase pas
Write-Host "==> Création de l'archive" -ForegroundColor Cyan
if (Test-Path $archive) { Remove-Item $archive -Force }
tar -cf $archive -C $publishDir --exclude=./appsettings.json --exclude=./Firmwares .
Assert-LastExitCode "tar"

Write-Host "==> Envoi vers $target" -ForegroundColor Cyan
scp $archive "${target}:/tmp/aquamancy-publish.tar"
Assert-LastExitCode "scp"

# L'ancienne version est gardée dans <RemoteDir>_previous pour pouvoir revenir en arrière
Write-Host "==> Installation et redémarrage du service" -ForegroundColor Cyan
$remoteScript = @"
set -e
rm -rf '${RemoteDir}_previous'
cp -a '$RemoteDir' '${RemoteDir}_previous'
sudo systemctl stop $Service
tar -xf /tmp/aquamancy-publish.tar -C '$RemoteDir'
chmod +x '$RemoteDir/Aquamancy'
rm -f /tmp/aquamancy-publish.tar
sudo systemctl start $Service
for i in `$(seq 1 15); do
  code=`$(curl -s -o /dev/null -w '%{http_code}' http://localhost:5000/ || true)
  if [ "`$code" = "200" ]; then echo "Application en ligne (HTTP 200)"; exit 0; fi
  sleep 2
done
echo "L'application ne répond pas, voir : journalctl -u $Service -n 50"
exit 1
"@
# Envoyé en base64 : PowerShell 5.1 ajoute un BOM quand il écrit sur l'entrée d'un programme natif
$encoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes(($remoteScript -replace "`r", "")))
ssh $target "echo $encoded | base64 -d | bash"
Assert-LastExitCode "installation sur le Raspberry"

Remove-Item $archive -Force
Write-Host "==> Déploiement terminé : http://${RemoteHost}:5000" -ForegroundColor Green
Write-Host "    Retour arrière : ssh $target `"sudo systemctl stop $Service && rm -rf $RemoteDir && mv ${RemoteDir}_previous $RemoteDir && sudo systemctl start $Service`""
