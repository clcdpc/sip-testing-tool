#!/bin/bash
set -euo pipefail

# Shared by the standalone Mac bootstrap and GitHub release packaging.
if [ "$#" -ne 3 ]; then
    printf 'Usage: PackageMacOS.sh <publish directory> <new app path> <version>\n' >&2
    exit 1
fi
publish_root="$1"
app="$2"
version="${3%%[-+]*}"
source_root="$(cd "$(dirname "$0")/.." && pwd)"
macos_assets="$source_root/src/sip-testing-tool-avalonia/Properties/macOS"

[ "$(uname -s)" = "Darwin" ] || { printf 'App packaging requires macOS.\n' >&2; exit 1; }
[[ "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || { printf 'Invalid app version: %s\n' "$version" >&2; exit 1; }
[ -f "$publish_root/sip-testing-tool-avalonia" ] || { printf 'The published app is missing.\n' >&2; exit 1; }
[ ! -e "$app" ] && [ ! -L "$app" ] || { printf 'The app destination already exists: %s\n' "$app" >&2; exit 1; }

macos_root="$app/Contents/MacOS"
resources="$app/Contents/Resources"
frameworks="$app/Contents/Frameworks"
mkdir -p "$macos_root" "$resources" "$frameworks"
# Keep managed assemblies and data out of code-only directories. Relative links
# preserve .NET's publish layout while allowing the whole app to be relocated.
shopt -s dotglob nullglob
for entry in "$publish_root"/*; do
    name="${entry##*/}"
    if [ -f "$entry" ] && [[ "$(file -b "$entry")" == *Mach-O* ]]; then
        case "$name" in
            *.dylib) destination="Frameworks" ;;
            *) destination="MacOS" ;;
        esac
    else
        destination="Resources"
    fi
    cp -R "$entry" "$app/Contents/$destination/$name"
    # The .NET host resolves assembly links before looking for its runtime;
    # native libraries also locate helpers relative to their own directory.
    for directory in MacOS Resources Frameworks; do
        if [ "$directory" != "$destination" ]; then
            ln -s "../$destination/$name" "$app/Contents/$directory/$name"
        fi
    done
done
shopt -u dotglob nullglob
chmod +x "$macos_root/sip-testing-tool-avalonia"
sed "s/__APP_VERSION__/$version/g" "$macos_assets/Info.plist" > "$app/Contents/Info.plist"
plutil -lint "$app/Contents/Info.plist"

printf '\nCreating the Mac app icon...\n'
iconset="${app%/*}/app.iconset"
mkdir "$iconset"
for size in 16 32 128 256 512; do
    sips -z "$size" "$size" "$source_root/src/sip-testing-tool-avalonia/Assets/app.png" \
        --out "$iconset/icon_${size}x${size}.png" >/dev/null
    double_size=$((size * 2))
    sips -z "$double_size" "$double_size" "$source_root/src/sip-testing-tool-avalonia/Assets/app.png" \
        --out "$iconset/icon_${size}x${size}@2x.png" >/dev/null
done
iconutil -c icns "$iconset" -o "$resources/app.icns"

printf '\nSigning the app with an ad-hoc signature...\n'
while IFS= read -r -d '' binary; do
    if [[ "$(file -b "$binary")" == *Mach-O* ]]; then
        codesign --force --sign - --timestamp=none "$binary"
    fi
done < <(find "$macos_root" "$frameworks" -type f -print0)
codesign --force --sign - --timestamp=none --entitlements "$macos_assets/Entitlements.plist" "$app"
codesign --verify --deep --strict "$app"
