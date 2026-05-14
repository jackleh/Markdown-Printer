using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Markdig;

namespace MDPrinter.Services;

internal sealed partial class MarkdownFormatterService : IMarkdownFormatterService
{
	private const string EmptyPreviewMessage = "<p>Start typing markdown to preview it here.</p>";

	private readonly MarkdownPipeline pipeline = new MarkdownPipelineBuilder()
		.UseAdvancedExtensions()
		.DisableHtml()
		.Build();

	public string Normalize(string rawMarkdown)
	{
		if (string.IsNullOrWhiteSpace(rawMarkdown))
		{
			return string.Empty;
		}

		var standardized = rawMarkdown.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
		var lines = standardized.Split('\n');
		var builder = new StringBuilder();
		var hasWrittenContent = false;
		var insertBlankLine = false;
		var insideFence = false;

		foreach (var sourceLine in lines)
		{
			var line = sourceLine.TrimEnd();
			var trimmedStart = line.TrimStart();

			if (CodeFencePattern().IsMatch(trimmedStart))
			{
				if (insertBlankLine)
				{
					builder.AppendLine();
					insertBlankLine = false;
				}

				builder.AppendLine(trimmedStart);
				hasWrittenContent = true;
				insideFence = !insideFence;
				continue;
			}

			if (insideFence)
			{
				builder.AppendLine(line);
				hasWrittenContent = true;
				continue;
			}

			if (string.IsNullOrWhiteSpace(line))
			{
				if (hasWrittenContent)
				{
					insertBlankLine = true;
				}

				continue;
			}

			if (insertBlankLine)
			{
				builder.AppendLine();
				insertBlankLine = false;
			}

			builder.AppendLine(NormalizeLine(line));
			hasWrittenContent = true;
		}

		return hasWrittenContent ? $"{builder.ToString().TrimEnd()}{Environment.NewLine}" : string.Empty;
	}

	public string ToHtmlDocument(string title, string markdown, bool useLandscapeLayout, double baseTextSize)
	{
		var encodedTitle = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(title) ? "Markdown Printer Document" : title.Trim());
		var htmlBody = string.IsNullOrWhiteSpace(markdown)
			? EmptyPreviewMessage
			: Markdown.ToHtml(markdown, pipeline);
		var pageOrientation = useLandscapeLayout ? "landscape" : "portrait";
		var maxWidth = useLandscapeLayout ? 1280 : 980;
		var normalizedBaseTextSize = Math.Clamp(baseTextSize, 12d, 28d);

		return $$"""
		<!DOCTYPE html>
		<html lang="en">
		<head>
		    <meta charset="utf-8" />
		    <meta name="viewport" content="width=device-width, initial-scale=1" />
		    <meta http-equiv="Content-Security-Policy" content="default-src 'none'; style-src 'unsafe-inline'; img-src data: https:;" />
		    <title>{{encodedTitle}}</title>
		    <style>
		@page {
		    size: {{pageOrientation}};
		    margin: 0.5in;
		}

		        :root {
		            color-scheme: light dark;
		        }

		        body {
		            margin: 0;
		            font-family: "Segoe UI", Arial, sans-serif;
		            font-size: {{normalizedBaseTextSize}}px;
		            background: #f8fafc;
		            color: #0f172a;
		        }

		        main {
		            max-width: {{maxWidth}}px;
		            margin: 0 auto;
		            padding: 24px 28px 40px;
		            line-height: 1.65;
		        }

		        pre {
		            padding: 16px;
		            border-radius: 12px;
		            overflow-x: auto;
		            background: #0f172a;
		            color: #e2e8f0;
		        }

		        code {
		            font-family: Consolas, "Courier New", monospace;
		        }

		        blockquote {
		            margin: 0;
		            padding-left: 16px;
		            border-left: 4px solid #93c5fd;
		            color: #334155;
		        }

		        table {
		            width: 100%;
		            border-collapse: collapse;
		        }

		        th, td {
		            padding: 10px;
		            border: 1px solid #cbd5e1;
		            text-align: left;
		        }

		        img {
		            max-width: 100%;
		        }
		    </style>
		</head>
		<body>
		    <main>
		{{htmlBody}}
		    </main>
		</body>
		</html>
		""";
	}

	private static string NormalizeLine(string line)
	{
		var taskListMatch = TaskListPattern().Match(line);
		if (taskListMatch.Success)
		{
			var checkState = taskListMatch.Groups[2].Value.Equals("x", StringComparison.OrdinalIgnoreCase) ? "x" : " ";
			return $"{taskListMatch.Groups[1].Value}- [{checkState}] {taskListMatch.Groups[3].Value.Trim()}".TrimEnd();
		}

		var orderedListMatch = OrderedListPattern().Match(line);
		if (orderedListMatch.Success)
		{
			return $"{orderedListMatch.Groups[1].Value}{orderedListMatch.Groups[2].Value}. {orderedListMatch.Groups[3].Value.Trim()}".TrimEnd();
		}

		var unorderedListMatch = UnorderedListPattern().Match(line);
		if (unorderedListMatch.Success)
		{
			return $"{unorderedListMatch.Groups[1].Value}- {unorderedListMatch.Groups[2].Value.Trim()}".TrimEnd();
		}

		var headingMatch = HeadingPattern().Match(line);
		if (headingMatch.Success)
		{
			return $"{headingMatch.Groups[1].Value} {headingMatch.Groups[2].Value.Trim()}".TrimEnd();
		}

		var quoteMatch = QuotePattern().Match(line);
		if (quoteMatch.Success)
		{
			return $"{quoteMatch.Groups[1].Value} {quoteMatch.Groups[2].Value.Trim()}".TrimEnd();
		}

		return line;
	}

	[GeneratedRegex(@"^(```|~~~)")]
	private static partial Regex CodeFencePattern();

	[GeneratedRegex(@"^(\s*)[-*+]\s+\[( |x|X)\]\s*(.*)$")]
	private static partial Regex TaskListPattern();

	[GeneratedRegex(@"^(\s*)(\d+)[\.\)]\s+(.*)$")]
	private static partial Regex OrderedListPattern();

	[GeneratedRegex(@"^(\s*)[-*+]\s+(.*)$")]
	private static partial Regex UnorderedListPattern();

	[GeneratedRegex(@"^(#{1,6})\s+(.*)$")]
	private static partial Regex HeadingPattern();

	[GeneratedRegex(@"^(\s*>+)\s*(.*)$")]
	private static partial Regex QuotePattern();
}
