#!/usr/bin/env bash
set -euo pipefail
root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$root"
action="${1:-}"
case "$action" in
    clean) rm -rf -- bin src/obj tests/bin tests/obj tools/ConfigChecks/bin tools/ConfigChecks/obj; exit ;;
    install|package) ;;
    *) echo 'Usage: bash tools/artifacts.sh install|package|clean' >&2; exit 1 ;;
esac
dotnet="${DOTNET:-dotnet}"
version="$("$dotnet" msbuild src/Hoba.csproj -nologo -getProperty:Version)"
[[ "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || { echo 'Invalid project version' >&2; exit 1; }
(cd dependencies && sha256sum --check SHA256SUMS)
files=(bin/Nivalis.Hoba.dll dependencies/Nivalis.ModCompanion.dll)
for file in "${files[@]}"; do test -f "$file"; done
if [[ "$action" == install ]]; then
    test -f "${GAME_PATH:?Set GAME_PATH}/BepInEx/interop/Assembly-CSharp.dll"
    for file in "${files[@]}"; do
        install -Dm644 "$file" "$GAME_PATH/BepInEx/plugins/$(basename "$file")"
    done
    echo 'Installed HOBA and Mod Companion. Restart the game.'
    exit
fi
mkdir -p bin
staging="$(mktemp -d "$root/bin/.package.XXXXXXXX")"
trap 'rm -rf -- "$staging"' EXIT
mkdir -p "$staging/files/BepInEx/plugins"
cp -- "${files[@]}" "$staging/files/BepInEx/plugins/"
"$dotnet" msbuild tools/Package.proj -nologo -target:Package \
    "-p:PackageSource=$staging/files" "-p:PackageArchive=$staging/archive.zip"
mv -- "$staging/archive.zip" "bin/Nivalis.Hoba-$version.zip"
(cd bin && sha256sum "Nivalis.Hoba-$version.zip" > "Nivalis.Hoba-$version.zip.sha256")
echo "Package: bin/Nivalis.Hoba-$version.zip"
