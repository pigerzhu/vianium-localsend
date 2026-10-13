// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0
// SPDX-FileCopyrightText: 2026 Angel Careaga <hello@angelcareaga.com>
// SPDX-FileCopyrightText: 2026 pigerzhu

using System;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Phone.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Vianium.LocalSend.Logging;

namespace Vianium.LocalSend
{
    public sealed partial class App : Application
    {
        private const string TAG = "App";

        public App()
        {
            // Bring logging online before anything else, so a failure in
            // InitializeComponent or a static ctor is still captured.
            AppLog.Init();
            AppLog.I(TAG, "Constructing — LocalSend");

            this.InitializeComponent();
            this.Suspending += OnSuspending;
            this.Resuming += OnResuming;
            this.UnhandledException += OnUnhandledException;
            HardwareButtons.BackPressed += OnBackPressed;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

            AppLog.I(TAG, "Constructed — crash handlers armed");
        }

        private void OnBackPressed(object sender, BackPressedEventArgs e)
        {
            var rootFrame = Window.Current.Content as Frame;
            if (rootFrame != null && rootFrame.CanGoBack)
            {
                e.Handled = true;
                AppLog.V(TAG, "BackPressed — navigating back");
                rootFrame.GoBack();
            }
            else
            {
                AppLog.V(TAG, "BackPressed — no back stack, exiting app");
            }
        }

        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            AppLog.I(TAG, "OnLaunched — kind=" + e.Kind +
                " prevState=" + e.PreviousExecutionState +
                " args=" + (string.IsNullOrEmpty(e.Arguments) ? "(none)" : e.Arguments));

            Frame rootFrame = Window.Current.Content as Frame;

            if (rootFrame == null)
            {
                AppLog.D(TAG, "OnLaunched — creating root frame");
                rootFrame = new Frame();
                rootFrame.NavigationFailed += OnNavigationFailed;
                Window.Current.Content = rootFrame;
            }

            if (rootFrame.Content == null)
            {
                AppLog.D(TAG, "OnLaunched — navigating to MainPage");
                rootFrame.Navigate(typeof(Pages.MainPage), e.Arguments);
            }

            Window.Current.Activate();
        }

        protected override void OnActivated(IActivatedEventArgs args)
        {
            base.OnActivated(args);
            AppLog.I(TAG, "OnActivated — kind=" + args.Kind +
                " prevState=" + args.PreviousExecutionState);

            var rootFrame = Window.Current.Content as Frame;
            if (rootFrame == null)
            {
                AppLog.D(TAG, "OnActivated — creating root frame");
                rootFrame = new Frame();
                rootFrame.NavigationFailed += OnNavigationFailed;
                Window.Current.Content = rootFrame;
            }

            if (rootFrame.Content == null)
            {
                AppLog.D(TAG, "OnActivated — navigating to MainPage");
                rootFrame.Navigate(typeof(Pages.MainPage));
            }

            if (args.Kind == ActivationKind.PickFolderContinuation)
            {
                var page = rootFrame.Content as Pages.MainPage;
                var folderArgs = args as FolderPickerContinuationEventArgs;
                if (page != null && folderArgs != null) page.ContinueReceiveFolderPicker(folderArgs);
            }

            if (args.Kind == ActivationKind.PickFileContinuation)
            {
                var pickerArgs = args as FileOpenPickerContinuationEventArgs;
                if (pickerArgs != null)
                {
                    int count = pickerArgs.Files != null ? pickerArgs.Files.Count : 0;
                    AppLog.I(TAG, "OnActivated — file picker continuation, " + count + " file(s)");
                    var mainPage = rootFrame.Content as Pages.MainPage;
                    if (mainPage != null)
                    {
                        mainPage.ContinueFileOpenPicker(pickerArgs);
                    }
                    else
                    {
                        AppLog.W(TAG, "OnActivated — picker continuation but MainPage not available");
                    }
                }
            }

            Window.Current.Activate();
        }

        private void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            string page = e.SourcePageType != null ? e.SourcePageType.FullName : "(unknown)";
            AppLog.E(TAG, "Navigation FAILED to " + page, e.Exception);
            AppLog.FlushBlocking();
            throw new Exception("Failed to load Page " + page);
        }

        private async void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            AppLog.I(TAG, "OnSuspending — pausing services");

            try
            {
                var rootFrame = Window.Current.Content as Frame;
                if (rootFrame != null)
                {
                    var mainPage = rootFrame.Content as Pages.MainPage;
                    if (mainPage != null)
                    {
                        mainPage.SuspendServices();
                    }
                }
            }
            catch (Exception ex)
            {
                AppLog.E(TAG, "OnSuspending — error pausing services", ex);
            }
            finally
            {
                // Drain pending log lines to disk while we still have a
                // deferral; otherwise they would be lost on termination.
                try
                {
                    await AppLog.FlushToFileAsync();
                }
                catch { }
                AppLog.I(TAG, "OnSuspending — complete, log flushed");
                deferral.Complete();
            }
        }

        private async void OnResuming(object sender, object e)
        {
            AppLog.I(TAG, "OnResuming — restarting services");
            var rootFrame = Window.Current.Content as Frame;
            if (rootFrame != null)
            {
                var mainPage = rootFrame.Content as Pages.MainPage;
                if (mainPage != null)
                {
                    try
                    {
                        await mainPage.ResumeServicesAsync();
                    }
                    catch (Exception ex)
                    {
                        AppLog.E(TAG, "OnResuming — error restarting services", ex);
                    }
                }
            }
        }

        /// <summary>
        /// Last-resort handler for exceptions that escape the UI thread.
        /// Logs the failure and forces a synchronous flush so the crash
        /// survives in the on-device log. <see cref="UnhandledExceptionEventArgs.Handled"/>
        /// is intentionally left <c>false</c> — the goal is to record the
        /// crash, not to mask it.
        /// </summary>
        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            AppLog.E(TAG, "UNHANDLED EXCEPTION: " + e.Message, e.Exception);
            AppLog.FlushBlocking();
        }

        /// <summary>
        /// Catches faulted <see cref="Task"/>s whose exception was never
        /// observed (fire-and-forget work). Marked observed so it does not
        /// escalate, but recorded and flushed first.
        /// </summary>
        private void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            AppLog.E(TAG, "Unobserved task exception", e.Exception);
            e.SetObserved();
            AppLog.FlushBlocking();
        }
    }
}
