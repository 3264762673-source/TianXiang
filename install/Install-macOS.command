#!/bin/bash
set -eu
bundle=$(cd "$(dirname "$0")" && pwd -P)
steam="$HOME/Library/Application Support/Steam/steamapps"
game="${1:-$steam/common/Slay the Spire 2}"
if [ -d "$game" ] && [[ "$game" != *.app ]]; then
  for app in "$game"/*.app; do
    if [ -f "$app/Contents/MacOS/SlayTheSpire2" ]; then game=$app; break; fi
  done
fi
if [ ! -f "$game/Contents/MacOS/SlayTheSpire2" ]; then
  printf '请把 Steam 安装目录中的 SlayTheSpire2.app 拖到终端，作为脚本的第一个参数。\n' >&2
  exit 1
fi
app=$(cd "$game" && pwd -P)
game_root=$(dirname "$app")
if pgrep -x SlayTheSpire2 >/dev/null 2>&1; then echo '请先退出杀戮尖塔 2。'; exit 1; fi
version_ok=0
for info in "$game_root/release_info.json" "$app/Contents/Resources/release_info.json" "$app/Contents/MacOS/release_info.json"; do
  [ -f "$info" ] || continue
  grep -Eq '"version"[[:space:]]*:[[:space:]]*"v0\.111\.0"' "$info" || { echo '需要游戏 v0.111.0'; exit 1; }
  version_ok=1
done
if [ "$version_ok" = 0 ]; then
  manifest="$game_root/../../appmanifest_2868840.acf"
  grep -Eq '"buildid"[[:space:]]*"24724944"' "$manifest" || { echo '无法核实 0.111.0 游戏版本，请检查所选 Steam 库。'; exit 1; }
fi
cd "$bundle"
if command -v shasum >/dev/null; then shasum -a 256 -c SHA256SUMS.txt >/dev/null; else sha256sum -c SHA256SUMS.txt >/dev/null; fi
mods="$app/Contents/MacOS/mods"
[ ! -L "$mods" ] || { echo 'mods 是符号链接，请手动安装。'; exit 1; }
mkdir -p "$mods"
ids=(STS2-RitsuLib TianXiang)
case "${2:-}" in --weather) ids+=(CelestialWeather);; '') ;; *) echo '第二个参数仅支持 --weather'; exit 1;; esac
for id in "${ids[@]}" WeatherSage; do
  [ ! -L "$mods/$id" ] || { echo "$id 是符号链接，请手动安装。"; exit 1; }
done
dep="$mods/STS2-RitsuLib/mod_manifest.json"
if [ -d "$mods/STS2-RitsuLib" ]; then
  grep -Eq '"version"[[:space:]]*:[[:space:]]*"0\.6\.(7|10)"' "$dep" || { echo '现有前置版本不同，请手动检查。'; exit 1; }
fi
backup=$(mktemp -d "$game_root/TianXiang-backup.XXXXXX")
stage="$backup/staging"
mkdir "$stage"
installed=()
restore() {
  code=$?
  if [ "$code" != 0 ]; then
    for id in "${installed[@]}"; do mv "$mods/$id" "$stage/failed-$id"; done
    for id in "${ids[@]}" WeatherSage; do [ ! -d "$backup/$id" ] || mv "$backup/$id" "$mods/$id"; done
    echo "安装未完成，旧文件已退回。备份目录 $backup" >&2
  fi
}
trap restore EXIT
for id in "${ids[@]}"; do cp -R "$bundle/mods/$id" "$stage/$id"; done
for id in "${ids[@]}" WeatherSage; do
  if [ -e "$mods/$id" ]; then mv "$mods/$id" "$backup/$id"; fi
done
for id in "${ids[@]}"; do mv "$stage/$id" "$mods/$id"; installed+=("$id"); done
rmdir "$stage"
echo "已安装天相者 v0.6.0。请从 Steam 启动，在模组列表中启用 TianXiang 和 STS2-RitsuLib。备份在 $backup"

