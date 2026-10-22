# SIP Testing Tool

Open **`src/sip-testing-tool.sln`** in Visual Studio with .NET 10 and Windows desktop development support. Set **sip-testing-tool** as the startup project for the Windows Forms app. The **sip-testing-tool-avalonia** project is also available for Windows/Linux testing. Use **Build Solution** and **Start Debugging**; NuGet restores the dependencies. The Avalonia extension for Visual Studio is optional for XAML previewing.

## Projects

| Project | Purpose |
| --- | --- |
| `src/sip-testing-tool-avalonia` | Avalonia desktop app for Windows and Linux, with Velopack updates. |
| `src/sip-testing-tool` | Windows Forms app with folder, single-file and ClickOnce publishing profiles. |
| `src/sip-core` | Shared SIP client used by both apps and the console. |
| `src/sip-console` | JSON command runner that emits PRTG XML. |
| `tests/sip-tests` | Windows Forms, console and SIP regression checks. |
| `tests/sip-desktop-tests` | Portable Avalonia, SIP session, settings and updater regression checks. |

Both interfaces keep connection, login, patron and item values in the top ribbon. Select a command in the left workbench and use **Execute**; the colored, selectable raw sent/received log remains on the right. The split between workbench and log is resizable. Notice update fields are inline in the workbench in both interfaces.

In both desktop interfaces, press **Enter** in any connection field to **Connect**, or in any login field (including the algorithm options) to **Login**. Enter focuses the corresponding button, so the next **Tab** moves directly to the next section: Login after Connect, or Patron after Login. Validation errors focus the field needing correction. Login becomes available after connecting. When the vendor profile list is open, Enter accepts the highlighted choice; press Enter again to log in. The profile list includes the documented Polaris values **SIP**, **ENVISIONWARE**, **COMPRISE**, **3M** and **ITG**. **SIP** selects the default profile; clearing the field omits VP. Custom profile values can still be typed.

Default connection fields can be left blank in either interface, including the port. **Connect** requires a server and a numeric port from 1 through 65535. Once connected, **Connect** reconnects using the current ribbon values; an invalid edit leaves the existing connection open. Saving defaults keeps the current ribbon values, command options, connection and message history; use **Settings → Reload default values** to apply the saved fields to the ribbon.

In either desktop message log, hover over either character of a field identifier such as **AO**, **AA**, or **U4** to see its name and description. Help covers sent and received SIP2 fields, the **AY** sequence number and **AZ** checksum, and vendor extensions. Alphanumeric identifiers are recognized without treating their values as malformed data. Unknown field codes retain their original values and are labeled as possible server-specific extensions. Standard descriptions follow the [SIP2 protocol definition](https://developers.exlibrisgroup.com/wp-content/uploads/2020/01/3M-Standard-Interchange-Protocol-Version-2.00.pdf). The raw transcript remains selectable, copyable and exportable.

Hover over a fixed field value to see its name, decoded value and exact character range within the message. This includes message IDs, date/time values, policy/status flags, language and payment codes, patron status/summary flags, item counts, fixed-length optional fields, and the **AY**/**AZ** values. A subtle dotted outline marks the whole field, including blank spaces and wrapped rows; identified fixed fields include their code in the outline. Click a fixed field to keep a solid outline after moving the pointer away; click elsewhere, select text, or press **Escape** to clear it. Outlines follow scrolling, resizing and themes without changing the raw transcript or its formatting. Unknown message headers and incomplete fixed values are left uninterpreted; identifier help is still available at recognized variable-field boundaries.

Patron status, summary and supported-message (**BX**) flags can be inspected individually, including blank flags. Each tooltip names the hovered flag, shows its position and set/not-set state, and includes a checklist of the whole group. While inspecting a group, faint cell borders separate its positions and an underline marks each set flag; the hovered or clicked flag has its own outline. Reserved positions and unexpected values are labeled explicitly. Patron status and summary use a space for not set; BX uses **N**. These visual guides leave the raw log and copied/exported text unchanged.

Click **+** beside a log message in either interface to open its field table beneath the transcript. Each field has a name, brief description and value in message order, including repeated fields and the sequence/checksum trailer. Patron status, summary and supported-message fields have a parent row followed by an indented row for every flag, with explicit **SET**, **Not set**, reserved or invalid states. Unknown fields and incomplete data retain their original values. The table shows one message at a time; another **+** switches messages. Drag the divider to resize the table, click **−** or **Collapse** to close it, or press **Escape** while the table has focus. Table cells can be selected and copied, and raw log selection and export keep their existing behavior.

Polaris field help uses the supplied **SIP Message Documentation** (July 2010, pages 2–4) and **RPA via SIP for Vendors 2012** references. It names birth/expiration dates, patron codes and home branches, statistical classes, postal codes, and checkin collection/call-number/hold fields. **U1–U5** are the library-defined **User1–User5** attributes; the references list U4/U5, and the library confirmed the same convention for U1–U3. Their values are kept as configured text. Checkin **CV** alert codes are decoded separately from the legacy Polaris 3.6 login use of CV. Login **CP** authentication overrides and the patron username/barcode behavior of **AA** are explained in their message context.

Recognized Polaris **AV** fine entries expand into child rows for the fine transaction ID and outstanding amount, plus fine type, fee description, related item barcode and title where supplied. The documented amount-first 3M format keeps the combined fee description/title together because it specifies no boundary between them. Layouts are recognized from the value syntax; the two references differ on some vendor-profile mappings. Unknown layouts and extra data remain readable without guessed components. Hovering an AV identifier also lists its decoded components. Count rows and simple name/address fields omit descriptions that merely repeat their names; counts still distinguish numeric values from unavailable blank counts.

**Checkout** includes renewal policy, no-block mode, an optional no-block due date/time, item properties (CH), terminal password (AC), fee acknowledgement (BO), and cancel (BI). Check the date picker to include its date/time; an unchecked picker sends a blank date. Blank item properties omit CH. BO and BI each offer **Omit field**, **No (N)** and **Yes (Y)**. The AC terminal password is separate from the shared login password (CO) and patron PIN (AD).

Both interfaces use ordinary, unmasked textboxes for passwords and PINs, including default settings. **Tab** and **Shift+Tab** select the entire existing textbox value on every visit, ready to overwrite; mouse clicks still place the caret normally. This applies to the ribbon, command options and dialog textboxes. Dark mode uses muted textbox borders and a subdued focus outline.

Checkout defaults preserve the previous request: renewals allowed, no-block off, blank due date, empty AC, no CH or BI, and BO=N. Values stay in place while switching commands. **Reset checkout options** restores those defaults without changing the shared values or message history.

## Command options

Only the selected command's options are shown. Labels sit beside their inputs when the workbench has enough room; longer labels and date editors move above their inputs to preserve a usable input width. The rows adjust when the divider is dragged. All command options stay in the running session while switching commands or themes. Each command has a **Reset … options** button that affects that command's fields. New command options are session-only; saved defaults and `.sipc` files continue to cover the existing shared settings.

| Command | Editable options |
| --- | --- |
| Checkin | No block; return date/time; return location (AP); item properties (CH); terminal password (AC); cancel (BI). |
| Renew | Third-party renewals; no block; due date/time; include shared item barcode (AB); title identifier (AJ); item properties (CH); terminal password (AC); fee acknowledged (BO). |
| Renew all | Terminal password (AC); fee acknowledged (BO). |
| Item information | Terminal password (AC). |
| Patron information | Summary; optional BP/BQ range; language code; terminal password (AC). Ranges can be cleared to omit them, including with no summary selected. |
| Patron status | Language code; terminal password (AC). |
| End session | Terminal password (AC). |
| Fee paid | Fee/payment types; amount; currency code; fee identifier (CG); transaction ID (BK); terminal password (AC). |
| Notice update | Delivery status/date/medium; notice type; comment (DE); terminal password (AC). Uses the shared patron and item barcodes. |
| Login | User ID and password algorithm identifiers; credentials, location and vendor profile remain in the ribbon. |
| ACS Status | Self-check status; maximum print width; protocol version. |

Defaults retain the existing request choices: Checkin no-block is off and return location falls back to the shared Terminal (AP); Renew allows third-party renewal, no-block is off, and its date starts fourteen days ahead; language codes are `000`, currency is `USD`, login algorithms are `0`, and ACS status is `0` / `100` / `2.00`. Reset recomputes suggested dates. Blank optional fields are omitted as indicated by their tooltips; existing empty AC fields remain for commands that previously sent them. BO and BI distinguish omission from explicit N/Y. Checkin and Renew now honor the selected no-block flag.

An unchecked Checkin return date or Notice delivery date uses the execution time. An unchecked Renew due date sends eighteen spaces. Selecting a calendar date preserves the entered time. Invalid dates, language/range values, payment values, algorithm identifiers and status/version widths produce a status-bar message before anything is sent. Payment amounts use a decimal point on the wire regardless of the UI culture.

The ribbon's institution (AO) and terminal (AP) values are read for each execution, so edits after connecting take effect on subsequent requests. A Checkin return-location override takes precedence over the shared AP value.

Login algorithm identifiers describe **already encoded** values agreed with the SIP server. `0` sends plain text. Changing an identifier does not perform encryption; the tool sends the visible credentials as entered. Field mappings follow the [SIP2 protocol definition](https://developers.exlibrisgroup.com/wp-content/uploads/2020/01/3M-Standard-Interchange-Protocol-Version-2.00.pdf); Notice update retains the existing server-specific message 43 implementation. Both interfaces expose these command options and use the same SIP client. The field reference and Polaris decoding are shared in `sip-core/Inspection`, so descriptions and compound values stay consistent.

In Windows Forms, **View → Dark mode** (**Ctrl+Shift+D**) switches the current window and open dialogs immediately. The connection, current command values, message history and log selection stay in place; existing sent/received colors update with the theme. The choice is saved per user in `%LOCALAPPDATA%\Clc\SipTestingTool\winforms-appearance.json`. The theme checkbox in default settings also applies immediately; **Save and Close** remembers that choice. A user's existing `dark_theme_enabled` setting is used until a per-user theme choice has been saved.

The checkout date/time field and its calendar popup follow the selected theme, including on Windows 10. Check the field to include it, type `yyyy-MM-dd HH:mm:ss`, or choose a calendar date while retaining the time. **Up/Down** adjusts the date/time part at the caret, **F4** or **Alt+Down** opens the calendar, and **Escape** restores the last valid date. Calendar arrow keys move by day/week, **Page Up/Down** by month, and **Enter** selects the focused date. Incomplete or impossible checked dates stop checkout with a validation message; unchecked dates are omitted.

Avalonia also provides **View → Dark mode** (**Ctrl+Shift+D**) and previews theme changes in the settings dialog; cancelling restores the original theme. Its field table uses virtualized rows, selectable cells and **Copy table** for tab-separated output. **Ctrl+Enter** in the raw log, or **Inspect message fields** in its context menu, inspects the message at the caret. Hover and pinned outlines use a separate drawing layer so moving the pointer preserves text, selection and colors.

The Avalonia app runs network requests off the UI thread, permits one SIP operation at a time, and closes stalled connections after 30 seconds. It supports the existing encoding/checksum settings, light/dark themes, Always on Top, settings import/export and log copy/export. Saved defaults live outside the installation in the current user's application-data directory: `%APPDATA%\Clc\SipTestingTool\settings.json` on Windows, normally `~/.config/Clc/SipTestingTool/settings.json` on Linux. Use **Settings → Import settings** to bring across a `.sipc` file from the Windows Forms app. Defaults start with automatic connection disabled and empty connection details.

The console project includes `Examples/acs-status.json`. Supply a JSON filename or inline JSON as its command-line argument. Existing field aliases, `{now}` placeholders, and success regular expressions are supported.

## Avalonia publishing and updates

Right-click **sip-testing-tool-avalonia → Publish**, select one of its checked-in folder profiles, and publish in **Release**:

| Visual Studio profile | Application files | Velopack release files |
| --- | --- | --- |
| `Velopack-Windows` | `artifacts/avalonia/publish/win-x64` | `artifacts/velopack/win-x64` |
| `Velopack-Linux` | `artifacts/avalonia/publish/linux-x64` | `artifacts/velopack/linux-x64` |
| `Windows-SingleFile` | `artifacts/avalonia/publish/win-x64-single-file/sip-testing-tool-avalonia.exe` | — |

The paths above are relative to the consolidated repository root. Publish profiles store plain folder paths relative to each application's `.csproj`, which both Visual Studio and command-line publishing resolve into that root's `artifacts` folder. Visual Studio also reads profiles from XML streams without their file location, so profile values must not depend on `MSBuildProjectDirectory`, `MSBuildThisFileDirectory`, or imports that use those properties. `PublishPaths.props` supplies the absolute root for Velopack packaging during the application build.

The two **Velopack** profiles publish self-contained .NET applications and then run the pinned local Velopack CLI. Windows produces an installer, portable ZIP and update package; Linux produces an AppImage and update package. Both Velopack profiles can be packaged on Windows. Increase **Version** in the Avalonia `.csproj` for every release; a release version must identify the same application contents whenever it is served to users. Keep old release packages when publishing subsequent versions so Velopack can generate deltas.

**Windows-SingleFile** produces one portable Windows x64 EXE containing the application and its Avalonia dependencies. Users install the [x64 .NET 10 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) separately; the .NET 10 Desktop Runtime or SDK also satisfies that requirement. The .NET runtime is excluded from the EXE. Debug symbol files are omitted from the publish output, and Avalonia's native graphics/text libraries unpack into .NET's temporary cache at startup. Settings retain their existing per-user location. Update this portable build by replacing the EXE; use **Velopack-Windows** for the installer and automatic update workflow. See Microsoft's [single-file deployment documentation](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview) for bundling and native extraction behavior.

If publishing reports an **equal or greater version**, that version already exists in the selected release directory, including packages created during local testing. Increase `<Version>` in `src/sip-testing-tool-avalonia/sip-testing-tool-avalonia.csproj` (for example, `2.1.0` to `2.1.1`) and publish again. The application file version follows that value automatically. Validation publishes can use a separate `VelopackOutputDir` to avoid occupying the next release version in the normal output directory.

The app checks for updates at startup when enabled in **Settings → Change default settings**, and from **Updates → Check for updates**. Available updates download in the background. **Restart to update** applies an update only when the user chooses to restart and no SIP operation is running. Development builds skip feed requests. User defaults remain outside the replaced application files.

The proposed feed base is `https://siptool.clcohio.org/updates/desktop/`. The updater appends the current OS and architecture, selecting `win-x64/` or `linux-x64/`. To activate a feed, copy the **contents of each Velopack release directory**, including its release index and packages, into the matching directory on the web server. Host these files unchanged over HTTPS and allow the package/index/AppImage file extensions to be downloaded. This setup does not upload files to the server or replace the existing ClickOnce deployment.

Windows packages are unsigned until signing is configured. The optional **VelopackSignParams** MSBuild property passes Windows SignTool arguments to Velopack; configure the certificate/provider and timestamping for the publishing account before distributing releases. The Windows Forms ClickOnce certificate selection is independent of this setting. Linux users download the AppImage to a writable location, mark it executable once (`chmod +x <file>.AppImage`), then launch it; subsequent updates replace that AppImage. Existing ClickOnce users install the new desktop app once and can import their `.sipc` settings.

Equivalent local commands from the repository root are:

```text
dotnet publish src/sip-testing-tool-avalonia -c Release -p:PublishProfile=Velopack-Windows
dotnet publish src/sip-testing-tool-avalonia -c Release -p:PublishProfile=Velopack-Linux
dotnet publish src/sip-testing-tool-avalonia -c Release -p:PublishProfile=Windows-SingleFile
```

## Windows Forms publishing

Right-click **sip-testing-tool → Publish** and choose one of the profiles under `src/sip-testing-tool/Properties/PublishProfiles`:

| Visual Studio profile | Output under the consolidated repository root |
| --- | --- |
| `FolderProfile` | `artifacts/app` |
| `Windows-SingleFile` | `artifacts/app-single-file/sip-testing-tool.exe` |
| `ClickOnceProfile` | `artifacts/clickonce` |

**Windows-SingleFile** produces one portable Windows x64 EXE containing the application and its dependencies. Users install the [x64 .NET 10 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) separately. Starting defaults are embedded in the EXE; the app creates `%LOCALAPPDATA%\Clc\SipTestingTool\winforms-single-file.config` on first launch, and **Change default settings** saves there. Existing saved values survive replacing the portable EXE. Folder and ClickOnce builds retain their existing configuration files. Publish from the command line with `dotnet publish src/sip-testing-tool -c Release -p:PublishProfile=Windows-SingleFile`.

ClickOnce retains the existing install URL and release sequence.

ClickOnce signing uses DigiCert KeyLocker through the Windows current-user Personal certificate store. The profile selects certificate `4DA4BD6078F452BB8F3B6CE766144B352F8D55EC`, valid through June 23, 2027, using the `DigiCert Software Trust Manager KSP` provider. Publishing requires the KeyLocker client and authentication for the publishing Windows account; no PFX file is referenced. See [DigiCert's ClickOnce signing instructions](https://docs.digicert.com/en/digicert-keylocker/code-signing/sign-with-third-party-signing-tools/windows-applications/sign-clickonce-manifests-with-visual-studio-using-ksp-library.html).

## Verification

Both test projects use MSTest.Sdk 4.5.1. Open `src/sip-testing-tool.sln`, build the solution, then use **Test > Test Explorer > Run All**. The Windows Forms project exposes 211 regression tests, 24 focused command-option tests and 116 individually named form-to-submission cases; the Avalonia project exposes 102 tests. Windows Forms UI tests run on an STA thread; Avalonia UI tests use its headless dispatcher. Each assembly runs sequentially because its checks share UI and process state.

From the repository root, the equivalent command is:

```text
dotnet test src/sip-testing-tool.sln
```

Run `dotnet msbuild build/VerifyPublishPaths.proj` to check all six publish profiles, including Visual Studio's XML-stream reader that has no profile file location. This checks their output destinations without building, signing or packaging a release.

Tests are ordinary MSTest methods grouped by behavior: checksums, connections, console output, workbench behavior, checkout, themes, command-option state, settings, updates and desktop sessions. Test classes use MSTest lifecycle methods; the shared test support is limited to UI setup, test data and loopback SIP peers. Both projects use MSTest assertions directly.

The tests use loopback SIP servers and Avalonia's headless platform; they never contact a production SIP endpoint. Windows Forms tests submit through public buttons and verify the captured packets and message log.

`tests/sip-tests/FormSubmissionTests.cs` and `FormSubmissionTests.Cases.cs` contain parameterized form-to-submission checks for all twelve commands. Each case edits real form controls, uses `Button.PerformClick()` to reset and submit, captures the actual request at a loopback TCP server, and compares fixed and variable fields with independent expected values. These cases exercise every command option, dropdown choice, typed date overrides and unchecked dates, optional-field omission, numeric boundaries, decimal culture handling, both Login buttons, updated ribbon values, checksums and transcript entries. These checks verify client wiring and serialization; acceptance by a particular SIP server still depends on that server's implementation and configuration.

Generated output is ignored by Git. The original Git history is preserved. This consolidated version combines the .NET 10 app with ACS Status, Always On Top, configurable encoding/checksum validation, and connection-loss handling from the older project.

## Code style

Prefer straightforward control flow, descriptive names, and small methods with one purpose. Keep simple calls and expressions together; wrap at natural boundaries when it helps the reader, without enforcing a hard line length. The root `.editorconfig` supplies the shared C# formatting rules used by Visual Studio. Keep generated designer files under the designer's control. Windows Forms tests use the shared type-specific accessors in `TestSupport/ControlLookupExtensions.cs`; use a descriptive local variable when a scenario reuses a control.

To apply or check formatting from the repository root:

```text
dotnet format whitespace src/sip-testing-tool.sln
dotnet format whitespace src/sip-testing-tool.sln --verify-no-changes
```
