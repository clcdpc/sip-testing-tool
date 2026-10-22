# SIP Testing Tool

Open **`src/sip-testing-tool.sln`** in Visual Studio with .NET 10 and Windows desktop development support. Set **sip-testing-tool** as the startup project for the Windows Forms app. The **sip-testing-tool-avalonia** project is also available for Windows/Linux testing. Use **Build Solution** and **Start Debugging**; NuGet restores the dependencies. The Avalonia extension for Visual Studio is optional for XAML previewing.

## Projects

| Project | Purpose |
| --- | --- |
| `src/sip-testing-tool-avalonia` | Avalonia desktop app for Windows and Linux, with Velopack updates. |
| `src/sip-testing-tool` | Windows Forms app with the existing ClickOnce publishing profiles. |
| `src/sip-core` | Shared SIP client used by both apps and the console. |
| `src/sip-console` | JSON command runner that emits PRTG XML. |
| `tests/sip-tests` | Windows Forms, console and SIP regression checks. |
| `tests/sip-desktop-tests` | Portable Avalonia, SIP session, settings and updater regression checks. |

The Avalonia interface keeps connection, login, patron and item values in the top ribbon. Select a command in the left workbench and use **Execute**; the colored raw sent/received log remains on the right. The split between workbench and log is resizable. Notice update fields are inline in the workbench.

The Avalonia app runs network requests off the UI thread, permits one SIP operation at a time, and closes stalled connections after 30 seconds. It supports the existing encoding/checksum settings, light/dark themes, Always on Top, settings import/export and log copy/export. Saved defaults live outside the installation in the current user's application-data directory: `%APPDATA%\Clc\SipTestingTool\settings.json` on Windows, normally `~/.config/Clc/SipTestingTool/settings.json` on Linux. Use **Settings → Import settings** to bring across a `.sipc` file from the Windows Forms app. Defaults start with automatic connection disabled and empty connection details.

The console project includes `Examples/acs-status.json`. Supply a JSON filename or inline JSON as its command-line argument. Existing field aliases, `{now}` placeholders, and success regular expressions are supported.

## Avalonia publishing and updates

Right-click **sip-testing-tool-avalonia → Publish**, select one of its checked-in folder profiles, and publish in **Release**:

| Visual Studio profile | Application files | Velopack release files |
| --- | --- | --- |
| `Velopack-Windows` | `artifacts/avalonia/publish/win-x64` | `artifacts/velopack/win-x64` |
| `Velopack-Linux` | `artifacts/avalonia/publish/linux-x64` | `artifacts/velopack/linux-x64` |

The profiles publish self-contained .NET applications and then run the pinned local Velopack CLI. Windows produces an installer, portable ZIP and update package; Linux produces an AppImage and update package. Both profiles can be packaged on Windows. Increase **Version** in the Avalonia `.csproj` for every release; a release version must identify the same application contents whenever it is served to users. Keep old release packages when publishing subsequent versions so Velopack can generate deltas.

If publishing reports an **equal or greater version**, that version already exists in the selected release directory, including packages created during local testing. Increase `<Version>` in `src/sip-testing-tool-avalonia/sip-testing-tool-avalonia.csproj` (for example, `2.1.0` to `2.1.1`) and publish again. The application file version follows that value automatically. Validation publishes can use a separate `VelopackOutputDir` to avoid occupying the next release version in the normal output directory.

The app checks for updates at startup when enabled in **Settings → Change default settings**, and from **Updates → Check for updates**. Available updates download in the background. **Restart to update** applies an update only when the user chooses to restart and no SIP operation is running. Development builds skip feed requests. User defaults remain outside the replaced application files.

The proposed feed base is `https://siptool.clcohio.org/updates/desktop/`. The updater appends the current OS and architecture, selecting `win-x64/` or `linux-x64/`. To activate a feed, copy the **contents of each Velopack release directory**, including its release index and packages, into the matching directory on the web server. Host these files unchanged over HTTPS and allow the package/index/AppImage file extensions to be downloaded. This setup does not upload files to the server or replace the existing ClickOnce deployment.

Windows packages are unsigned until signing is configured. The optional **VelopackSignParams** MSBuild property passes Windows SignTool arguments to Velopack; configure the certificate/provider and timestamping for the publishing account before distributing releases. The Windows Forms ClickOnce certificate selection is independent of this setting. Linux users download the AppImage to a writable location, mark it executable once (`chmod +x <file>.AppImage`), then launch it; subsequent updates replace that AppImage. Existing ClickOnce users install the new desktop app once and can import their `.sipc` settings.

Equivalent local commands from the repository root are:

```text
dotnet publish src/sip-testing-tool-avalonia -c Release -p:PublishProfile=Velopack-Windows
dotnet publish src/sip-testing-tool-avalonia -c Release -p:PublishProfile=Velopack-Linux
```

## Windows Forms publishing

The Windows Forms project's profiles are under `src/sip-testing-tool/Properties/PublishProfiles`. Folder publishing writes to `artifacts/app`; ClickOnce writes to `artifacts/clickonce` and retains the existing install URL and release sequence.

ClickOnce signing uses DigiCert KeyLocker through the Windows current-user Personal certificate store. The profile selects certificate `4DA4BD6078F452BB8F3B6CE766144B352F8D55EC`, valid through June 23, 2027, using the `DigiCert Software Trust Manager KSP` provider. Publishing requires the KeyLocker client and authentication for the publishing Windows account; no PFX file is referenced. See [DigiCert's ClickOnce signing instructions](https://docs.digicert.com/en/digicert-keylocker/code-signing/sign-with-third-party-signing-tools/windows-applications/sign-clickonce-manifests-with-visual-studio-using-ksp-library.html).

## Verification

Both test projects use MSTest.Sdk 4.5.1. Open `src/sip-testing-tool.sln`, build the solution, then use **Test > Test Explorer > Run All**, or run `dotnet test src/sip-testing-tool.sln`. Windows tests cover checksums, connections, console output and SIP command options; portable desktop tests cover sessions, settings, workbench behavior and updates. Tests use loopback SIP peers and Avalonia's headless platform.

Generated output is ignored by Git. The original Git history is preserved. This consolidated version combines the .NET 10 app with ACS Status, Always On Top, configurable encoding/checksum validation, and connection-loss handling from the older project.
