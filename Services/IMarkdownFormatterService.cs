namespace MDPrinter.Services;

/// <summary>
/// Normalizes markdown text and renders it into HTML.
/// </summary>
public interface IMarkdownFormatterService
{
	/// <summary>
	/// Normalizes raw markdown into the export-ready markdown text.
	/// </summary>
	/// <param name="rawMarkdown">The raw markdown input.</param>
	/// <returns>The normalized markdown output.</returns>
	string Normalize(string rawMarkdown);

	/// <summary>
	/// Builds a full HTML document for previewing and printing markdown content.
	/// </summary>
	/// <param name="title">The document title.</param>
	/// <param name="markdown">The normalized markdown content.</param>
	/// <param name="useLandscapeLayout">When <see langword="true"/>, the rendered document uses a landscape page layout.</param>
	/// <param name="baseTextSize">The base text size, in pixels, for the rendered document.</param>
	/// <returns>A complete HTML document string.</returns>
	string ToHtmlDocument(string title, string markdown, bool useLandscapeLayout, double baseTextSize);
}
