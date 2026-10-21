# SIP Testing Tool

This is the authoritative project for the Windows SIP testing app, its shared SIP library, and the console monitoring tool. Open **`sip-testing-tool.sln` in this folder**. All projects use .NET 10; the desktop app and regression checks require Windows.

## Projects

| Path | Purpose |
| --- | --- |
| `src/sip-testing-tool` | Windows Forms app. Set this as the startup project in Visual Studio. The main form is `MainForm.cs`. |
| `src/sip-core` | Shared SIP client used by the app and console tool. |
| `src/sip-console` | JSON command runner that emits PRTG XML. |
| `tests/sip-tests` | Regression checks using local TCP servers and form construction. |

## Build and run

Install a .NET 10 SDK, or use Visual Studio with .NET 10 and Windows desktop development support. Run these commands from this folder:

```powershell
.\build.ps1
dotnet run --project .\src\sip-testing-tool --configuration Release --no-build
```

`build.ps1` restores packages, builds the whole solution in Release, and runs the regression checks. It uses the preserved Newtonsoft.Json package in `local/offline-feed` when available. A source checkout without that local folder restores from the configured NuGet feeds.

Equivalent commands with the standard NuGet feeds are:

```powershell
dotnet build .\sip-testing-tool.sln --configuration Release
dotnet run --project .\tests\sip-tests --configuration Release --no-build
```

The built app is `src/sip-testing-tool/bin/Release/net10.0-windows/sip-testing-tool.exe`. The checks use loopback addresses and do not contact a library SIP server.

## App settings

Use **Change Default Settings** to configure the server, credentials, encoding, and checksum validation, then **Reload Default Values** to apply them. Defaults use UTF-8, enable checksum checking, and leave automatic connection disabled. The app supports the existing `.sipc` import and export format, ACS Status, and the Always On Top context menu option.

The checked-in `App.config` contains empty connection details. The imported legacy console settings are retained in `local/imported-settings/settings.config` for reference. They are not loaded automatically by the consolidated app.

## Console tool

Copy `examples/acs-status.json`, enter the intended server and port, and run:

```powershell
dotnet run --project .\src\sip-console --configuration Release --no-build -- .\local\commands.json
```

The tool accepts a JSON filename or inline JSON. Existing command sequences, friendly field names such as `sipUsername` and `patronBarcode`, success regular expressions, and `{now}` placeholders are supported. `EncodingName` defaults to `UTF-8`; `CheckChecksum` defaults to `true`. Output includes the legacy PRTG boolean lookup and escapes channel names as XML. Running with `--help` prints usage.

## Publishing and local files

The folder publish profile writes an unsigned framework-dependent app to `artifacts/app`:

```powershell
dotnet publish .\src\sip-testing-tool --configuration Release -p:PublishProfile=FolderProfile
```

The ClickOnce profile retains the previous install URL and release sequence, and writes its output to `artifacts/clickonce`. It references the copied certificate at `local/signing/code_signing_exp2026.pfx`. Certificate access and signing are required when publishing ClickOnce; ordinary builds do not use a signing key.

`local/` is ignored by Git and contains the imported signing keys, legacy settings, original publish profiles, package cache, and source audit records. Preserve that folder alongside the repository when backing up this working copy. Build outputs are also ignored.

## Consolidation record

See [docs/consolidation.md](docs/consolidation.md) for the source mapping and restored behavior. The repository preserves the original Git history and records the consolidated version on `codex/consolidated`. The existing Bitbucket origin is retained; consolidation does not push or deploy anything.
