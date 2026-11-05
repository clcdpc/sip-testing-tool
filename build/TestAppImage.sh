#!/usr/bin/env bash
set -euo pipefail
asset=$(realpath "$1")
workspace=$(mktemp -d)
application_pid=
cleanup() {
    if [[ -n "$application_pid" ]]; then
        kill "$application_pid" 2>/dev/null || true
        wait "$application_pid" 2>/dev/null || true
    fi
    rm -rf -- "$workspace"
}
trap cleanup EXIT

chmod +x "$asset"
(cd -- "$workspace" && "$asset" --appimage-extract > /dev/null)
app_dir="$workspace/squashfs-root"
mkdir -p "$workspace/no-dotnet" "$workspace/config"
cat > "$workspace/no-dotnet/dotnet" <<'DOTNET'
#!/bin/sh
echo 'The AppImage must not invoke system dotnet.' >&2
exit 1
DOTNET
chmod +x "$workspace/no-dotnet/dotnet"

# Run the packaged launcher under Xvfb with runtime discovery pointed at an empty
# location. /proc confirms which runtime the native apphost actually loaded.
DOTNET_ROOT="$workspace/no-dotnet" DOTNET_ROOT_X64="$workspace/no-dotnet" \
DOTNET_MULTILEVEL_LOOKUP=0 PATH="$workspace/no-dotnet:$PATH" \
XDG_CONFIG_HOME="$workspace/config" \
    "$app_dir/AppRun" > "$workspace/startup.log" 2>&1 &
application_pid=$!
sleep 5
if ! kill -0 "$application_pid" 2>/dev/null; then
    cat "$workspace/startup.log"
    echo 'The packaged application exited during startup.' >&2
    exit 1
fi
for library in libcoreclr.so libhostpolicy.so; do
    if ! grep -F -- "$app_dir/usr/bin/$library" "/proc/$application_pid/maps" > /dev/null; then
        cat "$workspace/startup.log"
        echo "The application did not load its bundled $library." >&2
        exit 1
    fi
done
echo 'AppImage startup passed using the bundled .NET runtime with system runtime discovery disabled.'
