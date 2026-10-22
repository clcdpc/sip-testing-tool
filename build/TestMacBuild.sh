#!/bin/bash
# Exercise the standalone bootstrap without network access, an SDK or macOS tools.
set -euo pipefail

# Git Bash otherwise copies symlink targets, hiding the bundle layout being tested.
case "$(uname -s)" in
    MINGW*|MSYS*)
        if [[ "${MSYS:-}" != *winsymlinks* ]]; then
            export MSYS="${MSYS:+$MSYS }winsymlinks"
            exec /bin/bash "$0" "$@"
        fi ;;
esac

repo_root="$(cd "$(dirname "$0")/.." && pwd)"
mkdir -p "$repo_root/artifacts"
test_root="$(mktemp -d "$repo_root/artifacts/mac build tests-XXXXXX")"
trap 'printf "Mac bootstrap check failed on line %s. Diagnostics: %s\n" "$LINENO" "$test_root" >&2' ERR
mock_bin="$test_root/bin"
fixture="$test_root/fixture/repository"
mkdir -p "$mock_bin" "$fixture/src/sip-testing-tool-avalonia/Properties/PublishProfiles" \
    "$fixture/src/sip-testing-tool-avalonia/Assets"
cp "$repo_root/src/sip-testing-tool-avalonia/sip-testing-tool-avalonia.csproj" \
    "$fixture/src/sip-testing-tool-avalonia/"
cp "$repo_root"/src/sip-testing-tool-avalonia/Properties/PublishProfiles/macOS-*.pubxml \
    "$fixture/src/sip-testing-tool-avalonia/Properties/PublishProfiles/"
cp -R "$repo_root/src/sip-testing-tool-avalonia/Properties/macOS" \
    "$fixture/src/sip-testing-tool-avalonia/Properties/"
cp "$repo_root/src/sip-testing-tool-avalonia/Assets/app.png" \
    "$fixture/src/sip-testing-tool-avalonia/Assets/"
tar -czf "$test_root/source.tar.gz" -C "$test_root/fixture" repository
export MOCK_SOURCE_ARCHIVE="$test_root/source.tar.gz"

cat > "$mock_bin/dispatcher" <<'MOCK'
#!/bin/bash
set -euo pipefail
tool="${0##*/}"
case "$tool" in
    uname)
        if [ "$1" = "-s" ]; then printf '%s\n' "${MOCK_OS:-Darwin}";
        else printf '%s\n' "${MOCK_CPU:-arm64}"; fi ;;
    sw_vers) printf '%s\n' "${MOCK_MACOS:-14.0}" ;;
    sysctl) printf '%s\n' "${MOCK_ARM_HARDWARE:-1}" ;;
    curl)
        destination=""; url=""
        while [ "$#" -gt 0 ]; do
            case "$1" in
                --output) destination="$2"; shift 2 ;;
                --retry|--connect-timeout) shift 2 ;;
                --fail|--location) shift ;;
                *) url="$1"; shift ;;
            esac
        done
        case "$url" in
            https://codeload.github.com/clcdpc/sip-testing-tool/tar.gz/refs/heads/master)
                [ "${MOCK_DOWNLOAD_FAIL:-0}" = "0" ] || exit 22
                cp "$MOCK_SOURCE_ARCHIVE" "$destination" ;;
            https://dot.net/v1/dotnet-install.sh) cp "$0" "$destination" ;;
            *) printf 'Unexpected download: %s\n' "$url" >&2; exit 90 ;;
        esac ;;
    dotnet-install.sh)
        [ "$*" = "--channel 10.0 --quality GA --architecture $MOCK_ARCH --install-dir $MOCK_WORK_ROOT/dotnet-$MOCK_ARCH --no-path" ]
        cp "$0" "$DOTNET_ROOT/dotnet"
        chmod +x "$DOTNET_ROOT/dotnet" ;;
    dotnet)
        [ "$0" = "$MOCK_WORK_ROOT/dotnet-$MOCK_ARCH/dotnet" ]
        [ "$DOTNET_CLI_HOME" = "$MOCK_WORK_ROOT/dotnet-home" ]
        [ "$NUGET_PACKAGES" = "$MOCK_WORK_ROOT/nuget/packages" ]
        [ "$NUGET_HTTP_CACHE_PATH" = "$MOCK_WORK_ROOT/nuget/http-cache" ]
        [ "$TMPDIR" = "$MOCK_WORK_ROOT/tmp" ]
        [ "$DOTNET_GENERATE_ASPNET_CERTIFICATE" = "false" ]
        [ "$DOTNET_ADD_GLOBAL_TOOLS_TO_PATH" = "false" ]
        case "$1" in
            publish)
                project="$2"
                [[ "$*" == *"--configuration Release --artifacts-path "*" -p:PublishProfile=macOS-$MOCK_ARCH -p:UsedAvaloniaProducts=" ]]
                source_root="${project%/src/*}"
                output="$source_root/artifacts/avalonia/publish/osx-$MOCK_ARCH"
                mkdir -p "$output/zh-Hans"
                printf '#!/bin/bash\nexit 0\n' > "$output/sip-testing-tool-avalonia"
                printf '#!/bin/bash\nexit 0\n' > "$output/createdump"
                chmod +x "$output/createdump"
                printf 'native\n' > "$output/libcoreclr.dylib"
                printf 'native\n' > "$output/libhostfxr.dylib"
                printf 'native\n' > "$output/libhostpolicy.dylib"
                printf 'managed\n' > "$output/sip-testing-tool-avalonia.dll"
                printf 'framework\n' > "$output/System.Xml.Linq.dll"
                printf 'core library\n' > "$output/System.Private.CoreLib.dll"
                printf '{}\n' > "$output/sip-testing-tool-avalonia.runtimeconfig.json"
                printf '{}\n' > "$output/sip-testing-tool-avalonia.deps.json"
                printf 'satellite\n' > "$output/zh-Hans/Avalonia.resources.dll"
                printf 'hidden resource\n' > "$output/.app-data" ;;
            msbuild) printf '2.1.1-beta.1\n' ;;
            *) exit 91 ;;
        esac ;;
    sips)
        [ "$5" = "--out" ]
        cp "$4" "$6" ;;
    iconutil)
        [ "$1" = "-c" ] && [ "$2" = "icns" ] && [ "$4" = "-o" ]
        [ -f "$3/icon_512x512@2x.png" ]
        printf 'icon\n' > "$5" ;;
    plutil)
        [ "$1" = "-lint" ]
        [ "$(sed -n 's/.*<string>\(2\.1\.1\)<\/string>.*/\1/p' "$2" | wc -l | tr -d ' ')" = "2" ]
        ! grep -q '__APP_VERSION__' "$2" ;;
    file)
        case "$2" in
            */sip-testing-tool-avalonia|*/createdump|*.dylib) printf 'Mach-O executable\n' ;;
            *) printf 'data\n' ;;
        esac ;;
    codesign)
        [ "${MOCK_SIGN_FAIL:-0}" = "0" ] || exit 33
        target="${!#}"
        if [[ "$target" == *.app ]]; then
            # Managed DLLs are resources; only native code may be a real file here.
            shopt -s dotglob nullglob
            for entry in "$target/Contents/MacOS"/* "$target/Contents/Frameworks"/*; do
                if [ -L "$entry" ]; then
                    [ -e "$entry" ]
                    case "$(readlink "$entry")" in
                        ../Resources/*|../Frameworks/*|../MacOS/*) ;;
                        *) printf 'Invalid bundle link: %s\n' "$entry" >&2; exit 94 ;;
                    esac
                elif [ ! -f "$entry" ] || [[ "$(file -b "$entry")" != *Mach-O* ]] \
                    || [[ "$entry" == */MacOS/*.dylib ]] \
                    || { [[ "$entry" == */Frameworks/* ]] && [[ "$entry" != *.dylib ]]; }; then
                    printf 'Unsigned or misplaced bundle entry: %s\n' "$entry" >&2
                    exit 95
                fi
            done
        fi
        printf '%s\n' "$*" >> "$MOCK_WORK_ROOT/signing.log"
        if [ "$1" = "--verify" ]; then
            [ "$2" = "--deep" ] && [ "$3" = "--strict" ]
        else
            [[ "$*" != *--deep* ]]
            [ "$1" = "--force" ] && [ "$2" = "--sign" ] && [ "$3" = "-" ]
        fi ;;
    open) [ "$1" = "-R" ] && [ -d "$2" ] ;;
    git|brew|sudo|xcode-select) printf 'Unexpected prerequisite: %s\n' "$tool" >&2; exit 92 ;;
    *) exit 93 ;;
esac
MOCK
chmod +x "$mock_bin/dispatcher"
for tool in uname sw_vers sysctl curl sips iconutil plutil file codesign open git brew sudo xcode-select dotnet; do
    cp "$mock_bin/dispatcher" "$mock_bin/$tool"
done
export PATH="$mock_bin:$PATH"

run_build() {
    local folder="$1"
    mkdir -p "$folder"
    (
        cd "$folder"
        export MOCK_WORK_ROOT="$PWD/sip-testing-tool-macos"
        /bin/bash "$repo_root/build-macos.command"
    ) > "$folder/output.log" 2>&1
}

assert_app() {
    local folder="$1" app
    app="$(cat "$folder/sip-testing-tool-macos/latest-app.txt")"
    [[ "$app" == "$folder/sip-testing-tool-macos/build-"*"/SIP Testing Tool.app" ]]
    [ -x "$app/Contents/MacOS/sip-testing-tool-avalonia" ]
    [ -x "$app/Contents/MacOS/createdump" ]
    [ -f "$app/Contents/Frameworks/libcoreclr.dylib" ]
    [ -f "$app/Contents/Resources/sip-testing-tool-avalonia.dll" ]
    [ -f "$app/Contents/Resources/System.Xml.Linq.dll" ]
    [ -f "$app/Contents/Resources/zh-Hans/Avalonia.resources.dll" ]
    [ -f "$app/Contents/Resources/.app-data" ]
    for name in sip-testing-tool-avalonia.dll System.Xml.Linq.dll sip-testing-tool-avalonia.runtimeconfig.json sip-testing-tool-avalonia.deps.json zh-Hans .app-data; do
        [ -L "$app/Contents/MacOS/$name" ]
        [ "$(readlink "$app/Contents/MacOS/$name")" = "../Resources/$name" ]
        [ -e "$app/Contents/MacOS/$name" ]
    done
    [ "$(readlink "$app/Contents/MacOS/libcoreclr.dylib")" = "../Frameworks/libcoreclr.dylib" ]
    [ -f "$app/Contents/MacOS/libcoreclr.dylib" ]
    # .NET resolves the main DLL to Resources, then probes for its native runtime.
    for name in libhostfxr.dylib libhostpolicy.dylib libcoreclr.dylib; do
        [ "$(readlink "$app/Contents/Resources/$name")" = "../Frameworks/$name" ]
        [ -f "$app/Contents/Resources/$name" ]
    done
    # Native runtime code may probe relative to its own resolved Frameworks path.
    [ -f "$app/Contents/Frameworks/System.Private.CoreLib.dll" ]
    [ -x "$app/Contents/Frameworks/createdump" ]
    [ -f "$app/Contents/Resources/app.icns" ]
    [ -s "${app%/*}/build.log" ]
    [ -x "$folder/sip-testing-tool-macos/dotnet-$MOCK_ARCH/dotnet" ]
    [ "$(wc -l < "$folder/sip-testing-tool-macos/signing.log" | tr -d ' ')" = "7" ]
    grep -Fq -- "$app/Contents/Frameworks/libcoreclr.dylib" "$folder/sip-testing-tool-macos/signing.log"
}

export MOCK_ARCH=arm64 MOCK_CPU=arm64 MOCK_ARM_HARDWARE=1
run_build "$test_root/Apple Silicon"
assert_app "$test_root/Apple Silicon"
first_app="$(cat "$test_root/Apple Silicon/sip-testing-tool-macos/latest-app.txt")"
cp -R "$first_app" "$test_root/Relocated.app"
[ -L "$test_root/Relocated.app/Contents/MacOS/sip-testing-tool-avalonia.dll" ]
[ -f "$test_root/Relocated.app/Contents/MacOS/sip-testing-tool-avalonia.dll" ]
[ -f "$test_root/Relocated.app/Contents/MacOS/libcoreclr.dylib" ]
[ -f "$test_root/Relocated.app/Contents/MacOS/zh-Hans/Avalonia.resources.dll" ]
[ -f "$test_root/Relocated.app/Contents/Resources/libhostfxr.dylib" ]
[ -f "$test_root/Relocated.app/Contents/Frameworks/System.Private.CoreLib.dll" ]
[ -x "$test_root/Relocated.app/Contents/Frameworks/createdump" ]
printf 'keep source edits\n' > "${first_app%/*}/source/sentinel.txt"
printf 'keep work folder\n' > "$test_root/Apple Silicon/sip-testing-tool-macos/sentinel.txt"
run_build "$test_root/Apple Silicon"
second_app="$(cat "$test_root/Apple Silicon/sip-testing-tool-macos/latest-app.txt")"
[ "$first_app" != "$second_app" ] && [ -d "$first_app" ] && [ -d "$second_app" ]
[ -f "${first_app%/*}/source/sentinel.txt" ]
[ -f "$test_root/Apple Silicon/sip-testing-tool-macos/sentinel.txt" ]

export MOCK_ARCH=x64 MOCK_CPU=x86_64 MOCK_ARM_HARDWARE=0
run_build "$test_root/Intel"
assert_app "$test_root/Intel"
export MOCK_ARCH=arm64 MOCK_CPU=x86_64 MOCK_ARM_HARDWARE=1
run_build "$test_root/Rosetta"
assert_app "$test_root/Rosetta"

export MOCK_OS=Linux
if run_build "$test_root/Wrong OS"; then exit 1; fi
[ ! -e "$test_root/Wrong OS/sip-testing-tool-macos" ]
unset MOCK_OS
export MOCK_MACOS=13.7
if run_build "$test_root/Old macOS"; then exit 1; fi
[ ! -e "$test_root/Old macOS/sip-testing-tool-macos" ]
unset MOCK_MACOS

export MOCK_DOWNLOAD_FAIL=1
if run_build "$test_root/Network failure"; then exit 1; fi
[ ! -e "$test_root/Network failure/sip-testing-tool-macos/dotnet-arm64" ]
unset MOCK_DOWNLOAD_FAIL
export MOCK_SIGN_FAIL=1
if run_build "$test_root/Apple Silicon"; then exit 1; fi
[ "$(cat "$test_root/Apple Silicon/sip-testing-tool-macos/latest-app.txt")" = "$second_app" ]
unset MOCK_SIGN_FAIL

printf 'Mac bootstrap checks passed (Intel, Apple Silicon, Rosetta, reruns and failures).\nDiagnostics: %s\n' "$test_root"
