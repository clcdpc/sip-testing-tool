# SIP project consolidation

Consolidated on October 20, 2026. This folder is the development source for the Windows app, SIP library, and console tool. Both source folders remain available for manual archival.

## Source mapping

The .NET 10 work in `sip-testing-tool-netcore` supplies the app and console foundation. The older `sip-testing-tool` supplies the Git history, additional app features, defensive response handling, and the existing Newtonsoft.Json 13.0.4 package.

| Original location | Consolidated location |
| --- | --- |
| `sip-testing-tool-netcore/src/sip-testing-tool` | `src/sip-testing-tool`; `Form1.cs`, its designer, and its resource file are renamed to `MainForm.*` to match the existing class. |
| `sip-testing-tool-netcore/src/sip-net-core` | `src/sip-core`; converted to the single shared .NET 10 library. |
| `sip-testing-tool-netcore/src/sip-console-netstandard` | `src/sip-console`; the console namespace and executable name use `sip_console` and `sip-console`. |
| `sip-testing-tool/.git` | Independent Git repository retaining the original commits through `320e3ce` and their ancestors, with a consolidation branch and commit. |
| Signing files and legacy console settings from both folders | Deduplicated into ignored `local/signing` and `local/imported-settings`. |
| Original publish profiles and per-user publication metadata | Ignored `local/original-publish-profiles`. |

## Restored behavior

- ACS Status is restored in both the shared client and the desktop command buttons.
- Always On Top uses a Windows Forms context menu supported by the current app.
- Encoding and checksum preferences now reach the shared client. UTF-8 remains the default; configured legacy code pages are supported.
- Empty and short responses fail checksum validation without substring exceptions. TCP failures and peer closure produce the connection-loss result expected by the desktop app.
- The public checksum helper and two-argument SIP client constructor remain available. Checksums use the encoded message bytes and produce four hexadecimal digits, including for supplementary Unicode characters.
- Console usage handling and the legacy PRTG boolean lookup are retained. XML output escapes channel names. The command sequence uses the ignore attribute for its actual JSON serializer.
- The desktop app loads saved defaults at startup, validates the port, and releases the shared client when reconnecting or closing.

The modern library's existing command defaults are retained, including its `000` language setting and existing field construction. The consolidation does not change the SIP command set or migrate runtime credentials automatically.

## Removed duplication

The blank .NET 6 starter app, obsolete .NET Framework projects, duplicated SIP libraries, old project-file backup, and missing `sipmaui` entries are excluded from the active solution. The `sip-core.zip` source files matched the older folder's core source exactly, so the archive adds no unique source.

Visual Studio caches, generated `bin` and `obj` folders, downloaded package directories, and old published binaries are excluded from the imported source. The active projects have relative project references and no dependency on either original directory. Strong-name signing paths and the unused Roslyn package are removed from development builds.

## Local preservation

`local/imported-files.json` lists the five unique signing/settings files and their hashes. `local/source-inventory.json` records input-file hashes. `local/legacy-working-tree.patch` captures the older repository's tracked staged and unstaged changes relative to its last commit. The authoritative source includes the useful modern console files that were untracked in that repository.

The active ClickOnce profile retains the prior install URL, application version `2.1.0.*`, and next revision `8`. Output paths now point inside this folder. Original profiles retain the former publication destinations for reference. No publication or server connection is part of consolidation validation.

## Validation

The Release solution build completes with zero warnings and zero errors. All 17 regression checks pass, including local TCP exchanges, ACS Status, connection loss, configured encoding, optional checksum checking, console JSON/PRTG behavior, and construction of the main and supporting Windows forms with their resources. Live SIP server behavior and ClickOnce signing require the intended server and signing environment.
