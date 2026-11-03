# Axure RP 8.0–8.1 Compatibility and Feature Scope

## Target range and verified build

| Item | Target / verified information |
|---|---|
| Project target family | Axure RP 8.0–8.1 |
| Currently verified binary set | Windows build 3400, identified by the SHA-256 hashes below |
| Official release history label | 8.1.0.3400, May 11, 2022 |
| FileVersion / ProductVersion of all six local files | 8.0.0.3400 |
| Operating system | Windows desktop |
| Host architecture | x86 |
| UI technology | WPF / .NET Framework 4.x |
| Command client | Python 3 |
| RP files | .rp files read and written by the installed Axure RP application |

**Important:** 8.0–8.1 is the project target range. Only the local build 3400 binary set listed below has been verified, with fixed SHA-256 checks at startup. Other binary sets and builds remain unverified; the adapter refuses to start when a hash differs. This does not mean every 8.0–8.1 installation is supported.

### Release label versus file version

[Axure's official release history](https://www.axure.com/release-history/rp8) labels build 3400 as **8.1.0.3400 (May 11, 2022)**. A read-only inspection of the six local files below on 2026-11-03 found **8.0.0.3400** in both `FileVersion` and `ProductVersion`. `Get-AuthenticodeSignature` returned `Valid` for all six, with **Axure Software Solutions, Inc** as the signer. Their SHA-256 hashes matched the table below.

Earlier documentation incorrectly treated the `8.0.0.3400` file version as the official release name. The valid signatures and hashes identify these specific binaries; the version-field difference alone does not establish that their version resources were modified. This inspection did not download and extract a fresh official installer for a byte-for-byte comparison, so it does not equate the local binary set with every distribution labeled `8.1.0.3400`.

To inspect your installation, replace the path below and repeat for the other five files:

```powershell
$Binary = 'D:\Apps\Axure RP 8\AxureRP8.exe'
(Get-Item -LiteralPath $Binary).VersionInfo | Select-Object FileVersion, ProductVersion
Get-AuthenticodeSignature -LiteralPath $Binary | Select-Object Status, SignerCertificate
Get-FileHash -LiteralPath $Binary -Algorithm SHA256
```

The host checks these Axure files:

| File | SHA-256 |
|---|---|
| AxureRP8.exe | DDDE5C4EC3281F4C0522A38DD38E7498B0D70FFDF126E7F8916C212A109FD279 |
| Client.dll | 4AD07217D548C6F9E5E17AAAA1C55E627B0F4F1855E203A946D88881B0CCF109 |
| Client.Win32.dll | 9D7F4FAA0B8C3C02CFE125A32F4A123B2CF2F91EDC080074C715EE63A7215074 |
| Model.dll | 58B45B074F502EBE4B345346BDF8AC8680F7A9A4A8C53693922CE4AA2D4B3C65 |
| Platform.dll | D53B20B6658932BEF477E592FAAC5A028EF3B8BF482B77F0E7E8C9D1E2A83A87 |
| Platform.Win32.dll | 3C59611656496F8BF00DB641C4D92E5BC172461288BE2BF570440363486F12D6 |

Treat any hash mismatch as incompatible. RP 9, RP 10, RP 11, macOS, Linux, Axure Cloud, and other RP8 builds are unverified.

## Implemented commands

- Session: status, capture, close, undo, redo.
- Pages: pages, page.open, page.reveal, page.create, page.rename, page.content, page.polish.
- Widgets: widgets, widget.create, widget.update, widget.group, widget.select, widget.style, widget.path.
- Interactions: interactions, interaction.add, interaction.replaceActions, interaction.repairText.
- Output: save, prototype.export.

page.removeEmpty and frame.removeOld have narrowly scoped cleanup guards. Read their source protections before use; they are not general-purpose deletion operations. methods diagnoses editor methods discovered through reflection and is not a stable API.

## Widget types and styles

widget.create supports: button, input, checkbox, image, rectangle, paragraph, heading, ellipse, vector, hline, and vline. Image input is limited to local PNG/JPEG files.

The widget.style allowlist includes: fontFamily, fontSize, fontWeight, color, fill, borderColor, borderWidth, radius, align, valign, four-side padding, rotation, opacity, visible, and shadow.

## Interaction scope

Supported actions include setting/copying text, checkbox state, restricted native expressions, show/hide/toggle, enable/disable, move, resize, page navigation, firing another event, and short waits. Event names come from RP8 native enums and are checked against the events supported by the target widget.

Not implemented or not verified as general features: dynamic panel state changes, repeaters, arbitrary page nesting, adaptive views, all Axure expressions, every interaction action, general-purpose widget deletion, and independent undo semantics for text/styles/interactions. Native undo/redo for adding widgets has been verified; save a working copy frequently for other changes.

## Existing verification record

Local records from 2026-10-24 include 25 checks for visible-window behavior, editing without foreground focus, save/Save As, widget ID readback, and focus protection. The interaction workflow also records 15 edit/persistence checks and 8 browser-preview checks. These results apply only to the binary hashes above and the local samples used at the time; they do not guarantee other binary sets or every operation. The version, signature, and hash inspection on 2026-11-03 rechecked file identity; it did not rerun those functional checks.
