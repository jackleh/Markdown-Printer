#if WINDOWS
using Windows.Storage.Pickers;
using WinRT.Interop;
using WinUIApplication = Microsoft.Maui.Controls.Application;
using WinUIWindow = Microsoft.UI.Xaml.Window;
#elif MACCATALYST
using Foundation;
using Microsoft.Maui.ApplicationModel;
using UIKit;
using UniformTypeIdentifiers;
using MauiApplication = Microsoft.Maui.Controls.Application;
#endif

namespace MDPrinter.Services;

internal sealed class OutputDirectoryPickerService : IOutputDirectoryPickerService
{
	public Task<string?> PickDirectoryAsync(CancellationToken cancellationToken)
	{
#if WINDOWS
		return PickDirectoryOnWindowsAsync(cancellationToken);
#elif MACCATALYST
		return PickDirectoryOnMacCatalystAsync(cancellationToken);
#else
		throw new PlatformNotSupportedException("Folder selection is not supported on this platform.");
#endif
	}

#if WINDOWS
	private static async Task<string?> PickDirectoryOnWindowsAsync(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var mauiWindow = WinUIApplication.Current?.Windows.FirstOrDefault()
			?? throw new InvalidOperationException("An active window is required before choosing an output directory.");
		var nativeWindow = mauiWindow.Handler?.PlatformView as WinUIWindow
			?? throw new InvalidOperationException("The Windows output directory picker is not available until the main window is ready.");
		var folderPicker = new FolderPicker();
		folderPicker.FileTypeFilter.Add("*");
		InitializeWithWindow.Initialize(folderPicker, WindowNative.GetWindowHandle(nativeWindow));

		var selectedFolder = await folderPicker.PickSingleFolderAsync();
		return selectedFolder?.Path;
	}
#endif

#if MACCATALYST
	// On sandboxed Mac Catalyst the folder URL returned by the document picker is
	// security-scoped: the app may only read or write inside it while access is
	// explicitly open. That access has to stay open from the moment the user picks
	// the folder until the save finishes writing into it, which happens after this
	// method returns (in MarkdownDocumentService). We therefore keep the scope open
	// and release the previously picked folder the next time the user saves, so at
	// most one scoped URL is held at a time and it is released when the app exits.
	private NSUrl? activeScopedUrl;

	private Task<string?> PickDirectoryOnMacCatalystAsync(CancellationToken cancellationToken)
	{
		var completionSource = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
		using var registration = cancellationToken.Register(() => completionSource.TrySetCanceled(cancellationToken));

		MainThread.BeginInvokeOnMainThread(() =>
		{
			try
			{
				cancellationToken.ThrowIfCancellationRequested();

				var presentingController = GetPresentingViewController()
					?? throw new InvalidOperationException("The Mac output directory picker is not available until the main window is ready.");

				var picker = new UIDocumentPickerViewController(new[] { UTTypes.Folder })
				{
					AllowsMultipleSelection = false
				};

				void Detach()
				{
					picker.DidPickDocumentAtUrls -= OnPicked;
					picker.WasCancelled -= OnCancelled;
				}

				void OnPicked(object? sender, UIDocumentPickedAtUrlsEventArgs args)
				{
					Detach();

					var pickedUrl = args.Urls.FirstOrDefault();
					if (pickedUrl is null)
					{
						completionSource.TrySetResult(null);
						return;
					}

					ReleaseActiveScope();
					if (pickedUrl.StartAccessingSecurityScopedResource())
					{
						activeScopedUrl = pickedUrl;
					}

					completionSource.TrySetResult(pickedUrl.Path);
				}

				void OnCancelled(object? sender, EventArgs args)
				{
					Detach();
					completionSource.TrySetResult(null);
				}

				picker.DidPickDocumentAtUrls += OnPicked;
				picker.WasCancelled += OnCancelled;
				presentingController.PresentViewController(picker, true, null);
			}
			catch (OperationCanceledException)
			{
				completionSource.TrySetCanceled(cancellationToken);
			}
			catch (Exception exception)
			{
				completionSource.TrySetException(exception);
			}
		});

		return completionSource.Task;
	}

	private void ReleaseActiveScope()
	{
		if (activeScopedUrl is null)
		{
			return;
		}

		activeScopedUrl.StopAccessingSecurityScopedResource();
		activeScopedUrl = null;
	}

	private static UIViewController? GetPresentingViewController()
	{
		var mauiWindow = MauiApplication.Current?.Windows.FirstOrDefault();
		var presentingController = (mauiWindow?.Handler?.PlatformView as UIWindow)?.RootViewController;

		// A modal (such as a previously dismissed picker) may still be on top, so
		// walk to the controller that is actually presenting right now.
		while (presentingController?.PresentedViewController is not null)
		{
			presentingController = presentingController.PresentedViewController;
		}

		return presentingController;
	}
#endif
}
