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

			// The preview WebView is bound to PreviewSource, so it may still be
			// loading its own navigation when printing starts. Calling
			// NavigateToString below aborts that in-flight navigation, which
			// raises NavigationCompleted with IsSuccess == false. We must only
			// act on the navigation we started here, identified by its
			// NavigationId, otherwise that aborted preview navigation would be
			// mistaken for a print failure and the print dialog would never open.
			ulong? printNavigationId = null;

			void HandleNavigationStarting(WinUIWebView2 sender, CoreWebView2NavigationStartingEventArgs args)
			{
				sender.NavigationStarting -= HandleNavigationStarting;
				printNavigationId = args.NavigationId;
			}

			void HandleNavigationCompleted(WinUIWebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
			{
				// Ignore completions for any navigation other than the print
				// navigation we initiated (for example a preview navigation that
				// was still in flight and got superseded by NavigateToString).
				if (printNavigationId is null || args.NavigationId != printNavigationId.Value)
				{
					return;
				}

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
				platformWebView.NavigationStarting -= HandleNavigationStarting;
				platformWebView.NavigationCompleted -= HandleNavigationCompleted;
				platformWebView.CoreWebView2?.Stop();
				completionSource.TrySetCanceled(cancellationToken);
			});

			platformWebView.NavigationStarting += HandleNavigationStarting;
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
