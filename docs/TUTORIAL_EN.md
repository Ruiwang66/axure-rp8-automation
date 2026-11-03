# Axure RP 8.0–8.1 Detailed Usage Tutorial

This tutorial targets the Axure RP 8.0–8.1 product line. The current adapter has been verified only with a specific Windows build 3400 binary set and is pinned to its SHA-256 hashes. Axure's release history labels build 3400 as `8.1.0.3400`, while the signed local files report `8.0.0.3400` in `FileVersion` / `ProductVersion`; see the [compatibility notes](COMPATIBILITY_EN.md) for the distinction and verification limits. Other binary sets remain unverified. For your first run, use a disposable test project.

## 1. Prerequisites

You need:

- An authorized Axure RP 8 installation whose six program files match the [verified binary hashes](COMPATIBILITY_EN.md).
- A Windows desktop environment.
- The .NET Framework 4.x C# compiler included with Windows, at %WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe.
- Python 3 available through PowerShell as py -3.
- Write access to the Axure installation directory. The build creates RP8Live.next.exe and 打开独立RP8.exe there.

The repository does not include Axure's EXE, DLLs, configuration, icon, or sample .rp projects. The build also reads AxureRP8.exe.config and App.ico from your Axure installation directory.

## 2. Clone and build

Clone the repository into an rp8-automation subdirectory under the Axure installation directory. The build script expects this directory relationship to locate the Axure files:

```powershell
$AxureRoot = 'D:\Apps\Axure RP 8' # Replace with your actual installation path
$Repo = Join-Path $AxureRoot 'rp8-automation'
git clone https://github.com/Ruiwang66/axure-rp8-automation.git $Repo
Set-Location $Repo
.\live\build.cmd
```

On success, these files appear in $AxureRoot:

- RP8Live.next.exe: dedicated process hosting Axure RP 8 and the native editing adapter.
- RP8Live.next.exe.config: copied from the original AxureRP8.exe.config.
- 打开独立RP8.exe: copies the input project into an isolated work directory and launches the dedicated host.

The build fails if the compiler, App.ico, or Axure configuration is missing. Check the installation directory and version. Do not replace files with DLLs from another version.

## 3. Start an isolated working copy

Pass the path of an existing .rp file to the launcher:

```powershell
$Source = 'D:\Prototypes\source.rp'
& (Join-Path $AxureRoot '打开独立RP8.exe') $Source
```

The launcher copies the project to rp8-automation\live\projects\<session>\AI实时设计.rp and opens that copy. The source project is not the target of this automation session.

If the dedicated RP8 instance is already running, the launcher reuses it and does not switch to the newly supplied .rp file. To switch projects, make sure the current project is saved, safely close the dedicated instance through the command, then launch it again. Without an argument, the launcher tries to open live\实时设计演示.rp; that demo project is not distributed in the public repository, so normally pass your own file explicitly.

## 4. Send the first command

The CLI accepts a JSON string or a UTF-8 JSON file. A file is easier for Chinese text and longer requests:

```powershell
$Cli = Join-Path $AxureRoot 'rp8-automation\live\live_cli.py'
py -3 $Cli '{"operation":"status"}'
py -3 $Cli --file '.\examples\requests\pages.json'
```

The response is JSON. The ok field indicates success; a failure includes error. The default wait for RP8 is up to 20 seconds. After a timeout, inspect the session and matching request/result before resending a command to avoid creating duplicate widgets.

Common read-only requests:

```powershell
py -3 $Cli --file '.\examples\requests\status.json'
py -3 $Cli --file '.\examples\requests\pages.json'
py -3 $Cli --file '.\examples\requests\widgets.json'
```

- status returns host readiness, the current file and page, unsaved-change state, and window-focus audit details.
- pages returns page GUIDs. After calling page.open, query status to confirm the current page name.
- widgets returns the current page's top-level widgets and children. Each widget includes a stable GUID, name, type, text, and bounds.

## 5. Open a page and create a widget

Use a GUID returned by pages with page.open:

```json
{"operation":"page.open","id":"REPLACE_WITH_PAGE_GUID"}
```

Create a native button:

```json
{
  "operation": "widget.create",
  "kind": "button",
  "name": "btnContinue",
  "text": "Continue",
  "x": 120,
  "y": 180,
  "width": 160,
  "height": 48
}
```

Keep the returned id; use this GUID for later edits and interactions. Other supported kinds are paragraph, heading, rectangle, ellipse, vector, hline, vline, input, checkbox, and image. Images require a local PNG/JPEG path and are embedded in the target RP project.

Update a widget's text and position:

```json
{
  "operation": "widget.update",
  "id": "REPLACE_WITH_WIDGET_GUID",
  "text": "Updated button label",
  "x": 140,
  "y": 200,
  "width": 180,
  "height": 48
}
```

Omitted properties keep their existing values. widget.style accepts only the documented style properties in COMPATIBILITY_EN.md.

## 6. Add a native interaction

First use widgets to get the GUIDs for a button and target text widget. Then add an OnClick case to the button:

```json
{
  "operation": "interaction.add",
  "id": "BUTTON_GUID",
  "event": "OnClick",
  "name": "Show welcome text",
  "actions": [
    {
      "kind": "setText",
      "target": "TEXT_WIDGET_GUID",
      "text": "Welcome to Axure RP 8"
    }
  ]
}
```

Supported action kinds include setText, setChecked, copyText, expressionText, visibility, enable, move, resize, openPage, fire, and wait. Events and actions must be supported by the target widget. Expressions must use Axure's native syntax; ordinary JavaScript is not an Axure expression.

interaction.add creates a new case. interaction.replaceActions replaces the action list of the selected case. Before using it, read the current interactions and verify the caseId. Inspect a widget's interactions with:

```json
{"operation":"interactions","id":"WIDGET_GUID"}
```

## 7. Save, capture, and export a preview

Save As to a new path that does not already exist:

```json
{"operation":"save","path":"D:\\Prototypes\\login-edited.rp"}
```

save uses Axure's native save operation and refuses to overwrite an existing target. After saving, check that status.hasUnsavedChanges is false.

Capture the current RP8 window:

```json
{"operation":"capture","path":"D:\\Prototypes\\evidence\\rp8-window.png"}
```

Call Axure's native HTML generator:

```json
{"operation":"prototype.export","path":"D:\\Prototypes\\login-preview"}
```

The export directory must be a new, empty path. After export, inspect navigation, controls, and layout in a browser. A successful export alone does not prove that interactions work.

## 8. Suggested agent prompt

```text
Use the axure-rp8-design workflow with the dedicated Axure RP 8 window I started.
First read status, pages, and widgets for the current page, and confirm the target file and page.
Edit only the working copy created by the launcher. Create native widgets and supported
interactions based on my page requirements. After each batch, read the results and inspect
the page. Save As to a new .rp path, confirm hasUnsavedChanges=false, and list anything
that could not be implemented.
```

## 9. Safety and troubleshooting

- **Version check fails:** The adapter targets RP 8.0–8.1 but accepts only the build 3400 binary hashes listed in the compatibility document. A matching release label or file version does not replace a hash match. Do not remove or bypass the check.
- **RP8 client not ready:** Wait for the dedicated window to finish starting, then send status. Do not send commands to an RP8 instance opened through the normal shortcut.
- **The launcher did not switch files:** The existing dedicated instance is reused and does not switch documents. Save and safely close that instance before launching with another file.
- **CLI timeout:** First inspect live\session.json and the corresponding request result. Do not blindly retry a write request that may already have executed.
- **Save/page-switch results differ:** Save after editing the current page, before switching pages or exiting.
- **Unsupported interaction:** The adapter rejects unknown events, actions, and style properties. Dynamic panel states, repeaters, and adaptive views are not currently implemented.

Runtime session.json, request/response JSON, screenshots, export folders, and .rp files may contain user design information. Keep them local and do not add them to Git commits.
