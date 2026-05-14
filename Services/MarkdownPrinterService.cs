using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;

#if MACCATALYST
using UIKit;
#elif WINDOWS
using Microsoft.Web.WebView2.Core;
using WinUIWebView2 = Microsoft.UI.Xaml.Controls.WebView2;
#endif

namespace MDPrinter.Services;

internal sealed class MarkdownPrinterService : IMarkdownPrinterService
{
	public async Task PrintAsync(string title, string htmlContent, WebView previewWebView, CancellationToken cancellationToken)
	{
#if MACCATALYST
		await PrintOnAppleAsync(title, htmlContent, cancellationToken);
#elif WINDOWS
		await PrintOnWindowsAsync(htmlContent, previewWebView, cancellationToken);
#else
		throw new PlatformNotSupportedException("Printing is not supported on this platform.");
#endif
	}

#if WINDOWS
	private static Task PrintOnWindowsAsync(string htmlContent, WebView previewWebView, CancellationToken cancellationToken)
	{
		return MainThread.InvokeOnMainThreadAsync(async () =>
		{
			cancellationToken.ThrowIfCancellationRequested();

			var platformWebView = previewWebView.Handler?.PlatformView as WinUIWebView2
				?? throw new InvalidOperationException("The rendered preview is not ready to print yet.");
			await platformWebView.EnsureCoreWebView2Async();

			var completionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

			void HandleNavigationCompleted(WinUIWebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
			{
				sender.NavigationCompleted -= HandleNavigationCompleted;

				if (!args.IsSuccess)
				{
					completionSource.TrySetException(new InvalidOperationException($"Windows could not load the rendered preview for printing ({args.WebErrorStatus})."));
					return;
				}

				try
				{
					sender.CoreWebView2.ShowPrintUI(CoreWebView2PrintDialogKind.System);
					completionSource.TrySetResult();
				}
				catch (NotImplementedException)
				{
					completionSource.TrySetException(new InvalidOperationException("The installed WebView2 runtime does not support the Windows print dialog API."));
				}
			}

			using var registration = cancellationToken.Register(() =>
			{
				platformWebView.NavigationCompleted -= HandleNavigationCompleted;
				platformWebView.CoreWebView2?.Stop();
				completionSource.TrySetCanceled(cancellationToken);
			});

			platformWebView.NavigationCompleted += HandleNavigationCompleted;
			platformWebView.NavigateToString(htmlContent);
			await completionSource.Task;
		});
	}
#endif

#if MACCATALYST
	private static Task PrintOnAppleAsync(string title, string htmlContent, CancellationToken cancellationToken)
	{
		var completionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		using var registration = cancellationToken.Register(() => completionSource.TrySetCanceled(cancellationToken));

		MainThread.BeginInvokeOnMainThread(() =>
		{
			var controller = UIPrintInteractionController.SharedPrintController;
			if (controller is null)
			{
				completionSource.TrySetException(new InvalidOperationException("Apple print services are not available."));
				return;
			}

			controller.PrintInfo = UIPrintInfo.PrintInfo;
			controller.PrintInfo.JobName = title;
			controller.PrintInfo.OutputType = UIPrintInfoOutputType.General;
			controller.ShowsNumberOfCopies = true;
			controller.PrintFormatter = new UIMarkupTextPrintFormatter(htmlContent);
			controller.Present(true, (printController, completed, error) =>
			{
				if (error is not null)
				{
					completionSource.TrySetException(new InvalidOperationException(error.LocalizedDescription));
					return;
				}

				if (!completed)
				{
					completionSource.TrySetCanceled(cancellationToken);
					return;
				}

				completionSource.TrySetResult();
			});
		});

		return completionSource.Task;
	}
#endif
}
