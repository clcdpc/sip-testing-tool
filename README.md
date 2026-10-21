# SIP Testing Tool

Open **`src/sip-testing-tool.sln`** in Visual Studio with .NET 10 and Windows desktop development support. Set **sip-testing-tool** as the startup project, select Debug or Release, and use **Build Solution** and **Start Debugging**. NuGet packages restore through Visual Studio.

## Projects

| Project | Purpose |
| --- | --- |
| `src/sip-testing-tool` | Windows Forms app; the main form is `MainForm.cs`. |
| `src/sip-core` | Shared SIP client used by the app and console. |
| `src/sip-console` | JSON command runner that emits PRTG XML. |
| `tests/sip-tests` | Regression checks; run as the startup project without debugging to see the results. |

The console project includes `Examples/acs-status.json`. Supply a JSON filename or inline JSON as its command-line argument. Existing field aliases, `{now}` placeholders, and success regular expressions are supported.

## Settings and publishing

Use **Change Default Settings** and **Reload Default Values** in the app. The checked-in defaults use UTF-8 and checksum validation, with automatic connection disabled and connection details empty. Existing `.sipc` settings files can be imported.

Visual Studio publish profiles are under the app project's `Properties/PublishProfiles`. Folder publishing writes to `artifacts/app`; ClickOnce writes to `artifacts/clickonce` and retains the existing install URL and release sequence.

ClickOnce signing uses DigiCert KeyLocker through the Windows current-user Personal certificate store. The profile selects certificate `4DA4BD6078F452BB8F3B6CE766144B352F8D55EC`, valid through June 23, 2027, using the `DigiCert Software Trust Manager KSP` provider. Publishing requires the KeyLocker client and authentication for the publishing Windows account; no PFX file is referenced. See [DigiCert's ClickOnce signing instructions](https://docs.digicert.com/en/digicert-keylocker/code-signing/sign-with-third-party-signing-tools/windows-applications/sign-clickonce-manifests-with-visual-studio-using-ksp-library.html).

Generated output is ignored by Git.

The original Git history is preserved. This version combines the .NET 10 app with ACS Status, Always On Top, configurable encoding/checksum validation, and connection-loss handling from the older project.
