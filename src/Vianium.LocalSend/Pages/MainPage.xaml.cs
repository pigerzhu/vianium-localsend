// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0
// SPDX-FileCopyrightText: 2026 Angel Careaga <hello@angelcareaga.com>

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Activation;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Vianium.LocalSend.Converters;
using Vianium.LocalSend.Data;
using Vianium.LocalSend.Discovery;
using Vianium.LocalSend.Logging;
using Vianium.LocalSend.Protocol;
using Vianium.LocalSend.Strings;

namespace Vianium.LocalSend.Pages
{
    public sealed partial class MainPage : Page
    {
        private const string TAG = "MainPage";

        private DeviceInfo _localDevice;
        private DiscoveryService _discovery;
        private LocalSendServer _server;
        private LocalSendClient _client;
        private DispatcherTimer _refreshTimer;

        private List<StorageFile> _selectedFiles;
        private List<ReceivedFileItem> _receivedFiles;
        private bool _servicesStarted;
        private bool _sdAvailable;
        private bool _pickerInFlight;

        private static readonly SolidColorBrush BrushGreen =
            new SolidColorBrush(Windows.UI.Color.FromArgb(255, 63, 185, 80));
        private static readonly SolidColorBrush BrushRed =
            new SolidColorBrush(Windows.UI.Color.FromArgb(255, 248, 81, 73));
        private static readonly SolidColorBrush BrushGray =
            new SolidColorBrush(Windows.UI.Color.FromArgb(255, 139, 148, 158));

        public MainPage()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = NavigationCacheMode.Required;
            _selectedFiles = new List<StorageFile>();
            _receivedFiles = new List<ReceivedFileItem>();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            AppLog.V(TAG, "OnNavigatedTo — servicesStarted=" + _servicesStarted);

            if (!_servicesStarted)
            {
                // Apply localized strings + cached alias immediately
                ApplyStrings();
                string alias = DeviceInfo.GetSavedAlias() ?? "Windows Phone";
                AliasTextBox.Text = alias;
                DeviceAliasDisplay.Text = alias;
                ServerStatusText.Text = S.Starting;

                // Defer all networking to Low priority — page renders first
                var ignore = Dispatcher.RunAsync(CoreDispatcherPriority.Low, async () =>
                {
                    await InitializeServicesAsync();
                });
            }
            else
            {
                StartRefreshTimer();
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            StopRefreshTimer();
        }

        private async Task InitializeServicesAsync()
        {
            var timer = AppLog.Time("App", "Init");
            AppLog.I(TAG, "Initializing services...");

            // This runs inside an async-void dispatcher callback — an
            // exception escaping here would be unhandled and crash the app.
            try
            {
                _localDevice = DeviceInfo.CreateLocal();

                // Update UI with real device info
                AliasTextBox.Text = _localDevice.Alias;
                FingerprintText.Text = _localDevice.Fingerprint;
                DeviceAliasDisplay.Text = _localDevice.Alias;
                DeviceModelDisplay.Text = _localDevice.DeviceModel ?? "";
                DeviceModelText.Text = _localDevice.DeviceModel ?? "";
                _sdAvailable = await StorageConfig.IsSdCardAvailableAsync();
                RefreshStorageButtons();

                // Create services (no I/O, fast)
                _discovery = new DiscoveryService(_localDevice);
                _discovery.Tracker.DevicesChanged += OnDevicesChanged;

                _server = new LocalSendServer(_localDevice, _discovery.Tracker);
                _server.IncomingTransferRequested += OnIncomingTransfer;
                _server.FileReceiveProgress += OnFileReceiveProgress;
                _server.TransferCompleted += OnTransferCompleted;

                _client = new LocalSendClient(_localDevice);
                AppLog.I(TAG, "Services created — device '" + _localDevice.Alias +
                    "' fingerprint=" + _localDevice.Fingerprint);

                // Start server + discovery in parallel
                await Task.WhenAll(StartServerAsync(), StartDiscoveryAsync());

                _servicesStarted = true;
                StartRefreshTimer();
                timer.Stop();
                AppLog.I(TAG, "All services started");
            }
            catch (Exception ex)
            {
                AppLog.E(TAG, "Service initialization failed", ex);
                try
                {
                    ServerStatusText.Text = "Error: " + ex.Message;
                    StatusDot.Fill = BrushRed;
                }
                catch { }
            }
        }

        private async Task StartServerAsync()
        {
            try
            {
                await _server.StartAsync();
                string ip = DiscoveryService.GetLocalIpAddress();
                ServerStatusText.Text = S.ListeningOn + ip + ":" + ProtocolConstants.Port;
                LocalIpText.Text = ip;
                StatusDot.Fill = BrushGreen;
                AppLog.I(TAG, "Server listening on " + ip + ":" + ProtocolConstants.Port);
            }
            catch (Exception ex)
            {
                AppLog.E("App", "Server failed", ex);
                ServerStatusText.Text = "Error: " + ex.Message;
                StatusDot.Fill = BrushRed;
            }
        }

        private async Task StartDiscoveryAsync()
        {
            try
            {
                await _discovery.StartAsync();
            }
            catch (Exception ex)
            {
                AppLog.E("App", "Discovery failed", ex);
            }
        }

        public async Task ResumeServicesAsync()
        {
            if (_server == null || _discovery == null || _localDevice == null)
            {
                AppLog.V(TAG, "ResumeServices skipped — services not yet initialized");
                return;
            }
            AppLog.I(TAG, "Resuming services...");
            try
            {
                await _server.StartAsync();
                await _discovery.StartAsync();
                string ip = DiscoveryService.GetLocalIpAddress();
                ServerStatusText.Text = S.ListeningOn + ip + ":" + ProtocolConstants.Port;
                LocalIpText.Text = ip;
                StatusDot.Fill = BrushGreen;
                AppLog.I(TAG, "Services resumed — listening on " + ip);
            }
            catch (Exception ex)
            {
                AppLog.E(TAG, "Resume failed", ex);
                ServerStatusText.Text = "Error al reanudar: " + ex.Message;
            }
        }

        public void SuspendServices()
        {
            // Called from App.OnSuspending — must never throw, or the
            // suspend path itself becomes a crash.
            try
            {
                StopRefreshTimer();
                if (_server != null) _server.Stop();
                if (_discovery != null) _discovery.Stop();
                ServerStatusText.Text = S.Suspended;
                StatusDot.Fill = BrushGray;
                AppLog.I(TAG, "Services suspended");
            }
            catch (Exception ex)
            {
                AppLog.E(TAG, "SuspendServices failed", ex);
            }
        }

        #region Navigation

        private void HistoryButton_Click(object sender, RoutedEventArgs e)
        {
            AppLog.V(TAG, "Navigating to HistoryPage");
            Frame.Navigate(typeof(HistoryPage));
        }

        private void AboutButton_Click(object sender, RoutedEventArgs e)
        {
            AppLog.V(TAG, "Navigating to AboutPage");
            Frame.Navigate(typeof(AboutPage));
        }

        #endregion

        #region Device Discovery UI

        private async void OnDevicesChanged()
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                RefreshDeviceList();
            });
        }

        private void RefreshDeviceList()
        {
            if (_discovery == null) return;

            var devices = _discovery.Tracker.GetAll();
            DevicesList.ItemsSource = devices;

            bool hasDevices = devices.Count > 0;
            NoDevicesText.Visibility = hasDevices ? Visibility.Collapsed : Visibility.Visible;
            DeviceCountText.Text = hasDevices ? devices.Count.ToString() : "";
        }

        private void StartRefreshTimer()
        {
            if (_refreshTimer != null) return;
            _refreshTimer = new DispatcherTimer();
            _refreshTimer.Interval = TimeSpan.FromSeconds(3);
            _refreshTimer.Tick += (s, e) => RefreshDeviceList();
            _refreshTimer.Start();
        }

        private void StopRefreshTimer()
        {
            if (_refreshTimer != null)
            {
                _refreshTimer.Stop();
                _refreshTimer = null;
            }
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            if (_discovery == null) return;

            RefreshButton.IsEnabled = false;
            NoDevicesText.Text = S.Searching;
            AppLog.I(TAG, "Manual device refresh requested");

            // async-void handler — an unhandled exception here crashes the app.
            try
            {
                await _discovery.SendAnnouncementAsync();
                await _discovery.RunHttpScanAsync();
                RefreshDeviceList();
                AppLog.I(TAG, "Manual refresh done — " + _discovery.Tracker.Count + " device(s)");
            }
            catch (Exception ex)
            {
                AppLog.E(TAG, "Manual refresh failed", ex);
            }
            finally
            {
                RefreshButton.IsEnabled = true;
            }
        }

        #endregion

        #region Sending Files

        private void DevicesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateSendButtonState();
        }

        private void PickFilesButton_Click(object sender, RoutedEventArgs e)
        {
            // Opening the picker suspends this app. A double-tap would call
            // PickMultipleFilesAndContinue twice — the second call throws,
            // and since this is a plain (non-async) void handler the
            // exception is unhandled and crashes the app. Guard both the
            // re-entrancy and the throw.
            if (_pickerInFlight)
            {
                AppLog.W(TAG, "File picker already opening — tap ignored");
                return;
            }

            try
            {
                _pickerInFlight = true;
                AppLog.I(TAG, "Opening file picker");

                var picker = new FileOpenPicker();
                picker.ViewMode = PickerViewMode.List;
                picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                picker.FileTypeFilter.Add("*");
                picker.PickMultipleFilesAndContinue();
            }
            catch (Exception ex)
            {
                _pickerInFlight = false;
                AppLog.E(TAG, "Failed to open file picker", ex);
                var ignore = ShowMessageAsync(S.Error, ex.Message);
            }
        }

        public void ContinueFileOpenPicker(FileOpenPickerContinuationEventArgs args)
        {
            // Picker returned (with files or cancelled) — release the guard.
            _pickerInFlight = false;

            try
            {
                int count = args != null && args.Files != null ? args.Files.Count : 0;
                AppLog.I(TAG, "File picker returned " + count + " file(s)");
                if (count == 0)
                    return;

                _selectedFiles.Clear();
                foreach (var f in args.Files)
                {
                    if (f != null)
                        _selectedFiles.Add(f);
                }

                SelectedFilesText.Text = S.FilesSelected(_selectedFiles.Count);
                UpdateSendButtonState();

                // After a cold reactivation the frame lands on tab 0; move
                // the user to the Send tab so the selection is visible.
                if (MainPivot != null)
                    MainPivot.SelectedIndex = 1;
            }
            catch (Exception ex)
            {
                AppLog.E(TAG, "ContinueFileOpenPicker failed", ex);
            }
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedDevice = DevicesList.SelectedItem as DiscoveredDevice;
            if (selectedDevice == null || _selectedFiles.Count == 0) return;

            // After a cold reactivation from the file picker, services
            // initialize on a background dispatcher. Don't let a fast tap
            // run the send path against half-built state.
            if (!_servicesStarted || _client == null)
            {
                AppLog.W(TAG, "Send tapped before services were ready — ignoring");
                await ShowMessageAsync(S.SendError, S.Starting);
                return;
            }
            if (string.IsNullOrEmpty(selectedDevice.IpAddress))
            {
                AppLog.W(TAG, "Send tapped but device has no IP address — ignoring");
                return;
            }

            SendButton.IsEnabled = false;
            AppLog.I(TAG, "Send requested: " + _selectedFiles.Count + " file(s) to " +
                selectedDevice.Alias + " at " + selectedDevice.IpAddress);
            TransferSession session = null;

            try
            {
                var fileDtos = new List<FileDto>();
                var storageFiles = new Dictionary<string, StorageFile>();

                foreach (var file in _selectedFiles)
                {
                    string id = Guid.NewGuid().ToString();
                    var props = await file.GetBasicPropertiesAsync();

                    var dto = new FileDto
                    {
                        Id = id,
                        FileName = file.Name,
                        Size = (long)props.Size,
                        FileType = FileDto.GuessMimeType(file.Name)
                    };
                    fileDtos.Add(dto);
                    storageFiles[id] = file;
                }

                session = new TransferSession();
                session.RemoteDevice = selectedDevice.Info;
                foreach (var dto in fileDtos)
                    session.Files[dto.Id] = new FileTransferInfo(dto, "");

                var cts = new CancellationTokenSource();

                Frame.Navigate(typeof(TransferPage), new TransferPageArgs
                {
                    Session = session,
                    CancellationSource = cts
                });

                await _client.SendFilesAsync(selectedDevice, fileDtos, storageFiles,
                    new Progress<TransferProgress>(p =>
                    {
                        var ignore = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                        {
                            var transferPage = Frame.Content as TransferPage;
                            if (transferPage != null)
                                transferPage.UpdateProgress();
                        });
                    }), cts.Token);

                // Final UI update after transfer completes
                session.State = session.HasErrors ? TransferState.Failed : TransferState.Completed;

                // Save to history
                await TransferHistory.AddSentFilesAsync(
                    selectedDevice.Alias, fileDtos);

                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    var transferPage = Frame.Content as TransferPage;
                    if (transferPage != null)
                        transferPage.UpdateProgress();
                });
            }
            catch (OperationCanceledException)
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    var transferPage = Frame.Content as TransferPage;
                    if (transferPage != null)
                        transferPage.UpdateProgress();
                });
            }
            catch (Exception ex)
            {
                AppLog.E("App", "Send failed", ex);
                if (session != null) session.State = TransferState.Failed;
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    var transferPage = Frame.Content as TransferPage;
                    if (transferPage != null)
                        transferPage.UpdateProgress();
                });
                await ShowMessageAsync(S.SendError, ex.Message);
            }
            finally
            {
                SendButton.IsEnabled = true;
            }
        }

        private void UpdateSendButtonState()
        {
            // Gate on service readiness so the Send buttons can never be
            // tapped while _client / _server are still null.
            bool ready = _servicesStarted && _client != null;
            bool hasDevice = DevicesList.SelectedItem != null;
            SendButton.IsEnabled = ready && hasDevice && _selectedFiles.Count > 0;
            SendTextButton.IsEnabled = ready && hasDevice && !string.IsNullOrWhiteSpace(TextMessageBox.Text);
        }

        #endregion

        #region Sending Text

        private void TextMessageBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateSendButtonState();
        }

        private async void SendTextButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedDevice = DevicesList.SelectedItem as DiscoveredDevice;
            string text = TextMessageBox.Text;
            if (selectedDevice == null || string.IsNullOrWhiteSpace(text)) return;

            SendTextButton.IsEnabled = false;
            try
            {
                await _client.SendTextAsync(selectedDevice, text, CancellationToken.None);
                TextMessageBox.Text = "";
                await ShowMessageAsync(S.Sent, S.MessageSentTo(selectedDevice.Alias));
            }
            catch (Exception ex)
            {
                AppLog.E("App", "Text send failed", ex);
                await ShowMessageAsync(S.Error, ex.Message);
            }
            finally
            {
                SendTextButton.IsEnabled = true;
            }
        }

        #endregion

        #region Receiving Files

        private async void OnIncomingTransfer(TransferSession session)
        {
            AppLog.I("App", "OnIncomingTransfer: " + session.TotalFiles + " files from " +
                (session.RemoteDevice != null ? session.RemoteDevice.Alias : "null"));

            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                try
                {
                    // Show receive progress panel
                    ReceiveProgressPanel.Visibility = Visibility.Visible;
                    string senderName = session.RemoteDevice != null
                        ? session.RemoteDevice.Alias : "Unknown";
                    ReceiveProgressTitle.Text = S.RequestFrom(senderName);
                    ReceiveProgressBar.Value = 0;
                    ReceiveProgressText.Text = session.TotalFiles + " file(s), " +
                        FileSizeConverter.Format(session.TotalBytes);

                    MainPivot.SelectedIndex = 0;

                    string sizeText = FileSizeConverter.Format(session.TotalBytes);

                    var dialog = new MessageDialog(
                        S.WantsToSend(senderName, session.TotalFiles, sizeText),
                        S.IncomingTransfer);
                    dialog.Commands.Add(new UICommand(S.Accept, cmd =>
                    {
                        _server.AcceptTransfer();
                        ReceiveProgressTitle.Text = S.ReceivingFrom(senderName);
                    }));
                    dialog.Commands.Add(new UICommand(S.Decline, cmd =>
                    {
                        _server.DeclineTransfer();
                        ReceiveProgressPanel.Visibility = Visibility.Collapsed;
                    }));
                    dialog.DefaultCommandIndex = 0;
                    dialog.CancelCommandIndex = 1;

                    await dialog.ShowAsync();
                }
                catch (Exception ex)
                {
                    // MessageDialog.ShowAsync throws if another dialog is
                    // already on screen; an unhandled throw in this
                    // async-void callback crashes the receiving device.
                    // Decline so the sender is not left hanging.
                    AppLog.E("App", "Incoming-transfer prompt failed", ex);
                    try { if (_server != null) _server.DeclineTransfer(); }
                    catch { }
                }
            });
        }

        private async void OnFileReceiveProgress(string fileId, long received, long total)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                try
                {
                    if (_server != null && _server.ActiveSession != null)
                    {
                        var session = _server.ActiveSession;
                        double percent = session.TotalBytes > 0
                            ? (session.TransferredBytes * 100.0 / session.TotalBytes)
                            : 0;
                        ReceiveProgressBar.Value = percent;
                        ReceiveProgressText.Text = string.Format("{0}% — {1}/{2} archivos — {3}",
                            (int)percent, session.CompletedFiles, session.TotalFiles,
                            FileSizeConverter.Format(session.TransferredBytes));
                    }
                }
                catch (Exception ex)
                {
                    AppLog.E("App", "Receive-progress UI update failed", ex);
                }
            });
        }

        private async void OnTransferCompleted(TransferSession session)
        {
            AppLog.I("App", "OnTransferCompleted: " + session.CompletedFiles + "/" +
                session.TotalFiles + " state=" + session.State);

            // Save to persistent history
            try
            {
                await TransferHistory.AddReceivedFilesAsync(session);
            }
            catch (Exception ex)
            {
                AppLog.E("App", "Saving received files to history failed", ex);
            }

            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
              try
              {
                // Update received files list
                foreach (var kvp in session.Files)
                {
                    if (kvp.Value.Status == FileTransferStatus.Completed)
                    {
                        _receivedFiles.Insert(0, new ReceivedFileItem
                        {
                            FileName = kvp.Value.File.FileName,
                            SizeText = FileSizeConverter.Format(kvp.Value.File.Size)
                        });
                    }
                }

                ReceivedFilesList.ItemsSource = null;
                ReceivedFilesList.ItemsSource = _receivedFiles;
                NoReceivedFilesText.Visibility = _receivedFiles.Count > 0
                    ? Visibility.Collapsed : Visibility.Visible;

                // Update progress panel
                ReceiveProgressBar.Value = 100;
                ReceiveProgressTitle.Text = session.HasErrors
                    ? S.CompletedWithErrors
                    : S.TransferComplete;
                ReceiveProgressText.Text = S.FilesReceived(session.CompletedFiles);

                // Restore server status
                string ip = DiscoveryService.GetLocalIpAddress();
                ServerStatusText.Text = S.ListeningOn + ip + ":" + ProtocolConstants.Port;

                string resultMsg = session.HasErrors
                    ? S.FilesReceivedWithErrors(session.CompletedFiles)
                    : S.FilesReceived(session.CompletedFiles);
                await ShowMessageAsync(S.TransferComplete, resultMsg);
              }
              catch (Exception ex)
              {
                  AppLog.E("App", "Transfer-completed UI update failed", ex);
              }
            });
        }

        #endregion

        #region Settings

        private void AliasTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            string newAlias = AliasTextBox.Text.Trim();
            if (!string.IsNullOrEmpty(newAlias) && _localDevice != null)
            {
                _localDevice.Alias = newAlias;
                DeviceInfo.SaveAlias(newAlias);
                DeviceAliasDisplay.Text = newAlias;
            }
        }

        private void RefreshStorageButtons()
        {
            ImageDestButton.Content = StorageConfig.GetDestinationLabel("image");
            VideoDestButton.Content = StorageConfig.GetDestinationLabel("video");
            AudioDestButton.Content = StorageConfig.GetDestinationLabel("audio");
            OtherDestButton.Content = StorageConfig.GetDestinationLabel("other");

            ImagePathHint.Text = StorageConfig.GetDestinationPath("image");
            VideoPathHint.Text = StorageConfig.GetDestinationPath("video");
            AudioPathHint.Text = StorageConfig.GetDestinationPath("audio");
            OtherPathHint.Text = StorageConfig.GetDestinationPath("other");

            CheckLegacyFiles();
        }

        private void ToggleDestination(string category)
        {
            string current;
            switch (category)
            {
                case "image":
                    current = StorageConfig.GetImageDestination();
                    StorageConfig.SetImageDestination(
                        current == StorageConfig.DestLibrary ? StorageConfig.DestLocal : StorageConfig.DestLibrary);
                    break;
                case "video":
                    current = StorageConfig.GetVideoDestination();
                    StorageConfig.SetVideoDestination(
                        current == StorageConfig.DestLibrary ? StorageConfig.DestLocal : StorageConfig.DestLibrary);
                    break;
                case "audio":
                    current = StorageConfig.GetAudioDestination();
                    StorageConfig.SetAudioDestination(
                        current == StorageConfig.DestLibrary ? StorageConfig.DestLocal : StorageConfig.DestLibrary);
                    break;
                case "other":
                    StorageConfig.SetOtherDestination(
                        StorageConfig.NextOtherDestination(_sdAvailable));
                    break;
            }
            RefreshStorageButtons();
        }

        private void ImageDestButton_Click(object sender, RoutedEventArgs e) { ToggleDestination("image"); }
        private void VideoDestButton_Click(object sender, RoutedEventArgs e) { ToggleDestination("video"); }
        private void AudioDestButton_Click(object sender, RoutedEventArgs e) { ToggleDestination("audio"); }
        private void OtherDestButton_Click(object sender, RoutedEventArgs e) { ToggleDestination("other"); }

        private async void CheckLegacyFiles()
        {
            int count = await StorageConfig.CountLegacyFilesAsync();
            if (count > 0)
            {
                MigrateButton.Content = S.MigrateFiles(count);
                MigrateButton.Visibility = Visibility.Visible;
            }
            else
            {
                MigrateButton.Visibility = Visibility.Collapsed;
            }
            MigrateStatusText.Visibility = Visibility.Collapsed;
            MigrateProgress.Visibility = Visibility.Collapsed;
        }

        private async void MigrateButton_Click(object sender, RoutedEventArgs e)
        {
            MigrateButton.IsEnabled = false;
            MigrateButton.Content = S.Migrating;
            MigrateProgress.Visibility = Visibility.Visible;
            MigrateProgress.Value = 0;
            MigrateStatusText.Visibility = Visibility.Collapsed;

            int total = await StorageConfig.CountLegacyFilesAsync();
            MigrateProgress.Maximum = total > 0 ? total : 1;

            var result = await StorageConfig.MigrateFilesAsync(new Progress<int>(n =>
            {
                var ignore = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    MigrateProgress.Value = n;
                });
            }));

            MigrateProgress.Value = MigrateProgress.Maximum;

            if (result.InteropFailed)
            {
                MigrateStatusText.Text = S.MigrationNoInterop;
                MigrateStatusText.Visibility = Visibility.Visible;
                MigrateButton.Content = S.MigrateFiles(result.Total);
                MigrateButton.IsEnabled = true;
            }
            else
            {
                MigrateStatusText.Text = S.MigrationDone(result.Moved, result.Failed);
                MigrateStatusText.Visibility = Visibility.Visible;

                // Re-check if there are still files left (failed ones)
                int remaining = await StorageConfig.CountLegacyFilesAsync();
                if (remaining > 0)
                {
                    MigrateButton.Content = S.MigrateFiles(remaining);
                    MigrateButton.IsEnabled = true;
                }
                else
                {
                    MigrateButton.Visibility = Visibility.Collapsed;
                    MigrateProgress.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void LangEsButton_Click(object sender, RoutedEventArgs e)
        {
            S.SetLanguage("es");
            ApplyStrings();
        }

        private void LangEnButton_Click(object sender, RoutedEventArgs e)
        {
            S.SetLanguage("en");
            ApplyStrings();
        }

        /// <summary>
        /// Apply all localized strings to UI elements.
        /// Called on startup and when language changes.
        /// </summary>
        private void ApplyStrings()
        {
            // Pivot headers
            Tab1Header.Header = S.IsEnglish ? "receive" : "recibir";
            Tab2Header.Header = S.IsEnglish ? "send" : "enviar";
            Tab3Header.Header = S.IsEnglish ? "settings" : "ajustes";

            // Receive tab
            HistoryBtn.Content = S.History;
            AboutBtn.Content = S.About;
            ReceivedFilesLabel.Text = S.ReceivedFiles;
            NoReceivedFilesText.Text = S.WaitingForFiles;

            // Send tab
            SelectedFilesText.Text = S.NoFilesSelected;
            TextMessageBox.PlaceholderText = S.TextMessagePlaceholder;
            NearbyDevicesLabel.Text = S.NearbyDevices;
            NoDevicesText.Text = S.SearchingDevices;
            RefreshButton.Content = S.Search;
            SendButton.Content = S.SendFiles;

            // Settings tab
            DeviceSectionLabel.Text = S.Device;
            NameLabel.Text = S.Name;
            AliasTextBox.PlaceholderText = S.MyDevice;
            ModelLabel.Text = S.Model;
            NetworkSectionLabel.Text = S.Network;
            PortLabel.Text = S.Port + "53317";
            ProtocolLabel.Text = S.Protocol + "HTTP";
            StorageSectionLabel.Text = S.Storage;
            ImagesLabel.Text = S.Images;
            VideosLabel.Text = S.Videos;
            AudioLabel.Text = S.Audio;
            OtherFilesLabel.Text = S.OtherFiles;
            LangSectionLabel.Text = S.Language;
            AboutBtn2.Content = S.About;

            // Server status — refresh with current language
            if (_servicesStarted && _server != null)
            {
                string ip = DiscoveryService.GetLocalIpAddress();
                ServerStatusText.Text = S.ListeningOn + ip + ":" + ProtocolConstants.Port;
            }
            else
            {
                ServerStatusText.Text = S.Starting;
            }

            RefreshStorageButtons();
        }

        #endregion

        private async Task ShowMessageAsync(string title, string message)
        {
            try
            {
                var dialog = new MessageDialog(message, title);
                await dialog.ShowAsync();
            }
            catch { }
        }
    }

    internal sealed class ReceivedFileItem
    {
        public string FileName { get; set; }
        public string SizeText { get; set; }
    }
}
