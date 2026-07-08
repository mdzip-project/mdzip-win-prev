# mdzip-win-prev
MDZip previewer for Windows file explorer

## Overview

`mdzip-win-prev` is a Windows Shell **preview handler** for `.mdz` (MarkdownZip)
files.  When a `.mdz` file is selected in Windows File Explorer (or any
preview-handler–aware host such as Outlook or Total Commander), the handler
renders the entry-point Markdown document and displays the HTML result directly
in the preview pane — no external app required.

> **Document mode only.** Archives whose `manifest.json` declares
> `"mode": "project"` are not rendered; the preview pane shows an
> informational message instead.

## Architecture

| Component | Description |
|-----------|-------------|
| `MdzPreviewHandler` | COM class implementing `IPreviewHandler`, `IInitializeWithFile`, `IOleWindow`, and `IPreviewHandlerVisuals` |
| `MdzRenderer` | Reads an `.mdz` archive using [`mdzip-core`](https://github.com/mdzip-project/mdzip-core) and converts the entry-point Markdown to HTML via [Markdig](https://github.com/xoofx/markdig) |
| `PreviewPanel` | WinForms `UserControl` that hosts a `WebBrowser` control to display the rendered HTML |

Assets (images, stylesheets) referenced by the entry-point document are
extracted to a temporary directory; a `<base>` tag in the rendered HTML
ensures relative paths resolve correctly.

## Requirements

- Windows 10 or Windows 11 (x64)
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)

## Building

```powershell
dotnet build src/mdz.WinPrev/mdz.WinPrev.csproj -c Release
```

For the Windows COM host (`mdz.WinPrev.comhost.dll`) used by the registration
scripts, build on Windows:

```powershell
dotnet publish src/mdz.WinPrev/mdz.WinPrev.csproj -c Release -f net8.0-windows -r win-x64 --self-contained false
```

## Installation

Run the following from an **elevated** (Administrator) PowerShell session:

```powershell
.\scripts\install.ps1 -DllPath "<path-to>\mdz.WinPrev.comhost.dll"
```

To register a custom Explorer icon for `.mdz` files, pass an `.ico`, `.exe`, or
`.dll` icon source:

```powershell
.\scripts\install.ps1 -DllPath "<path-to>\mdz.WinPrev.comhost.dll" -IconPath "<path-to>\mdzip.ico"
```

When `-IconPath` is omitted, the installer uses the sibling
`..\mdzip-mark\ico\mdzip-mark-square.ico` icon if it is present.

The script:
1. Registers the CLSID `{CA7A244F-7A83-4B5E-9D7A-9F13EF5E8B3A}` under `HKCR\CLSID`.
2. Adds the preview handler to the `.mdz` file extension shell extension keys.
3. Adds the handler to the Windows Preview Handler list.
4. Registers a shell icon for the active `.mdz` ProgID.
5. Notifies the shell of the change.

You may need to restart Windows Explorer (`taskkill /f /im explorer.exe && start explorer`) for the change to take effect.

## Uninstallation

```powershell
.\scripts\uninstall.ps1
```

## Development

```powershell
# Build
dotnet build

# Run tests
dotnet test src/mdz.WinPrev.Tests/mdz.WinPrev.Tests.csproj
```

## Dependencies

| Package | Purpose |
|---------|---------|
| [`mdzip-core`](https://www.nuget.org/packages/mdzip-core) `1.3.0` | .mdz archive reading, manifest parsing, entry-point resolution |
| [`Markdig`](https://www.nuget.org/packages/Markdig) `0.40.0` | Markdown-to-HTML rendering with advanced extensions (tables, code highlighting anchors, …) |

## License

Apache-2.0 — see [LICENSE](LICENSE).
