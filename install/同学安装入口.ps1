$ErrorActionPreference='Stop'
try {
    Write-Host 'TianXiang 0.5.3 / CelestialWeather 0.1.0' -ForegroundColor Cyan
    Write-Host 'Tested game version: 0.111.0. Please quit the game before installing.'
    $candidates = [Collections.Generic.List[string]]::new()
    $steamPath=(Get-ItemProperty 'HKCU:\Software\Valve\Steam' -ErrorAction SilentlyContinue).SteamPath
    if ($steamPath) {
        $candidates.Add((Join-Path $steamPath 'steamapps\common\Slay the Spire 2'))
        $libraryFile=Join-Path $steamPath 'steamapps\libraryfolders.vdf'
        if(Test-Path -LiteralPath $libraryFile) {
            foreach($match in [regex]::Matches((Get-Content -LiteralPath $libraryFile -Raw),'"path"\s+"([^"]+)"')) {
                $libraryPath=$match.Groups[1].Value.Replace('\\','\')
                $candidates.Add((Join-Path $libraryPath 'steamapps\common\Slay the Spire 2'))
            }
        }
    }
    $found=@($candidates | Select-Object -Unique | Where-Object {Test-Path -LiteralPath (Join-Path $_ 'SlayTheSpire2.exe')})
    if($found.Count -eq 1){$chosenGame=$found[0];Write-Host ('Detected: '+$chosenGame)}
    else{$chosenGame=(Read-Host 'Paste game folder (contains SlayTheSpire2.exe)').Trim().Trim('"')}
    if(-not (Test-Path -LiteralPath (Join-Path $chosenGame 'SlayTheSpire2.exe'))){throw 'Game executable not found in that folder.'}
    Write-Host '1 = Character only (default); 2 = Character + optional weather challenge'
    $choice=Read-Host 'Choose 1 or 2'
    $selectedComponent=if($choice -eq '2'){'Both'}else{'Character'}
    & (Join-Path $PSScriptRoot '安装新版.ps1') -GameDir $chosenGame -Component $selectedComponent
    Write-Host 'Installed. Enable TianXiang and STS2-RitsuLib in game, restart, start a NEW run.' -ForegroundColor Green
} catch {Write-Host $_.Exception.Message -ForegroundColor Red;exit 1}
