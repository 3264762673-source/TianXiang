param(
    [string]$GameDir = 'D:\steam\steamapps\common\Slay the Spire 2',
    [ValidateSet('Both','Character','Weather')][string]$Component = 'Character'
)
$ErrorActionPreference = 'Stop'
$gameRoot = [IO.Path]::GetFullPath($GameDir).TrimEnd('\')
$packageRoot = [IO.Path]::GetFullPath($PSScriptRoot).TrimEnd('\')
$modsRoot = Join-Path $gameRoot 'mods'
$backupRoot = Join-Path $gameRoot ('mod-backups\TianXiang-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
function Assert-ChildPath([string]$Path,[string]$Root) {
    $full = [IO.Path]::GetFullPath($Path)
    $prefix = [IO.Path]::GetFullPath($Root).TrimEnd('\') + '\'
    if (-not $full.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)) { throw "Path outside expected directory: $full" }
    return $full
}
if (-not (Test-Path -LiteralPath (Join-Path $gameRoot 'SlayTheSpire2.exe'))) { throw "Game executable not found: $gameRoot" }
if ($packageRoot.Equals($gameRoot,[StringComparison]::OrdinalIgnoreCase)) { throw 'Extract the package into a separate folder first.' }
$running = Get-Process -Name 'SlayTheSpire2' -ErrorAction SilentlyContinue
if ($running) { throw 'Please quit Slay the Spire 2 before installing.' }
$hashFile = Join-Path $packageRoot 'SHA256SUMS.txt'
if (-not (Test-Path -LiteralPath $hashFile)) { throw 'SHA256SUMS.txt is missing. Extract the complete bundle.' }
foreach ($line in Get-Content -LiteralPath $hashFile -Encoding UTF8) {
    if ($line -notmatch '^([a-f0-9]{64})  (.+)$') { throw 'Invalid checksum list.' }
    $expectedHash=$Matches[1]; $relativePath=$Matches[2]
    $payloadFile=Assert-ChildPath (Join-Path $packageRoot $relativePath) $packageRoot
    if ((Get-FileHash -LiteralPath $payloadFile -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expectedHash) { throw "Checksum mismatch: $relativePath" }
}
$versionFile=Join-Path $gameRoot 'release_info.json'
if (-not (Test-Path -LiteralPath $versionFile)) { throw 'Game release_info.json not found.' }
if ((Get-Content -LiteralPath $versionFile -Raw | ConvertFrom-Json).version -ne 'v0.111.0') { throw 'This package requires game v0.111.0.' }
$selected = @()
if ($Component -ne 'Weather') { $selected += 'TianXiang' }
if ($Component -ne 'Character') { $selected += 'CelestialWeather' }
$dependencyManifest = Join-Path $modsRoot 'STS2-RitsuLib\mod_manifest.json'
if (Test-Path -LiteralPath $dependencyManifest) {
    $installedDependency = Get-Content -LiteralPath $dependencyManifest -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($installedDependency.version -notin @('0.6.7','0.6.10')) { throw ('Existing RitsuLib version is ' + $installedDependency.version + '. This bundle is tested with 0.6.10. Keep your other mods compatible before replacing it.') }
}
$selected += 'STS2-RitsuLib'
$backedUp=@(); $copied=@()
New-Item -ItemType Directory -Path $modsRoot -Force | Out-Null
New-Item -ItemType Directory -Path $backupRoot -Force | Out-Null
try {
    foreach ($id in (@('WeatherSage') + $selected)) {
        $target=Assert-ChildPath (Join-Path $modsRoot $id) $modsRoot
        $backup=Assert-ChildPath (Join-Path $backupRoot $id) $backupRoot
        if (Test-Path -LiteralPath $target) {
            if ((Get-Item -LiteralPath $target).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Linked mod directory requires manual handling: $target" }
            Move-Item -LiteralPath $target -Destination $backup
            $backedUp += $id
        }
    }
    foreach ($id in $selected) {
        $source=Assert-ChildPath (Join-Path $packageRoot ('mods\'+$id)) $packageRoot
        $target=Assert-ChildPath (Join-Path $modsRoot $id) $modsRoot
        $copied += $id
        Copy-Item -LiteralPath $source -Destination $target -Recurse
    }
    Write-Host 'Installed. Enable the mods in-game, restart, then begin a NEW run.'
    Write-Host ('Backup: ' + $backupRoot)
    Write-Host 'Weather is optional: Custom Mode > modifiers > Tian Yi Nan Ce.'
} catch {
    foreach ($id in $copied) {
        $target=Assert-ChildPath (Join-Path $modsRoot $id) $modsRoot
        $failed=Assert-ChildPath (Join-Path $backupRoot ('failed-new-'+$id)) $backupRoot
        if (Test-Path -LiteralPath $target) { Move-Item -LiteralPath $target -Destination $failed }
    }
    foreach ($id in $backedUp) {
        $backup=Assert-ChildPath (Join-Path $backupRoot $id) $backupRoot
        $target=Assert-ChildPath (Join-Path $modsRoot $id) $modsRoot
        Move-Item -LiteralPath $backup -Destination $target
    }
    throw
}
