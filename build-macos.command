#!/bin/bash
set -euo pipefail

# Standalone: download this file and run it with bash from the desired build folder.
repository="clcdpc/sip-testing-tool"
source_ref="master"
work_root="$PWD/sip-testing-tool-macos"
run_root=""

fail() {
    printf '\n%s\n' "$*" >&2
    exit 1
}

build_failed() {
    status=$?
    printf '\nBuild failed on line %s. Your previous builds are unchanged.\n' "$1" >&2
    if [ -n "$run_root" ]; then
        printf 'Build files and log: %s\n' "$run_root" >&2
    fi
    exit "$status"
}
trap 'build_failed "$LINENO"' ERR

[ "$(uname -s)" = "Darwin" ] || fail "Run this script on a Mac."
macos_version="$(sw_vers -productVersion)"
macos_major="${macos_version%%.*}"
[[ "$macos_major" =~ ^[0-9]+$ ]] || fail "Could not determine the macOS version."
[ "$macos_major" -ge 14 ] || fail "This .NET 10 build requires macOS 14 or later."

if [ "$(sysctl -n hw.optional.arm64 2>/dev/null || true)" = "1" ]; then
    architecture="arm64"
else
    case "$(uname -m)" in
        arm64) architecture="arm64" ;;
        x86_64) architecture="x64" ;;
        *) fail "This build supports Apple Silicon and Intel Macs." ;;
    esac
fi
runtime="osx-$architecture"
profile="macOS-$architecture"

for tool in curl tar mktemp tee sips iconutil plutil codesign file find; do
    command -v "$tool" >/dev/null 2>&1 || fail "The macOS command '$tool' is unavailable."
done

mkdir -p "$work_root"
run_root="$(mktemp -d "$work_root/build-$(date +%Y%m%d-%H%M%S)-XXXXXX")"
exec > >(tee "$run_root/build.log") 2>&1
printf 'Building SIP Testing Tool for %s\nBuild folder: %s\n' "$runtime" "$run_root"

archive="$run_root/source.tar.gz"
source_root="$run_root/source"
printf '\nDownloading %s (%s)...\n' "$repository" "$source_ref"
curl --fail --location --retry 3 --connect-timeout 30 \
    --output "$archive" "https://codeload.github.com/$repository/tar.gz/refs/heads/$source_ref"
mkdir "$source_root"
tar -xzf "$archive" -C "$source_root" --strip-components 1

project="$source_root/src/sip-testing-tool-avalonia/sip-testing-tool-avalonia.csproj"
macos_assets="$source_root/src/sip-testing-tool-avalonia/Properties/macOS"
if [ ! -f "$project" ] || [ ! -f "$source_root/src/sip-testing-tool-avalonia/Properties/PublishProfiles/$profile.pubxml" ]; then
    fail "The downloaded source does not contain the macOS build yet. Ask the maintainer to publish the current version, then run this script again."
fi

# Keep the SDK, NuGet cache and temporary build files local to this build folder.
export DOTNET_ROOT="$work_root/dotnet-$architecture"
export DOTNET_CLI_HOME="$work_root/dotnet-home"
export NUGET_PACKAGES="$work_root/nuget/packages"
export NUGET_HTTP_CACHE_PATH="$work_root/nuget/http-cache"
export TMPDIR="$work_root/tmp"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
export DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false
export PATH="$DOTNET_ROOT:$PATH"
mkdir -p "$DOTNET_ROOT" "$DOTNET_CLI_HOME" "$NUGET_PACKAGES" "$NUGET_HTTP_CACHE_PATH" "$TMPDIR"

printf '\nPreparing a private .NET 10 SDK...\n'
curl --fail --location --retry 3 --connect-timeout 30 \
    --output "$run_root/dotnet-install.sh" "https://dot.net/v1/dotnet-install.sh"
/bin/bash "$run_root/dotnet-install.sh" --channel 10.0 --quality GA \
    --architecture "$architecture" --install-dir "$DOTNET_ROOT" --no-path
dotnet="$DOTNET_ROOT/dotnet"

printf '\nPublishing the application...\n'
"$dotnet" publish "$project" --configuration Release \
    --artifacts-path "$run_root/build" "-p:PublishProfile=$profile" "-p:UsedAvaloniaProducts="
version="$("$dotnet" msbuild "$project" -nologo -getProperty:Version)"
version="${version%%[-+]*}"
[[ "$version" =~ ^[0-9]+(\.[0-9]+){0,2}$ ]] || fail "The project returned an invalid app version: $version"

publish_root="$source_root/artifacts/avalonia/publish/$runtime"
app="$run_root/SIP Testing Tool.app"
macos_root="$app/Contents/MacOS"
resources="$app/Contents/Resources"
mkdir -p "$macos_root" "$resources"
cp -R "$publish_root/." "$macos_root/"
chmod +x "$macos_root/sip-testing-tool-avalonia"
sed "s/__APP_VERSION__/$version/g" "$macos_assets/Info.plist" > "$app/Contents/Info.plist"
plutil -lint "$app/Contents/Info.plist"

printf '\nCreating the Mac app icon...\n'
iconset="$run_root/app.iconset"
mkdir "$iconset"
for size in 16 32 128 256 512; do
    sips -z "$size" "$size" "$source_root/src/sip-testing-tool-avalonia/Assets/app.png" \
        --out "$iconset/icon_${size}x${size}.png" >/dev/null
    double_size=$((size * 2))
    sips -z "$double_size" "$double_size" "$source_root/src/sip-testing-tool-avalonia/Assets/app.png" \
        --out "$iconset/icon_${size}x${size}@2x.png" >/dev/null
done
iconutil -c icns "$iconset" -o "$resources/app.icns"

printf '\nSigning the local build...\n'
while IFS= read -r -d '' binary; do
    if [[ "$(file -b "$binary")" == *Mach-O* ]]; then
        codesign --force --sign - --timestamp=none "$binary"
    fi
done < <(find "$macos_root" -type f -print0)
codesign --force --sign - --timestamp=none --entitlements "$macos_assets/Entitlements.plist" "$app"
codesign --verify --deep --strict "$app"

printf '%s\n' "$app" > "$work_root/latest-app.txt"
printf '\nBuild complete. Open this app:\n%s\n\nRun this script again to build the latest version.\n' "$app"
if command -v open >/dev/null 2>&1; then
    open -R "$app" || true
fi
