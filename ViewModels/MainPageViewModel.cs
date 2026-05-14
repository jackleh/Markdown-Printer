using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MDPrinter.Models;
using MDPrinter.Services;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;

namespace MDPrinter.ViewModels;

/// <summary>
/// Coordinates markdown editing, preview generation, file export, and printing.
/// </summary>
public sealed class MainPageViewModel : ObservableObject
{
	private const double DefaultBaseTextSize = 16;
	private const double MaximumBaseTextSize = 28;
	private const double MinimumBaseTextSize = 12;
	private const string DefaultStatusMessage = "Paste markdown from the clipboard or start typing to generate a print-ready .md file.";
	private const string DefaultDocumentTitle = "markdown-note";
	private const string ClipboardEmptyMessage = "The clipboard does not currently contain any text.";
	private const string LandscapeOrientation = "Landscape";
	private const string PortraitOrientation = "Portrait";
	private const string PrintQueuedMessage = "The system print dialog was opened for the rendered markdown preview.";

	private static readonly IReadOnlyList<string> pageOrientations = Array.AsReadOnly(new[] { PortraitOrientation, LandscapeOrientation });

	private double baseTextSize = DefaultBaseTextSize;
	private readonly IMarkdownClipboardService clipboardService;
	private readonly IMarkdownDocumentService documentService;
	private readonly IMarkdownFormatterService formatterService;
	private readonly IOutputDirectoryPickerService outputDirectoryPickerService;
	private readonly IMarkdownPrinterService printerService;

	private string generatedMarkdownPath = string.Empty;
	private bool isOutputPreviewOpen;
	private HtmlWebViewSource previewSource = new();
	private string rawMarkdown = """
# Welcome to Markdown Printer

Paste raw markdown here, then open the larger output preview to save or print the generated file.

- Bullet lists are normalized
- Preview renders the markdown immediately
- Save and print can use custom layout and text size

```csharp
Console.WriteLine("Ready to print markdown.");
```
""";
	private string selectedPageOrientation = PortraitOrientation;
	private string statusMessage = DefaultStatusMessage;

	/// <summary>
	/// Initializes a new instance of the <see cref="MainPageViewModel"/> class.
	/// </summary>
	/// <param name="clipboardService">Reads raw markdown text from the clipboard.</param>
	/// <param name="documentService">Saves the generated markdown and HTML files.</param>
	/// <param name="formatterService">Normalizes markdown and builds HTML previews.</param>
	/// <param name="outputDirectoryPickerService">Prompts the user to pick a save directory.</param>
	/// <param name="printerService">Prints rendered markdown through the platform print system.</param>
	public MainPageViewModel(
		IMarkdownClipboardService clipboardService,
		IMarkdownDocumentService documentService,
		IMarkdownFormatterService formatterService,
		IOutputDirectoryPickerService outputDirectoryPickerService,
		IMarkdownPrinterService printerService)
	{
		this.clipboardService = clipboardService;
		this.documentService = documentService;
		this.formatterService = formatterService;
		this.outputDirectoryPickerService = outputDirectoryPickerService;
		this.printerService = printerService;

		PasteFromClipboardCommand = new AsyncRelayCommand(PasteFromClipboardAsync);
		OpenSavePreviewCommand = new RelayCommand(OpenOutputPreview, CanExport);
		OpenPrintPreviewCommand = new RelayCommand(OpenOutputPreview, CanExport);
		SaveMarkdownCommand = new AsyncRelayCommand(SaveMarkdownAsync, CanExport);
		CloseOutputPreviewCommand = new RelayCommand(CloseOutputPreview);
		ClearCommand = new RelayCommand(Clear, CanExport);
		DecreaseBaseTextSizeCommand = new RelayCommand(DecreaseBaseTextSize, CanDecreaseBaseTextSizeCommand);
		IncreaseBaseTextSizeCommand = new RelayCommand(IncreaseBaseTextSize, CanIncreaseBaseTextSizeCommand);

		RefreshDerivedContent();
	}

	/// <summary>
	/// Gets the document title used for generated files and print jobs.
	/// </summary>
	public string DocumentTitle
	{
		get => GetEffectiveDocumentTitle();
	}

	/// <summary>
	/// Gets the suggested markdown file name based on the current document title.
	/// </summary>
	public string SuggestedFileName => $"{DocumentNameUtility.ToSafeFileStem(DocumentTitle)}.md";

	/// <summary>
	/// Gets or sets the raw markdown input being edited by the user.
	/// </summary>
	public string RawMarkdown
	{
		get => rawMarkdown;
		set
		{
			if (SetProperty(ref rawMarkdown, value))
			{
				InvalidateSavedDocument();
				OnPropertyChanged(nameof(DocumentTitle));
				OnPropertyChanged(nameof(SuggestedFileName));
				OnPropertyChanged(nameof(HasMarkdown));
				RefreshDerivedContent();
				RefreshCommandStates();
			}
		}
	}

	/// <summary>
	/// Gets the rendered HTML preview shown in the application.
	/// </summary>
	public HtmlWebViewSource PreviewSource
	{
		get => previewSource;
		private set => SetProperty(ref previewSource, value);
	}

	/// <summary>
	/// Gets or sets a value indicating whether the larger output preview is open.
	/// </summary>
	public bool IsOutputPreviewOpen
	{
		get => isOutputPreviewOpen;
		set => SetProperty(ref isOutputPreviewOpen, value);
	}

	/// <summary>
	/// Gets the supported page orientations for the output preview.
	/// </summary>
	public IReadOnlyList<string> PageOrientations => pageOrientations;

	/// <summary>
	/// Gets or sets the selected page orientation used for previewing and printing.
	/// </summary>
	public string SelectedPageOrientation
	{
		get => selectedPageOrientation;
		set
		{
			var normalizedOrientation = string.Equals(value, LandscapeOrientation, StringComparison.Ordinal) ? LandscapeOrientation : PortraitOrientation;
			if (SetProperty(ref selectedPageOrientation, normalizedOrientation))
			{
				InvalidateSavedDocument();
				RefreshDerivedContent();
			}
		}
	}

	/// <summary>
	/// Gets or sets the base text size used for previewing and printing.
	/// </summary>
	public double BaseTextSize
	{
		get => baseTextSize;
		set
		{
			var normalizedTextSize = Math.Clamp(Math.Round(value), MinimumBaseTextSize, MaximumBaseTextSize);
			if (SetProperty(ref baseTextSize, normalizedTextSize))
			{
				OnPropertyChanged(nameof(BaseTextSizeLabel));
				OnPropertyChanged(nameof(CanDecreaseBaseTextSize));
				OnPropertyChanged(nameof(CanIncreaseBaseTextSize));
				DecreaseBaseTextSizeCommand.NotifyCanExecuteChanged();
				IncreaseBaseTextSizeCommand.NotifyCanExecuteChanged();
				InvalidateSavedDocument();
				RefreshDerivedContent();
			}
		}
	}

	/// <summary>
	/// Gets the display label for the currently selected base text size.
	/// </summary>
	public string BaseTextSizeLabel => $"{BaseTextSize:0}px";

	/// <summary>
	/// Gets a value indicating whether the base text size can be decreased.
	/// </summary>
	public bool CanDecreaseBaseTextSize => BaseTextSize > MinimumBaseTextSize;

	/// <summary>
	/// Gets a value indicating whether the base text size can be increased.
	/// </summary>
	public bool CanIncreaseBaseTextSize => BaseTextSize < MaximumBaseTextSize;

	/// <summary>
	/// Gets the full path of the most recently saved markdown file.
	/// </summary>
	public string GeneratedMarkdownPath
	{
		get => generatedMarkdownPath;
		private set
		{
			if (SetProperty(ref generatedMarkdownPath, value))
			{
				OnPropertyChanged(nameof(HasGeneratedMarkdownPath));
			}
		}
	}

	private string StatusMessage
	{
		get => statusMessage;
		set => SetProperty(ref statusMessage, value);
	}

	/// <summary>
	/// Gets a value indicating whether there is markdown available to export.
	/// </summary>
	public bool HasMarkdown => !string.IsNullOrWhiteSpace(RawMarkdown);

	/// <summary>
	/// Gets a value indicating whether the app has already saved a markdown file for the current content.
	/// </summary>
	public bool HasGeneratedMarkdownPath => !string.IsNullOrWhiteSpace(GeneratedMarkdownPath);

	/// <summary>
	/// Gets the command that pastes clipboard text into the raw markdown editor.
	/// </summary>
	public IAsyncRelayCommand PasteFromClipboardCommand { get; }

	/// <summary>
	/// Gets the command that opens the larger output preview for saving.
	/// </summary>
	public IRelayCommand OpenSavePreviewCommand { get; }

	/// <summary>
	/// Gets the command that opens the larger output preview for printing.
	/// </summary>
	public IRelayCommand OpenPrintPreviewCommand { get; }

	/// <summary>
	/// Gets the command that writes the normalized markdown and rendered HTML to a user-selected directory.
	/// </summary>
	public IAsyncRelayCommand SaveMarkdownCommand { get; }

	/// <summary>
	/// Gets the command that closes the larger output preview.
	/// </summary>
	public IRelayCommand CloseOutputPreviewCommand { get; }

	/// <summary>
	/// Gets the command that clears the current markdown and preview output.
	/// </summary>
	public IRelayCommand ClearCommand { get; }

	/// <summary>
	/// Gets the command that decreases the base text size by one point.
	/// </summary>
	public IRelayCommand DecreaseBaseTextSizeCommand { get; }

	/// <summary>
	/// Gets the command that increases the base text size by one point.
	/// </summary>
	public IRelayCommand IncreaseBaseTextSizeCommand { get; }

	internal async Task PrintFromPreviewAsync(WebView previewWebView)
	{
		try
		{
			if (!HasMarkdown)
			{
				StatusMessage = "Enter markdown before printing.";
				return;
			}

			var normalizedMarkdown = GetNormalizedMarkdown();
			var htmlDocument = CreateHtmlDocument(normalizedMarkdown);
			await printerService.PrintAsync(DocumentTitle, htmlDocument, previewWebView, CancellationToken.None);
			StatusMessage = PrintQueuedMessage;
		}
		catch (PlatformNotSupportedException exception)
		{
			StatusMessage = exception.Message;
		}
		catch (FeatureNotSupportedException)
		{
			StatusMessage = "Printing is not supported on this device.";
		}
		catch (InvalidOperationException exception)
		{
			StatusMessage = exception.Message;
		}
		catch (IOException)
		{
			StatusMessage = "The rendered markdown could not be prepared for printing.";
		}
		catch (UnauthorizedAccessException)
		{
			StatusMessage = "The app does not have permission to print the generated document.";
		}
		catch (OperationCanceledException)
		{
			StatusMessage = "The print request was cancelled.";
		}
	}

	private bool CanExport()
	{
		return HasMarkdown;
	}

	private async Task PasteFromClipboardAsync()
	{
		try
		{
			var clipboardText = await clipboardService.GetTextAsync(CancellationToken.None);
			if (string.IsNullOrWhiteSpace(clipboardText))
			{
				StatusMessage = ClipboardEmptyMessage;
				return;
			}

			RawMarkdown = clipboardText;
			StatusMessage = "Clipboard text pasted and preview refreshed.";
		}
		catch (FeatureNotSupportedException)
		{
			StatusMessage = "Clipboard access is not supported on this device.";
		}
	}

	private void OpenOutputPreview()
	{
		IsOutputPreviewOpen = true;
	}

	private void CloseOutputPreview()
	{
		IsOutputPreviewOpen = false;
	}

	private async Task SaveMarkdownAsync()
	{
		try
		{
			if (!HasMarkdown)
			{
				StatusMessage = "Enter markdown before saving.";
				return;
			}

			var outputDirectory = await outputDirectoryPickerService.PickDirectoryAsync(CancellationToken.None);
			if (string.IsNullOrWhiteSpace(outputDirectory))
			{
				StatusMessage = "Save cancelled.";
				return;
			}

			var document = await SaveDocumentAsync(outputDirectory, CancellationToken.None);
			GeneratedMarkdownPath = document.MarkdownFilePath;
			StatusMessage = $"Markdown saved to {document.MarkdownFilePath}.";
		}
		catch (PlatformNotSupportedException exception)
		{
			StatusMessage = exception.Message;
		}
		catch (IOException)
		{
			StatusMessage = "The markdown files could not be written to the selected directory.";
		}
		catch (UnauthorizedAccessException)
		{
			StatusMessage = "The app does not have permission to write files into the selected directory.";
		}
		catch (InvalidOperationException exception)
		{
			StatusMessage = exception.Message;
		}
	}

	private void Clear()
	{
		RawMarkdown = string.Empty;
		StatusMessage = "Markdown content cleared.";
	}

	private void DecreaseBaseTextSize()
	{
		BaseTextSize -= 1;
	}

	private void IncreaseBaseTextSize()
	{
		BaseTextSize += 1;
	}

	private bool CanDecreaseBaseTextSizeCommand()
	{
		return CanDecreaseBaseTextSize;
	}

	private bool CanIncreaseBaseTextSizeCommand()
	{
		return CanIncreaseBaseTextSize;
	}

	private async Task<GeneratedDocument> SaveDocumentAsync(string outputDirectory, CancellationToken cancellationToken)
	{
		var normalizedMarkdown = GetNormalizedMarkdown();
		var htmlDocument = CreateHtmlDocument(normalizedMarkdown);
		var signature = $"{SuggestedFileName}\n{normalizedMarkdown}\n{SelectedPageOrientation}\n{BaseTextSizeLabel}";

		return await documentService.SaveAsync(
			DocumentTitle,
			normalizedMarkdown,
			htmlDocument,
			outputDirectory,
			signature,
			cancellationToken);
	}

	private void InvalidateSavedDocument()
	{
		GeneratedMarkdownPath = string.Empty;
	}

	private void RefreshCommandStates()
	{
		SaveMarkdownCommand.NotifyCanExecuteChanged();
		OpenSavePreviewCommand.NotifyCanExecuteChanged();
		OpenPrintPreviewCommand.NotifyCanExecuteChanged();
		ClearCommand.NotifyCanExecuteChanged();
	}

	private void RefreshDerivedContent()
	{
		var normalizedMarkdown = GetNormalizedMarkdown();
		PreviewSource = new HtmlWebViewSource
		{
			Html = CreateHtmlDocument(normalizedMarkdown)
		};
	}

	private string CreateHtmlDocument(string normalizedMarkdown)
	{
		return formatterService.ToHtmlDocument(DocumentTitle, normalizedMarkdown, UseLandscapeLayout, BaseTextSize);
	}

	private string GetNormalizedMarkdown()
	{
		return formatterService.Normalize(RawMarkdown);
	}

	private string GetEffectiveDocumentTitle()
	{
		var lines = RawMarkdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
		foreach (var line in lines)
		{
			var trimmedLine = line.Trim();
			if (!trimmedLine.StartsWith('#'))
			{
				continue;
			}

			var headingText = trimmedLine.TrimStart('#').Trim();
			if (!string.IsNullOrWhiteSpace(headingText))
			{
				return headingText;
			}
		}

		return DefaultDocumentTitle;
	}

	private bool UseLandscapeLayout => string.Equals(SelectedPageOrientation, LandscapeOrientation, StringComparison.Ordinal);
}
