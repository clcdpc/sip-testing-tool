# SIP Testing Tool

**Avalonia is the primary SIP Testing Tool desktop app**, targeting Windows, Linux and macOS. The **3.0** release series introduces it as the main application, starting with **3.0.0-beta.1**. The legacy Windows Forms app remains available as a fallback while the Avalonia release is validated.

The main window's title shows the running release version, including prerelease identifiers, for example **SIP Testing Tool v3.0.0-beta.6**. The title and updater read the same application informational version; commit/build metadata is omitted from the title.

Open **`src/sip-testing-tool.sln`** in Visual Studio with .NET 10 and Windows desktop development support. Set **sip-testing-tool-avalonia** as the startup project. Use **Build Solution** and **Start Debugging**; NuGet restores the dependencies. The Avalonia extension for Visual Studio is optional for XAML previewing. Select **sip-testing-tool** explicitly when working on the legacy Windows Forms app.

## Projects

| Project | Purpose |
| --- | --- |
| `src/sip-testing-tool-avalonia` | Primary desktop app for Windows, Linux and macOS; Updatum updates on Windows/Linux. |
| `src/sip-testing-tool` | Legacy Windows Forms fallback with folder, single-file and ClickOnce publishing profiles. |
| `src/sip-core` | Shared SIP client used by both apps and the console. |
| `src/sip-console` | JSON command runner that emits PRTG XML. |
| `tests/sip-tests` | Windows Forms, console and SIP regression checks. |
| `tests/sip-desktop-tests` | Portable Avalonia, SIP session, settings and updater regression checks. |

Both interfaces keep connection, login, patron and item values in the top ribbon. Select a command in the left workbench and use **Execute**; the colored, selectable raw sent/received log remains on the right. The split between workbench and log is resizable. Notice update fields are inline in the workbench in both interfaces.

In both desktop interfaces, press **Enter** in any connection field to **Connect**, or in any login field (including the algorithm options) to **Login**. Enter focuses the corresponding button, so the next **Tab** moves directly to the next section: Login after Connect, or Patron after Login. Validation errors focus the field needing correction. Login becomes available after connecting. When the vendor profile list is open, Enter accepts the highlighted choice; press Enter again to log in. The profile list includes the documented Polaris values **SIP**, **ENVISIONWARE**, **COMPRISE**, **3M** and **ITG**. **SIP** selects the default profile; clearing the field omits VP. Custom profile values can still be typed.

Default connection fields start blank in either interface, including the port. **Connect** requires a server and a numeric port from 1 through 65535. Once connected, **Connect** reconnects using the current ribbon values; an invalid edit leaves the existing connection open. Saving defaults keeps the current ribbon values, command options, connection and message history; use **Settings → Reload default values** to apply the saved fields to the ribbon.

In either desktop message log, hover over either character of a field identifier such as **AO**, **AA**, or **U4** to see its name and description. Help covers sent and received SIP2 fields, the **AY** sequence number and **AZ** checksum, and vendor extensions. Alphanumeric identifiers are recognized without treating their values as malformed data. Unknown field codes retain their original values and are labeled as possible server-specific extensions. Standard descriptions follow the [SIP2 protocol definition](https://developers.exlibrisgroup.com/wp-content/uploads/2020/01/3M-Standard-Interchange-Protocol-Version-2.00.pdf). The raw transcript remains selectable, copyable and exportable.

Hover over a fixed field value to see its name, decoded value and exact character range within the message. This includes message IDs, date/time values, policy/status flags, language and payment codes, patron status/summary flags, item counts, fixed-length optional fields, and the **AY**/**AZ** values. A subtle dotted outline marks the whole field, including blank spaces and wrapped rows; identified fixed fields include their code in the outline. Click a fixed field to keep a solid outline after moving the pointer away; click elsewhere, select text, or press **Escape** to clear it. Outlines follow scrolling, resizing and themes without changing the raw transcript or its formatting. Unknown message headers and incomplete fixed values are left uninterpreted; identifier help is still available at recognized variable-field boundaries.

Patron status, summary and supported-message (**BX**) flags can be inspected individually, including blank flags. Each tooltip names the hovered flag, shows its position and set/not-set state, and includes a checklist of the whole group. While inspecting a group, faint cell borders separate its positions and an underline marks each set flag; the hovered or clicked flag has its own outline. Reserved positions and unexpected values are labeled explicitly. Patron status and summary use a space for not set; BX uses **N**. These visual guides leave the raw log and copied/exported text unchanged.

Click **+** beside a log message in either interface to open its field table beneath the transcript. Each field has a name, brief description and value in message order, including repeated fields and the sequence/checksum trailer. Patron status, summary and supported-message fields have a parent row followed by an indented row for every flag, with explicit **SET**, **Not set**, reserved or invalid states. Unknown fields and incomplete data retain their original values. The table shows one message at a time; another **+** switches messages. Drag the divider to resize the table, click **−** or **Collapse** to close it, or press **Escape** while the table has focus. Table cells can be selected and copied, and raw log selection and export keep their existing behavior.

In Avalonia, **Actions → Focus messages** jumps to open properties or the first visible message expander; an empty log focuses **Clear log**. **Tab** and **Shift+Tab** reach the visible message expanders and skip the read-only transcript. The transcript still supports mouse selection, copying and inspecting the message at its caret. Press **Enter** or **Space** to toggle a focused expander. Expanding a message immediately focuses its properties. Use the arrow keys to browse property rows; **Escape** closes the properties and returns focus to an expander. Clearing the log returns focus to **Clear log** when the focused message disappears. See the [keyboard shortcuts](#desktop-keyboard-shortcuts) below.

Polaris field help uses the supplied **SIP Message Documentation** (July 2010, pages 2–4), **RPA via SIP for Vendors 2012**, and **Polaris SIP2 Custom Extensions & Enhancements** (revised 23 October 2026) references. The last document is an unofficial static analysis of SIPService.exe **8.1.7065.1**. The reference names birth/expiration dates, patron codes and home branches, statistical classes, postal codes, and checkin collection/call-number/hold fields. **U1–U5** are library-defined **User1–User5** attributes, identified together in the 8.1 reference; their configured text is preserved. Checkin **CV** alert codes remain distinct from the historical Polaris 3.6 login field; the 8.1 reference does not establish CV as a login alias. Login **CP** authentication overrides and the patron username/barcode behavior of **AA** are explained in their message context.

The 8.1 additions identify **FA** as accrued fines total, **PC** and EnvisionWare **SI** as internal patron IDs, **PB** as the resolved patron barcode, and **DB** as patron credit balance. Numeric text is preserved: FA's observed integer truncation is explained, and DB/FA monetary units remain unconfirmed. The **PY** help describes the local end-of-day expiration comparison and warns that an unevaluated/default N does not establish that registration is current. Known response headers **42** (deposit-account activity) and **44** (notice update) expose their first tagged fields correctly. In response 42, **TX** is documented only as literal zero in the analyzed build. Notice-pair **BK** is an echoed request value with an unconfirmed business meaning. These definitions do not assume the unproven request-41 layout or response-44 result-code mappings.

Recognized Polaris **AV** fine entries expand into child rows for the fine transaction ID and outstanding amount, plus fine type, fee description, related item barcode and title where supplied. The documented amount-first 3M format keeps the combined fee description/title together because it specifies no boundary between them. Layouts are recognized from the value syntax; the two references differ on some vendor-profile mappings. Unknown layouts and extra data remain readable without guessed components. Hovering an AV identifier also lists its decoded components. Count rows and simple name/address fields omit descriptions that merely repeat their names; counts still distinguish numeric values from unavailable blank counts.

The [worked field dictionary sample](examples/field-definitions.example.json) contains **96 entries covering 85 field codes and 55 value labels**, generated from the current compiled field reference. It includes the standard tagged fields, library-defined U1–U5 labels, Polaris extensions and message-specific meanings. This is a reference/template for the proposed optional dictionary; external-file loading has not yet been implemented. The opinionated defaults and parsing rules remain compiled into the application.

In the sample, `version` identifies the JSON format. Each `fields` entry has a `code`, `name` and `description`; optional `messages` scopes it to the listed two-digit message IDs, and optional `values` maps exact raw strings to explanations. A general entry and a message-specific entry can share a code; the scoped entry takes precedence in that message. Quoted numeric keys preserve leading zeroes, and the two-space CV key represents a blank checkin alert. Keep only the entries/properties you intend to customize in a future override file so omitted definitions inherit the compiled defaults. Fixed message headers, flag positions, date decoding and compound AV parsing continue to use their built-in implementations.

**Checkout** includes renewal policy, no-block mode, an optional no-block due date/time, item properties (CH), terminal password (AC), fee acknowledgement (BO), and cancel (BI). Check the date picker to include its date/time; an unchecked picker sends a blank date. Blank item properties omit CH. BO and BI each offer **Omit field**, **No (N)** and **Yes (Y)**. The AC terminal password is separate from the shared login password (CO) and patron PIN (AD).

Both interfaces use ordinary, unmasked textboxes for passwords and PINs, including default settings. **Tab** and **Shift+Tab** select the entire existing textbox value on every visit, ready to overwrite; mouse clicks still place the caret normally. This applies to the ribbon, command options and dialog textboxes. Dark mode uses muted textbox borders and a subdued focus outline.

Checkout defaults preserve the previous request: renewals allowed, no-block off, blank due date, empty AC, no CH or BI, and BO=N. Values stay in place while switching commands. **Reset checkout options** restores those defaults without changing the shared values or message history.

## Desktop keyboard shortcuts

These shortcuts apply to the Avalonia app. Application letter shortcuts use **Ctrl** on Windows/Linux and **Cmd** on macOS, where **Cmd** means Command (⌘). Function keys handle execution and navigation. Shift is reserved for reverse navigation and Clear log. Message inspection uses **Ctrl+Enter** on Windows/Linux and **Cmd+Enter** on macOS. Standard Copy, Cut, Paste, Select all and Undo shortcuts remain available.

| Action | Windows / Linux | macOS |
| --- | --- | --- |
| Execute the selected SIP command | F5 | F5 |
| Connect / reconnect | Ctrl+N | Cmd+N |
| Execute SIP Login | Ctrl+U | Cmd+U |
| Focus the command dropdown | Ctrl+K | Cmd+K |
| Focus and select server address | Ctrl+L | Cmd+L |
| Focus and select patron barcode (AA) | Ctrl+P | Cmd+P |
| Focus and select item barcode (AB) | Ctrl+I | Cmd+I |
| Focus messages | Ctrl+J | Cmd+J |
| Next application panel | F6 | F6 |
| Previous application panel | Shift+F6 | Shift+F6 |
| Find in message log | Ctrl+F | Cmd+F |
| Find next occurrence (active search) | Ctrl+G or F3 | Cmd+G or F3 |
| Find previous occurrence (active search) | Ctrl+Shift+G or Shift+F3 | Cmd+Shift+G or Shift+F3 |
| Close search (search input or transcript focused) | Escape | Escape |
| Export the message log | Ctrl+S | Cmd+S |
| Clear the message log | Ctrl+Shift+X | Cmd+Shift+X |
| Reset selected command options | Ctrl+R | Cmd+R |
| Toggle dark mode | Ctrl+D | Cmd+D |
| Inspect the message at the raw-log caret | Ctrl+Enter | Cmd+Enter |

Discover the complete action list and shortcuts in **Actions**, with dark mode under **View**. Contextual hints appear on the Execute button, beside the command dropdown and beside the message-log heading; control and field-label tooltips also show shortcuts. Menus, buttons and shortcuts share their action implementations and availability rules. Shortcuts leave modal dialogs and native file pickers in control.

Dedicated field-focus shortcuts select the current value for replacement. For repeated tests, focus a barcode, enter it, press **F5**, inspect the response and repeat. Execution uses the selected command's existing validation and runs only while connected and idle. **Login** always sends SIP Login without changing the selected command; Connect/reconnect uses the Connect button's validated, cancellable workflow. Holding an execution, connection, login, destructive, export or theme key does not repeat the action.

**F6** and **Shift+F6** cycle forward and backward through Connection, Login, Patron, Item, Command / Options and Messages, wrapping and skipping unavailable panels. Panel navigation preserves field values and selections, keeps normal Tab navigation, and returns to an expanded inspector when navigating to Messages.

**Find** in the message-log header or **Actions → Find in message log** opens AvaloniaEdit's search within the read-only transcript and focuses its input. Reopening selects the existing term. Search highlights sent and received text, supports case-sensitive, whole-word and regular-expression matching, and wraps through results with **Ctrl/Cmd+G / Ctrl/Cmd+Shift+G** or **F3 / Shift+F3**, from either the search input or transcript. **Enter / Shift+Enter** in the search input also navigate forward/backward. Navigation is inactive when search is closed. New messages remain searchable without pulling the viewport away from an active search. Escape in search or the transcript closes search and retains the current selection; automatic scrolling resumes on the next appended transaction. Escape in the inspector retains its usual collapse behavior. Replacement is unavailable.

Clearing a nonempty log and resetting modified command options require confirmation, with **Cancel** focused by default. Empty logs and default options need no confirmation. Reset affects only the selected command, including its range, fee or notice fields, while preserving shared connection, patron and item details and other commands' options. Clearing closes stale search results and inspectors. **Actions → Export message log** shares the Export log button's save dialog.

## Command options

Only the selected command's options are shown. Labels sit beside their inputs when the workbench has enough room. Fields stack beneath their labels when a long caption, date value or dropdown choice needs the extra width. The rows adjust when the divider is dragged. All command options stay in the running session while switching commands or themes. Each command has a **Reset … options** button that affects that command's fields. New command options are session-only; saved defaults and `.sipc` files continue to cover the existing shared settings.

Avalonia uses compact control sizes and spacing to keep all twelve commands visible without scrolling at the minimum window size with the default workbench width. Its ribbon aligns the field rows and action buttons, and the editable vendor profile has an explicit dropdown arrow. Date/time controls share a single border around their checkbox, text and calendar button. Default settings use labels beside their values; character encoding offers UTF-8 and ISO-8859-1 plus typed encoding names. Field-table headings track the actual row width, including when a scrollbar appears; parent fields are shaded and set/invalid flag values receive emphasis. Notice choices use readable labels.

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

In Windows Forms, **View → Dark mode** switches the current window and open dialogs immediately. The connection, current command values, message history and log selection stay in place; existing sent/received colors update with the theme. The choice is saved per user in `%LOCALAPPDATA%\Clc\SipTestingTool\winforms-appearance.json`. The theme checkbox in default settings also applies immediately; **Save and Close** remembers that choice. A user's existing `dark_theme_enabled` setting is used until a per-user theme choice has been saved.

The checkout date/time field and its calendar popup follow the selected theme, including on Windows 10. Check the field to include it, type `yyyy-MM-dd HH:mm:ss`, or choose a calendar date while retaining the time. **Up/Down** adjusts the date/time part at the caret, **F4** or **Alt+Down** opens the calendar, and **Escape** restores the last valid date. Calendar arrow keys move by day/week, **Page Up/Down** by month, and **Enter** selects the focused date. Incomplete or impossible checked dates stop checkout with a validation message; unchecked dates are omitted.

Avalonia also provides **View → Dark mode** and previews theme changes in the settings dialog; cancelling restores the original theme. Its field table uses virtualized rows, selectable cells and **Copy table** for tab-separated output. **Inspect message fields** in the raw log's context menu inspects the message at the caret. Hover and pinned outlines use a separate drawing layer so moving the pointer preserves text, selection and colors.

The Avalonia app uses cancellable asynchronous connection attempts with a **two-minute connection timeout**, including name resolution. **Cancel** appears beside Connect while an attempt is pending; cancelling or a failed attempt releases the connection controls for an immediate retry. The log and window remain usable while connecting, and errors distinguish name-resolution failures, refused connections and unreachable networks. SIP commands permit one operation at a time and retain a 30-second response timeout. The app supports the existing encoding/checksum settings, light/dark themes, Always on Top, settings import/export and log copy/export. Saved defaults live outside the installation in the current user's application-data directory: `%APPDATA%\Clc\SipTestingTool\settings.json` on Windows, normally `~/.config/Clc/SipTestingTool/settings.json` on Linux. Use **Settings → Import settings** to bring across a `.sipc` file from the Windows Forms app. Defaults start with automatic connection disabled and empty connection details.

The console project includes `Examples/acs-status.json`. Supply a JSON filename or inline JSON as its command-line argument. Existing field aliases, `{now}` placeholders, and success regular expressions are supported.

## Avalonia publishing and updates

Select **Windows-Zip** in Visual Studio's Publish page for the Windows release. The finished ZIP contains one `sip-testing-tool-avalonia.exe`; extract it to a writable folder and run it. **Windows-SingleFile** publishes the same update-capable EXE directly when a ZIP is unnecessary.

| Profile | Finished output | Run |
| --- | --- | --- |
| `Windows-Zip` | `artifacts/avalonia/releases/win-x64` | Extract `SIP-Testing-Tool_win-x64_v<version>.zip` and run its EXE |
| `Linux-AppImage` | `artifacts/avalonia/releases/linux-x64` | `SIP-Testing-Tool_linux-x64_v<version>.AppImage` |
| `Windows-SingleFile` | `artifacts/avalonia/publish/win-x64-single-file` | The EXE directly |
| `macOS-arm64` / `macOS-x64` | `artifacts/avalonia/publish/osx-<architecture>` | Use the Mac build instructions below for an app bundle |

**The .NET runtime is excluded from the Windows and Linux releases.** Install the [x64 .NET 10 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) on Windows, or the [x64 .NET 10 Runtime for your Linux distribution](https://learn.microsoft.com/en-us/dotnet/core/install/linux). The Windows Desktop Runtime or SDK also satisfies the Windows requirement. Avalonia's native graphics/text libraries are included; the Windows single-file bundle extracts them into .NET's temporary cache at startup.

Visual Studio's folder link opens the finished output shown above. Profile paths stay relative to the application project because Visual Studio also reads profiles as XML streams without their file location. `PublishPaths.props` supplies the repository root for internal staging. Choosing another destination in Visual Studio, or passing `-p:PublishDir=...`, sends the finished ZIP/AppImage there. Raw files for packaging stay under `artifacts/avalonia/staging/<runtime>`. Packaging requires PowerShell 7 (`pwsh`). AppImage packaging runs on Linux and downloads pinned, SHA-256-verified versions of appimagetool and the native AppImage launcher into `artifacts/tools/appimage`.

Equivalent commands from the repository root:

```text
dotnet publish src/sip-testing-tool-avalonia -c Release -p:PublishProfile=Windows-Zip
dotnet publish src/sip-testing-tool-avalonia -c Release -p:PublishProfile=Linux-AppImage
dotnet publish src/sip-testing-tool-avalonia -c Release -p:PublishProfile=Windows-SingleFile
```

Run the Linux command on Linux. Keep the AppImage in a writable location, mark it executable once (`chmod +x <file>.AppImage`), and launch it. The native AppImage launcher supports extraction mode (`APPIMAGE_EXTRACT_AND_RUN=1`) on systems without FUSE. Both packaging profiles produce a download and a matching `.sha256` sidecar. Local publishing can rebuild the same version; the release workflow refuses to replace a published GitHub release.

Local builds, Visual Studio publishing and the standalone Mac build default to **`0.0.0-dev`**, with optional SDK-added commit metadata. They work without Git tags or GitHub Actions variables, and do not infer a release version from local tags. Development packages keep that identity in their filenames and title and skip automatic update requests, including when packaged as an EXE or AppImage. For a deliberately versioned manual publish, append an explicit MSBuild version, for example `dotnet publish src/sip-testing-tool-avalonia -c Release -p:PublishProfile=Windows-Zip -p:Version=3.0.0-beta.6`. Do not edit the project file to increment a release version. Packaging verifies the embedded application version before producing a download.

The app uses [StageKit.Updatum](https://github.com/sn4k3/StageKit/tree/v0.4.1/src/StageKit.Updatum) to download and install updates from published [GitHub Releases](https://github.com/clcdpc/sip-testing-tool/releases). Stable builds receive stable releases only. Prerelease builds, such as `2.1.2-beta.1`, receive newer prereleases and stable releases; installing a stable release returns the app to stable-only updates. Version selection follows [semantic version precedence](https://semver.org/), so `beta.2` precedes `beta.10`, and the final `2.1.2` follows every `2.1.2` prerelease. Commit/build metadata does not affect ordering. The app reads its full informational version to retain the beta identifier, and selects the highest compatible version regardless of publication order.

**Settings → Change default settings → GitHub update repository** controls the source; leave it empty to disable checks. Existing settings with the old default web feed migrate to this repository. Startup checks can be disabled independently, and **Updates → Check for updates** checks manually. Development builds and the current Mac builds skip update requests; the published Windows EXE supports updates without an installer.

Available updates download in the background and require a verified SHA-256 digest from GitHub or the matching checksum sidecar. The updater verifies the download again before installation. **Restart to update** is available when the download is ready and no SIP operation or update check is running. Preferences are saved before starting the helper, which waits for this specific process to exit, replaces the existing EXE/AppImage while retaining its filename, and restarts it. Closing normally without choosing restart discards the temporary download. User defaults retain their per-user location.

Updatum uses temporary download/helper files and a temporary backup during replacement; the Windows application folder contains only its EXE. A failed file swap restores the previous file, but there is no automatic rollback if the new app starts and then crashes. Updatum 0.4.1 may show its temporary Windows command window during restart. Packages remain unsigned until application signing is configured. Existing Velopack and ClickOnce installations require a one-time move to the new portable release; their old updater cannot consume this format.

### GitHub release builds

The **Desktop release** GitHub Actions workflow packages the primary Avalonia app when a `vMAJOR.MINOR.PATCH` tag, optionally followed by a prerelease identifier, is pushed. **The triggering Git tag is the sole release-version authority.** The workflow validates it once through `build/ReleaseVersion.ps1`, removes the leading `v`, and passes the resulting version explicitly as `-p:Version` to builds, tests and publishing on all four platforms. For example, `v3.0.0-beta.6` embeds `3.0.0-beta.6` in the application without changing any project file. Alpha, beta and release-candidate identifiers follow semantic version syntax; numeric identifiers cannot have leading zeroes (`beta.1`, not `beta.01`). Build metadata is also accepted and retained, but changing only metadata does not create a newer update. SDK-added source revision metadata remains valid SemVer and does not affect version precedence or the title.

The workflow builds Windows x64 on `windows-2025`, Linux x64 on `ubuntu-22.04`, macOS Apple Silicon on `macos-15` and macOS Intel on `macos-15-intel`. All four jobs run the Avalonia tests and publish-profile checks; Windows also runs the shared protocol and Windows Forms tests. Verification checks the compiled informational version against the tag before publishing, and packaging checks the payload again. The existing profiles package the single-EXE ZIP and framework-dependent AppImage. Each Mac job publishes its self-contained profile and packages **`SIP Testing Tool.app`** as `SIP-Testing-Tool_osx-arm64_v<version>.zip` or `SIP-Testing-Tool_osx-x64_v<version>.zip`, including the .NET runtime. Mac bundles use the same packaging script as the standalone Mac build below. Before upload, verification checks the expected asset names, embedded versions, archive/AppImage structure and SHA-256 sidecars; Linux verification also extracts the AppImage and checks `X-AppImage-Version`, and Mac verification checks the executable architecture, bundle version, bundled runtime and extracted code signature. Both Mac bundle version fields use only `MAJOR.MINOR.PATCH`; the managed assembly and asset filename retain the full prerelease version and tag metadata. Windows native EXE version resources are checked on Windows; the final Linux job rechecks that ZIP's structure and checksum. Keep the `SIP-Testing-Tool_<rid>_v<version>` asset naming convention: the Windows/Linux updater selects the appropriate runtime and format from those names.

After all four jobs succeed, a final job verifies all downloads again, creates a temporary **draft** GitHub Release and uploads exactly eight assets: Windows ZIP, Linux AppImage, two macOS ZIPs, and their four checksum sidecars. After every upload succeeds, it **publishes automatically**. A build, verification or upload failure prevents publication; an upload failure leaves the draft available for investigation and retry. Prerelease versions automatically set GitHub's **Pre-release** flag and are explicitly excluded from **Latest**. The full version suffix remains in all download names and their checksum sidecars. The workflow uses the automatically supplied `GITHUB_TOKEN`, with `contents: write` limited to the release job. A rerun can refresh an unfinished draft; it refuses to replace a published release.

The Mac ZIPs require **macOS 14 or later**. Extract the ZIP for your Mac's architecture, then copy **SIP Testing Tool.app** to Applications. The app has an **ad-hoc signature**, with no Developer ID certificate or Apple notarization configured; macOS Gatekeeper can block the first launch of an internet download. If you trust the release, follow [Apple's instructions](https://support.apple.com/en-us/102445) to approve it in **System Settings → Privacy & Security → Open Anyway** after attempting to open it. Developer ID signing and notarization require an Apple Developer account and signing/notarization credentials in GitHub Actions secrets. Mac apps update by downloading a newer release and replacing the app; in-app updates remain available on Windows/Linux.

To release, merge the desired changes into `master`, then create a new semantic-version tag against the intended commit and push it. GitHub Actions builds with the tag-derived version, verifies the artifacts and publishes automatically. **Pushing the tag approves automatic publication**; no separate GitHub Publish button is required. For example:

```text
git switch master
git pull --ff-only
git tag -a v3.0.0-beta.6 -m "Release 3.0.0-beta.6"
git push origin v3.0.0-beta.6
```

Use a new tag after merging this versioning change; do not move or reuse `v3.0.0-beta.5`, which points to the failed release workflow's original commit.

After publication, check all four downloads, including launch on both Mac architectures and Windows/Linux two-version update checks. For the beta update test, run an older published prerelease EXE/AppImage, choose **Updates → Check for updates**, then **Restart to update**; the restarted title should show the new tag's version. Drafts are always excluded from in-app updates; published prereleases are offered only to prerelease builds. Published releases are available to eligible portable builds using that GitHub repository; no separate update server, index, installer or delta package is needed. The initial beta.1 tag used the earlier workflow and retains its manual draft-publication step.

## Build on a Mac

Download [build-macos.command](https://raw.githubusercontent.com/clcdpc/sip-testing-tool/master/build-macos.command) to a Mac running macOS 14 or later. In Terminal, change to the folder where you want the build files, then run the downloaded script. For example, if it is in Downloads:

```bash
cd ~/Downloads
bash ./build-macos.command
```

The script downloads **master** from the public repository, detects Intel or Apple Silicon (including Terminal running under Rosetta), downloads a private [.NET 10 SDK](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-install-script), and builds the Avalonia app. Internet access is required. Git, Homebrew, Xcode and a preinstalled .NET SDK/runtime are unnecessary; the SDK stays in the build folder and the resulting app includes its .NET runtime. The macOS changes must be merged into `master` before sharing this script.

Everything downloaded or built stays under **`sip-testing-tool-macos` in the working directory**. Each run gets a new `build-…` subfolder containing the source, `build.log` and **`SIP Testing Tool.app`**. The script creates its icon, signs the local build with an ad-hoc signature, verifies the bundle and reveals it in Finder. Open the app there, or copy it to Applications. `sip-testing-tool-macos/latest-app.txt` records the latest successful app path.

The bundle keeps native executables in `Contents/MacOS`, native libraries in `Contents/Frameworks`, and managed DLLs and data in `Contents/Resources`. Relative links expose the publish layout from all three directories so .NET can resolve assemblies, native libraries and runtime helpers, including after moving or copying the app. This follows the [signed-bundle layout rules](https://docs.avaloniaui.net/docs/deployment/macos/#sandbox-and-bundle); copying managed DLLs directly into `MacOS` causes `codesign` to reject them as unsigned nested code. After updating the build script, download it again before rerunning it; an older downloaded copy retains its original packaging steps.

Run the same script again to build the latest `master`; the private SDK and NuGet cache are reused, and previous builds are preserved. These locally built Mac apps update by rerunning the script. They do not use in-app updates and are not Developer ID/notarized downloads.

The `macOS-arm64` and `macOS-x64` publish profiles also support direct `dotnet publish` commands with an existing .NET 10 SDK. They produce self-contained application folders under the repository's `artifacts/avalonia/publish` directory. The standalone script adds the [macOS `.app` packaging](https://docs.avaloniaui.net/docs/deployment/macos), icon and local signing on the Mac through `build/PackageMacOS.sh`. To create the same ZIP and checksum as GitHub Actions with an existing SDK and PowerShell 7 on a Mac, run:

```powershell
$version = '0.0.0-dev' # Set an explicit version here for a deliberately versioned manual publish.
dotnet publish src/sip-testing-tool-avalonia -c Release -p:PublishProfile=macOS-arm64 -p:Version=$version
./build/PackageDesktop.ps1 -RuntimeIdentifier osx-arm64 -PublishDirectory artifacts/avalonia/publish/osx-arm64 -OutputDirectory artifacts/avalonia/releases/osx-arm64 -Version $version
./build/VerifyDesktopRelease.ps1 -Directory artifacts/avalonia/releases/osx-arm64 -RuntimeIdentifier osx-arm64 -Version $version
```

Use `macOS-x64` and `osx-x64` for Intel. Pass the same version to publishing, packaging and verification; mismatches are rejected. The standalone build of `master` also uses the development version.

## Legacy Windows Forms publishing

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

Both test projects use MSTest.Sdk 4.5.1. Open `src/sip-testing-tool.sln`, build the solution, then use **Test > Test Explorer > Run All**. Windows Forms UI tests run on an STA thread; Avalonia UI tests use its headless dispatcher. Each assembly runs sequentially because its checks share UI and process state.

From the repository root, the equivalent command is:

```text
dotnet test src/sip-testing-tool.sln
```

Run `dotnet msbuild build/VerifyPublishPaths.proj` to check all eight publish profiles, including Visual Studio's XML-stream reader that has no profile file location. This checks their output destinations without building, signing or packaging a release.

Run `pwsh -File build/TestReleaseVersion.ps1` with the .NET 10 SDK to check semantic tags, development defaults, version injection through all four release profiles, compiled informational-version fixtures, package names and checksums, and rejection of inconsistent payload versions. It creates no Git tags or GitHub releases. To verify an application build directly, use `pwsh -File build/VerifyDesktopRelease.ps1 -ApplicationPath <application.dll> -Version <expected-version>`.

Run `bash build/TestMacBuild.sh` on macOS, Linux or Git Bash to exercise the standalone Mac bootstrap with local tool/download fixtures. These checks cover architecture selection, paths with spaces, signed-bundle directory rules, relative links after copying the app, preserving previous builds and failure handling. Git Bash uses MSYS symbolic-link emulation for these fixtures. They use no network or installed SDK and leave diagnostics under `artifacts/mac build tests-…`; actual macOS launch and code-signing verification still require a Mac.

Tests are ordinary MSTest methods grouped by behavior: checksums, connections, console output, workbench behavior, checkout, themes, command-option state, settings, updates and desktop sessions. Test classes use MSTest lifecycle methods; the shared test support is limited to UI setup, test data and loopback SIP peers. Both projects use MSTest assertions directly.

The tests use loopback SIP servers and Avalonia's headless platform; they never contact a production SIP endpoint. Windows Forms tests submit through public buttons and verify the captured packets and message log.

The **Desktop tests** pull-request workflow runs the complete Avalonia suite on Windows, Linux and macOS. Shortcut tests send headless keystrokes, capture SIP requests, check focus and selection, exercise export through its storage provider, and verify theme persistence and modal safety. The gesture factory also tests Control and Command deterministically on every platform. Run `dotnet test tests/sip-desktop-tests/sip-desktop-tests.csproj --configuration Release` locally; Windows also supports `dotnet test tests/sip-tests/sip-tests.csproj --configuration Release` for the shared protocol and legacy UI checks.

Avalonia layout checks in `DesktopParityTests.Layout.cs` render every command in both themes at default and minimum window sizes, check for unnecessary scrolling and clipped choices, and verify ribbon, date-editor, settings and field-table alignment. Their PNG previews are saved under `artifacts/layout-comparison/avalonia`. They share the existing parity test class's UI session because AvaloniaEdit key bindings retain dispatcher ownership.

`tests/sip-tests/FormSubmissionTests.cs` and `FormSubmissionTests.Cases.cs` contain parameterized form-to-submission checks for all twelve commands. Each case edits real form controls, uses `Button.PerformClick()` to reset and submit, captures the actual request at a loopback TCP server, and compares fixed and variable fields with independent expected values. These cases exercise every command option, dropdown choice, typed date overrides and unchecked dates, optional-field omission, numeric boundaries, decimal culture handling, both Login buttons, updated ribbon values, checksums and transcript entries. These checks verify client wiring and serialization; acceptance by a particular SIP server still depends on that server's implementation and configuration.

Generated output is ignored by Git. The original Git history is preserved. This consolidated version combines the .NET 10 app with ACS Status, Always On Top, configurable encoding/checksum validation, and connection-loss handling from the older project.

## Code style

Prefer straightforward control flow, descriptive names, and small methods with one purpose. Keep simple calls and expressions together; wrap at natural boundaries when it helps the reader, without enforcing a hard line length. The root `.editorconfig` supplies the shared C# formatting rules used by Visual Studio. Keep generated designer files under the designer's control. Windows Forms tests use the shared type-specific accessors in `TestSupport/ControlLookupExtensions.cs`; use a descriptive local variable when a scenario reuses a control.

To apply or check formatting from the repository root:

```text
dotnet format whitespace src/sip-testing-tool.sln
dotnet format whitespace src/sip-testing-tool.sln --verify-no-changes
```
