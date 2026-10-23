#!/usr/bin/env bash
set -euo pipefail
publish_dir=$(realpath "$1")
output_file=$(realpath -m "$2")
version=$3
repo_dir=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)
tool_dir="$repo_dir/artifacts/tools/appimage"
mkdir -p "$tool_dir"

# Pin both the packager and native AppImage launcher; neither contains the .NET runtime.
download_verified() {
    local url=$1 file=$2 expected=$3
    if [[ ! -f "$file" ]]; then
        curl --fail --location --retry 3 --output "$file" "$url"
    fi
    printf '%s  %s\n' "$expected" "$file" | sha256sum --check --status
}
tool="$tool_dir/appimagetool-1.9.1-x86_64.AppImage"
runtime="$tool_dir/runtime-20251108-x86_64"
download_verified 'https://github.com/AppImage/appimagetool/releases/download/1.9.1/appimagetool-x86_64.AppImage' "$tool" 'ed4ce84f0d9caff66f50bcca6ff6f35aae54ce8135408b3fa33abfc3cb384eb0'
download_verified 'https://github.com/AppImage/type2-runtime/releases/download/20251108/runtime-x86_64' "$runtime" '2fca8b443c92510f1483a883f60061ad09b46b978b2631c807cd873a47ec260d'
chmod +x "$tool"

workspace=$(mktemp -d "$repo_dir/artifacts/avalonia/appimage.XXXXXXXX")
trap 'rm -rf -- "$workspace"' EXIT
app_dir="$workspace/SipTestingTool.AppDir"
mkdir -p "$app_dir/usr/bin"
cp -a -- "$publish_dir/." "$app_dir/usr/bin/"
cp -- "$repo_dir/src/sip-testing-tool-avalonia/Assets/app.png" "$app_dir/sip-testing-tool.png"
cat > "$app_dir/AppRun" <<'APPRUN'
#!/bin/sh
app_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
exec "$app_dir/usr/bin/sip-testing-tool-avalonia" "$@"
APPRUN
chmod +x "$app_dir/AppRun" "$app_dir/usr/bin/sip-testing-tool-avalonia"
cat > "$app_dir/sip-testing-tool.desktop" <<DESKTOP
[Desktop Entry]
Type=Application
Name=SIP Testing Tool
Exec=sip-testing-tool-avalonia
Icon=sip-testing-tool
Categories=Utility;Development;
Terminal=false
X-AppImage-Version=$version
DESKTOP
# Extraction mode works without FUSE on build runners; the resulting runtime also supports it.
ARCH=x86_64 VERSION="$version" APPIMAGE_EXTRACT_AND_RUN=1 "$tool" --runtime-file "$runtime" --no-appstream "$app_dir" "$output_file"
chmod +x "$output_file"
