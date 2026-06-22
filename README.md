# Markdown Printer

Markdown Printer is a desktop-focused .NET MAUI app for turning raw markdown into a print-ready document.

Paste or type markdown on the left, review the rendered result on the right, then open the output preview to:

- save the generated files
- print the rendered document
- switch between portrait and landscape
- increase or decrease the base text size

## Highlights

- Desktop-only targets:
  - `net10.0-windows10.0.19041.0`
  - `net10.0-maccatalyst`
- Side-by-side raw markdown and rendered preview
- Minimal red-themed UI with icon-based actions
- Automatic markdown normalization before preview, save, and print
- Output preview overlay with layout controls
- Save exports both:
  - `.md`
  - `.html`
- Print does **not** save files first
- File names are derived from the first markdown heading when possible

## Requirements

### Windows

- .NET 10 SDK
- MAUI workload installed
- WebView2 runtime available
- Visual Studio 2022 or later with MAUI support recommended

### Mac

- .NET 10 SDK
- MAUI workload installed
- Xcode and Mac Catalyst tooling

## Getting started

1. Clone the repository.
2. Open a terminal in the project folder.
3. Restore workloads/packages if needed.
4. Build and run the app.

## Build

### Windows

```powershell
dotnet build -f net10.0-windows10.0.19041.0
```

### Mac Catalyst

```powershell
dotnet build -f net10.0-maccatalyst
```

## Run

### Windows

```powershell
dotnet build -t:Run -f net10.0-windows10.0.19041.0
```

### Mac Catalyst

```powershell
dotnet build -t:Run -f net10.0-maccatalyst
```

## Distributable builds

The repository now includes packaging scripts and a GitHub Actions workflow for shareable desktop builds.

### Windows distributable

Create a zip that you can send to Windows users:

```powershell
.\scripts\publish-windows.ps1
```

Output:

```text
artifacts\dist\MarkdownPrinter-windows.zip
```

This produces an unpackaged Windows app folder and zips it for sharing.

### Mac distributable

On a Mac with Xcode and the Mac Catalyst workload installed:

```bash
./scripts/publish-maccatalyst.sh
```

Output:

```text
artifacts/dist/MarkdownPrinter-maccatalyst.app.zip
```

This script looks for the generated `.app` bundle and zips it in a Mac-friendly format.

### GitHub Actions build

There is also a manual workflow at:

```text
.github/workflows/build-distributables.yml
```

Run **Build distributables** from GitHub Actions to produce:

- `MarkdownPrinter-windows.zip`
- `MarkdownPrinter-maccatalyst.app.zip`

That is the easiest way to generate both shareable artifacts from the repository.

## How to use

### Main editor

- **Paste** icon: pulls text from the clipboard into the raw markdown pane
- **Clear** icon: clears the current markdown
- **Save** icon: opens the output preview configured for saving
- **Print** icon: opens the output preview configured for printing

### Raw markdown pane

- Paste or type markdown directly
- The editor is intended to fill the panel height before scrolling
- Markdown is normalized automatically as part of preview/export generation

### Rendered preview pane

- Shows the current rendered output as HTML
- Updates as the markdown changes

### Output preview overlay

The output preview is the full-window modal used before saving or printing.

Available options:

- **Page layout**: Portrait or Landscape
- **Base text size**: adjust with the minus and plus icons
- **Save** icon: write files to a selected folder
- **Print** icon: open the system print dialog
- **X**: close the overlay

## Save behavior

When you save:

1. Open the output preview.
2. Adjust layout and text size.
3. Click the save icon.
4. Choose an output folder.

The app writes:

- a normalized markdown file (`.md`)
- a rendered HTML file (`.html`)

The app does **not** silently save into app storage for normal save operations.

Folder selection by platform:

- **Windows**: uses the WinUI folder picker
- **Mac Catalyst**: uses the system document picker in folder mode; the sandbox grants write access to the folder you choose

## Print behavior

When you print:

1. Open the output preview.
2. Adjust layout and text size.
3. Click the print icon.

Behavior by platform:

- **Windows**: prints from the visible WebView-based preview using the system print UI
- **Mac Catalyst**: uses native Apple print services

Printing does **not** save files first.

## Markdown behavior

The app uses Markdig plus a normalization step before rendering/exporting.

Normalization currently helps standardize:

- unordered lists
- ordered lists
- task lists
- headings
- block quotes
- fenced code blocks

This reduces malformed output from inconsistent pasted markdown.

## File naming

The output file name is generated from the first markdown heading if one exists.

Example:

```md
# Sprint Summary
```

produces a file stem based on `Sprint Summary`.

If no heading is found, the fallback name is:

```text
markdown-note
```

## Project structure

```text
MD Printer/
├── App.xaml
├── App.xaml.cs
├── MainPage.xaml
├── MainPage.xaml.cs
├── MauiProgram.cs
├── MDPrinter.csproj
├── Models/
├── Resources/
├── Services/
└── ViewModels/
```

Important files:

- `MainPage.xaml` - main UI layout and output preview overlay
- `MainPage.xaml.cs` - print event handlers for the preview WebView
- `ViewModels/MainPageViewModel.cs` - editor state, preview state, save/print commands, layout options
- `Services/MarkdownFormatterService.cs` - markdown normalization and HTML generation
- `Services/MarkdownPrinterService.cs` - platform print integration
- `Services/MarkdownDocumentService.cs` - file export logic
- `Services/OutputDirectoryPickerService.cs` - output folder selection

## Dependencies

- `CommunityToolkit.Mvvm`
- `Markdig`
- `Microsoft.Maui.Controls`
- `Microsoft.Extensions.Logging.Debug`

## Known limitations

- This project is currently optimized around desktop workflows.
- Printing depends on the platform print stack and available runtime support.
- The Mac distributable build script must be run on macOS to produce a real `.app` bundle.
- Shared desktop builds are unsigned, so Windows SmartScreen or macOS Gatekeeper may warn users before first launch.

## Public repository notes

The project uses the neutral app identifier:

```text
com.mdprinter.app
```

The repository `.gitignore` is set up to exclude common local/build/publish artifacts such as:

- `.vs/`
- `bin/`
- `obj/`
- `*.pubxml`
- `*.azurePubxml`
- `*.publishsettings`
- `*.pfx`

## License

Add the license that matches how you want to publish the project.
