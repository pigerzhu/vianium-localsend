// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0
// SPDX-FileCopyrightText: 2026 Angel Careaga <hello@angelcareaga.com>
// SPDX-FileCopyrightText: 2026 pigerzhu

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
using Windows.UI.Xaml.Input;
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
        private const int MaxReceivePreviewItems = 5;

        private DeviceInfo _localDevice;
        private DiscoveryService _discovery;
        private LocalSendServer _server;
        private LocalSendClient _client;
        private DispatcherTimer _refreshTimer;
        private DispatcherTimer _incomingRequestTimer;

        private List<StorageFile> _selectedFiles;
        private List<ReceivedFileItem> _receivedFiles;
        private TransferSession _pendingIncomingSession;
        private TransferPageArgs _activeTransferArgs;
        private int _receivedFileCount;
        private long _totalBytesReceived;
        private bool _servicesStarted;
        private bool _sdAvailable;
        private bool _pickerInFlight;
        private bool _sendToMultiple;
        private bool _settingsLoading;

        private static readonly SolidColorBrush BrushAccent =
            new SolidColorBrush(Windows.UI.Color.FromArgb(255, 27, 161, 226));
        private static readonly SolidColorBrush BrushGreen =
            new SolidColorBrush(Windows.UI.Color.FromArgb(255, 63, 185, 80));
        private static readonly SolidColorBrush BrushRed =
            new SolidColorBrush(Windows.UI.Color.FromArgb(255, 248, 81, 73));
        private static readonly SolidColorBrush BrushGray =
            new SolidColorBrush(Windows.UI.Color.FromArgb(255, 139, 148, 158));
        private static readonly SolidColorBrush BrushTransparent =
            new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
        private static readonly SolidColorBrush BrushWhite =
            new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 255));

        public MainPage()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = NavigationCacheMode.Required;
            _selectedFiles = new List<StorageFile>();
            _receivedFiles = new List<ReceivedFileItem>();
            ApplyDeviceSelectionMode();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            AppLog.V(TAG, "OnNavigatedTo — servicesStarted=" + _servicesStarted);

            EqStoryboard.Begin();

            if (!_servicesStarted)
            {
                // Apply localized strings + cached alias immediately
                ApplyStrings();
                string alias = DeviceInfo.GetSavedAlias() ?? "Windows Phone";
                AliasTextBox.Text = alias;
                DeviceAliasDisplay.Text = alias;
                DeviceAliasSettings.Text = alias;
                ServerStatusText.Text = S.Starting;

                LoadPreferenceSettings();

                if (IsAutostartEnabled())
                {
                    // Defer all networking to Low priority — page renders first
                    var ignore = Dispatcher.RunAsync(CoreDispatcherPriority.Low, async () =>
                    {
                        await InitializeServicesAsync();
                    });
                }
                else
                {
                    SetReceiverStoppedByPreference();
                }
            }
            else
            {
                LoadPreferenceSettings();
                if (IsAutostartEnabled())
                {
                    StartRefreshTimer();
                }
                else
                {
                    SuspendServices();
                    SetReceiverStoppedByPreference();
                }
            }

            await LoadReceivedSummaryAsync();
            UpdateActiveTransferBanner();
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
                DeviceAliasSettings.Text = _localDevice.Alias;
                DeviceModelDisplay.Text = _localDevice.DeviceModel ?? "";
                DeviceModelText.Text = _localDevice.DeviceModel ?? "";
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
                    ServerStatusText.Text = S.ServerErrorMessage(ex.Message);
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
                LocalIpText.Text = FormatReceiveAddress(ip);
                SetReadyToReceiveStatus();
                AppLog.I(TAG, "Server listening on " + ip + ":" + ProtocolConstants.Port);
            }
            catch (Exception ex)
            {
                AppLog.E("App", "Server failed", ex);
                ServerStatusText.Text = S.ServerErrorMessage(ex.Message);
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
                LocalIpText.Text = FormatReceiveAddress(ip);
                SetReadyToReceiveStatus();
                AppLog.I(TAG, "Services resumed — listening on " + ip);
            }
            catch (Exception ex)
            {
                AppLog.E(TAG, "Resume failed", ex);
                ServerStatusText.Text = S.ResumeErrorMessage(ex.Message);
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
                HideIncomingRequest();
                ServerStatusText.Text = S.Suspended;
                StatusDot.Fill = BrushGray;
                AppLog.I(TAG, "Services suspended");
            }
            catch (Exception ex)
            {
                AppLog.E(TAG, "SuspendServices failed", ex);
            }
        }

        private void SetReadyToReceiveStatus()
        {
            ServerStatusText.Text = S.ReadyToReceive;
            StatusDot.Fill = BrushGreen;
        }

        private static string FormatReceiveAddress(string ip)
        {
            if (string.IsNullOrWhiteSpace(ip) || ip == "Unknown")
                return "";

            return ip + ":" + ProtocolConstants.Port;
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

            var selectedFingerprints = GetSelectedDeviceFingerprints();
            var devices = _discovery.Tracker.GetAll();
            DevicesList.ItemsSource = devices;
            RestoreDeviceSelection(devices, selectedFingerprints);

            bool hasDevices = devices.Count > 0;
            NoDevicesText.Visibility = hasDevices ? Visibility.Collapsed : Visibility.Visible;
            DeviceCountText.Text = hasDevices ? devices.Count.ToString() : "";
            NearbyCountText.Text = devices.Count.ToString();
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
            var selectedDevices = GetSelectedDevices();
            if (selectedDevices.Count == 0 || _selectedFiles.Count == 0) return;

            // After a cold reactivation from the file picker, services
            // initialize on a background dispatcher. Don't let a fast tap
            // run the send path against half-built state.
            if (!_servicesStarted || _client == null)
            {
                AppLog.W(TAG, "Send tapped before services were ready — ignoring");
                await ShowMessageAsync(S.SendError, S.Starting);
                return;
            }
            SendButton.IsEnabled = false;
            try
            {
                foreach (var selectedDevice in selectedDevices)
                {
                    if (string.IsNullOrEmpty(selectedDevice.IpAddress))
                    {
                        AppLog.W(TAG, "Send skipped: device has no IP address");
                        continue;
                    }

                    await SendFilesToDeviceAsync(selectedDevice);
                }
            }
            catch (OperationCanceledException)
            {
                AppLog.I(TAG, "Send cancelled by user");
            }
            catch (Exception ex)
            {
                AppLog.E("App", "Send failed", ex);
                await ShowMessageAsync(S.SendError, ex.Message);
            }
            finally
            {
                UpdateSendButtonState();
            }
        }

        private async Task SendFilesToDeviceAsync(DiscoveredDevice selectedDevice)
        {
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

                var transferArgs = new TransferPageArgs
                {
                    Session = session,
                    CancellationSource = cts,
                    IsReceive = false,
                    CancelAction = () => cts.Cancel(),
                    DismissAction = () => ClearActiveTransfer(session)
                };
                SetActiveTransfer(transferArgs);
                Frame.Navigate(typeof(TransferPage), transferArgs);

                await _client.SendFilesAsync(selectedDevice, fileDtos, storageFiles,
                    new Progress<TransferProgress>(p =>
                    {
                        var ignore = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                        {
                            var transferPage = Frame.Content as TransferPage;
                            if (transferPage != null)
                                transferPage.UpdateProgress();
                            else
                                UpdateActiveTransferBanner();
                        });
                    }), cts.Token, session);

                session.State = session.HasErrors ? TransferState.Failed : TransferState.Completed;

                await TransferHistory.AddSentFilesAsync(
                    selectedDevice.Alias, fileDtos);

                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    var transferPage = Frame.Content as TransferPage;
                    if (transferPage != null)
                        transferPage.UpdateProgress();
                    else
                        UpdateActiveTransferBanner();
                });
            }
            catch (OperationCanceledException)
            {
                if (session != null) session.State = TransferState.Cancelled;
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    var transferPage = Frame.Content as TransferPage;
                    if (transferPage != null)
                        transferPage.UpdateProgress();
                    else
                        UpdateActiveTransferBanner();
                });
                throw;
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
                    else
                        UpdateActiveTransferBanner();
                });
                throw;
            }
        }

        private void UpdateSendButtonState()
        {
            // Gate on service readiness so the Send buttons can never be
            // tapped while _client / _server are still null.
            bool ready = _servicesStarted && _client != null;
            bool hasDevice = GetSelectedDevices().Count > 0;
            bool hasFiles = _selectedFiles.Count > 0;

            SendButton.Visibility = hasFiles ? Visibility.Visible : Visibility.Collapsed;
            SendButton.IsEnabled = ready && hasDevice && hasFiles;
            SendTextButton.IsEnabled = ready && hasDevice && !string.IsNullOrWhiteSpace(TextMessageBox.Text);
        }

        private List<DiscoveredDevice> GetSelectedDevices()
        {
            var selected = new List<DiscoveredDevice>();
            if (DevicesList == null) return selected;

            if (_sendToMultiple)
            {
                foreach (var item in DevicesList.SelectedItems)
                {
                    var device = item as DiscoveredDevice;
                    if (device != null)
                        selected.Add(device);
                }
            }
            else
            {
                var device = DevicesList.SelectedItem as DiscoveredDevice;
                if (device != null)
                    selected.Add(device);
            }

            return selected;
        }

        private List<string> GetSelectedDeviceFingerprints()
        {
            var fingerprints = new List<string>();
            var selected = GetSelectedDevices();
            foreach (var device in selected)
            {
                if (device.Info != null && !string.IsNullOrEmpty(device.Info.Fingerprint))
                    fingerprints.Add(device.Info.Fingerprint);
            }
            return fingerprints;
        }

        private void RestoreDeviceSelection(List<DiscoveredDevice> devices, List<string> fingerprints)
        {
            if (devices == null || fingerprints == null || fingerprints.Count == 0)
                return;

            foreach (var device in devices)
            {
                if (device.Info == null || string.IsNullOrEmpty(device.Info.Fingerprint))
                    continue;
                if (!fingerprints.Contains(device.Info.Fingerprint))
                    continue;

                if (_sendToMultiple)
                {
                    DevicesList.SelectedItems.Add(device);
                }
                else
                {
                    DevicesList.SelectedItem = device;
                    break;
                }
            }
        }

        private void ApplyDeviceSelectionMode()
        {
            if (DevicesList != null)
            {
                var selected = GetSelectedDevices();
                DevicesList.SelectionMode = _sendToMultiple
                    ? ListViewSelectionMode.Multiple
                    : ListViewSelectionMode.Single;

                if (!_sendToMultiple && selected.Count > 0)
                    DevicesList.SelectedItem = selected[0];
            }

            if (DeviceSelectionModeText != null)
                DeviceSelectionModeText.Text = _sendToMultiple
                    ? S.SelectionModeMultiple
                    : S.SelectionModeSingle;

            if (DeviceSelectionModeButton != null)
            {
                DeviceSelectionModeButton.Background = _sendToMultiple ? BrushAccent : BrushTransparent;
                DeviceSelectionModeButton.BorderBrush = BrushAccent;
                DeviceSelectionModeButton.Foreground = _sendToMultiple ? BrushWhite : BrushAccent;
            }

            if (DeviceSelectionModeText != null)
                DeviceSelectionModeText.Foreground = _sendToMultiple ? BrushWhite : BrushAccent;

            UpdateSendButtonState();
        }

        private void DeviceSelectionModeButton_Click(object sender, RoutedEventArgs e)
        {
            _sendToMultiple = !_sendToMultiple;
            ApplyDeviceSelectionMode();
        }

        #endregion

        #region Sending Text

        private void TextMessageBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateSendButtonState();
        }

        private async void SendTextButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedDevices = GetSelectedDevices();
            string text = TextMessageBox.Text;
            if (selectedDevices.Count == 0 || string.IsNullOrWhiteSpace(text)) return;

            SendTextButton.IsEnabled = false;
            try
            {
                int sentCount = 0;
                string firstSentAlias = null;
                foreach (var selectedDevice in selectedDevices)
                {
                    if (string.IsNullOrEmpty(selectedDevice.IpAddress))
                        continue;
                    await _client.SendTextAsync(selectedDevice, text, CancellationToken.None);
                    if (firstSentAlias == null)
                        firstSentAlias = selectedDevice.Alias;
                    sentCount++;
                }

                if (sentCount == 0)
                    return;

                TextMessageBox.Text = "";
                string message = sentCount == 1
                    ? S.MessageSentTo(firstSentAlias)
                    : S.MessageSentToDevices(sentCount);
                await ShowMessageAsync(S.Sent, message);
            }
            catch (Exception ex)
            {
                AppLog.E("App", "Text send failed", ex);
                await ShowMessageAsync(S.Error, ex.Message);
            }
            finally
            {
                UpdateSendButtonState();
            }
        }

        #endregion

        #region Receiving Files

        private async Task LoadReceivedSummaryAsync()
        {
            try
            {
                var entries = await TransferHistory.LoadAsync();

                _receivedFiles.Clear();
                _receivedFileCount = 0;
                _totalBytesReceived = 0;

                foreach (var entry in entries)
                {
                    if (entry == null || entry.IsSent)
                        continue;

                    _receivedFileCount++;
                    _totalBytesReceived += entry.FileSize;

                    if (_receivedFiles.Count < MaxReceivePreviewItems)
                    {
                        _receivedFiles.Add(new ReceivedFileItem
                        {
                            FileName = entry.FileName,
                            SizeText = entry.SizeText
                        });
                    }
                }

                RefreshReceivedSummaryUi();
            }
            catch (Exception ex)
            {
                AppLog.E(TAG, "Could not refresh received summary", ex);
            }
        }

        private void RefreshReceivedSummaryUi()
        {
            ReceivedFilesList.ItemsSource = null;
            ReceivedFilesList.ItemsSource = _receivedFiles;

            bool hasFiles = _receivedFileCount > 0;
            EmptyReceivePanel.Visibility = hasFiles ? Visibility.Collapsed : Visibility.Visible;
            ReceivedFilesList.Visibility = _receivedFiles.Count > 0
                ? Visibility.Visible
                : Visibility.Collapsed;
            ReceivedCountText.Text = _receivedFileCount.ToString();
            TotalBytesText.Text = FileSizeConverter.Format(_totalBytesReceived);
        }

        private static string GetDeviceDisplayName(DeviceInfo device)
        {
            if (device != null && !string.IsNullOrWhiteSpace(device.Alias))
                return device.Alias;

            return S.UnknownDevice;
        }

        private void ShowIncomingRequest(TransferSession session)
        {
            if (session == null) return;

            _pendingIncomingSession = session;

            string senderName = GetDeviceDisplayName(session.RemoteDevice);
            string senderModel = session.RemoteDevice != null
                ? session.RemoteDevice.DeviceModel
                : "";

            IncomingRequestSenderNameText.Text = senderName;
            IncomingRequestSenderModelText.Text = senderModel ?? "";
            IncomingRequestSenderModelText.Visibility = string.IsNullOrWhiteSpace(senderModel)
                ? Visibility.Collapsed
                : Visibility.Visible;
            IncomingRequestFilesList.ItemsSource = BuildIncomingRequestFiles(session);

            ApplyIncomingRequestStrings();

            IncomingAcceptButton.IsEnabled = true;
            IncomingRejectButton.IsEnabled = true;
            ReceiveProgressPanel.Visibility = Visibility.Collapsed;
            IncomingRequestOverlay.Visibility = Visibility.Visible;
            StartIncomingRequestTimer();
        }

        private void ApplyIncomingRequestStrings()
        {
            IncomingRequestHeaderText.Text = S.ReceiveRequestHeader;
            IncomingRequestSummaryPrefixRun.Text = S.WantsToSendYouPrefix;
            IncomingRejectButton.Content = S.Decline.ToUpperInvariant();
            IncomingAcceptButton.Content = S.Accept.ToUpperInvariant();

            if (_pendingIncomingSession == null) return;

            IncomingRequestSummaryCountRun.Text = S.FileCount(_pendingIncomingSession.TotalFiles);
            IncomingRequestSummarySizeRun.Text = " \u00b7 " +
                FileSizeConverter.Format(_pendingIncomingSession.TotalBytes);
        }

        private List<IncomingRequestFileItem> BuildIncomingRequestFiles(TransferSession session)
        {
            var items = new List<IncomingRequestFileItem>();
            foreach (var file in session.Files.Values)
            {
                if (file == null || file.File == null)
                    continue;

                items.Add(new IncomingRequestFileItem
                {
                    FileIcon = GetFileIcon(file.File),
                    FileName = file.File.FileName,
                    SizeText = FileSizeConverter.Format(file.File.Size)
                });
            }

            return items;
        }

        private static string GetFileIcon(FileDto file)
        {
            string fileType = file.FileType ?? "";
            if (fileType.StartsWith("image/"))
                return "\uE114";
            if (fileType.StartsWith("video/"))
                return "\uE116";
            if (fileType.StartsWith("audio/"))
                return "\uE189";

            string name = file.FileName ?? "";
            string lower = name.ToLowerInvariant();
            if (lower.EndsWith(".jpg") || lower.EndsWith(".jpeg") ||
                lower.EndsWith(".png") || lower.EndsWith(".gif") ||
                lower.EndsWith(".bmp"))
                return "\uE114";
            if (lower.EndsWith(".mp4") || lower.EndsWith(".avi") ||
                lower.EndsWith(".mkv") || lower.EndsWith(".mov"))
                return "\uE116";

            return "\uE160";
        }

        private void StartIncomingRequestTimer()
        {
            StopIncomingRequestTimer();

            _incomingRequestTimer = new DispatcherTimer();
            _incomingRequestTimer.Interval = TimeSpan.FromSeconds(58);
            _incomingRequestTimer.Tick += IncomingRequestTimer_Tick;
            _incomingRequestTimer.Start();
        }

        private void StopIncomingRequestTimer()
        {
            if (_incomingRequestTimer == null) return;

            _incomingRequestTimer.Stop();
            _incomingRequestTimer.Tick -= IncomingRequestTimer_Tick;
            _incomingRequestTimer = null;
        }

        private void IncomingRequestTimer_Tick(object sender, object e)
        {
            StopIncomingRequestTimer();
            if (_pendingIncomingSession != null)
            {
                AppLog.W(TAG, "Incoming request timed out in UI");
                try { if (_server != null) _server.DeclineTransfer(); }
                catch { }
            }

            HideIncomingRequest();
        }

        private void HideIncomingRequest()
        {
            StopIncomingRequestTimer();
            IncomingRequestOverlay.Visibility = Visibility.Collapsed;
            IncomingRequestFilesList.ItemsSource = null;
            _pendingIncomingSession = null;
        }

        private async void OnIncomingTransfer(TransferSession session)
        {
            AppLog.I("App", "OnIncomingTransfer: " + session.TotalFiles + " files from " +
                (session.RemoteDevice != null ? session.RemoteDevice.Alias : "null"));

            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                try
                {
                    if (IsQuickSaveEnabled())
                    {
                        AppLog.I(TAG, "QuickSave enabled - auto accepting incoming transfer");
                        BeginReceiveTransfer(session);
                        return;
                    }

                    ShowIncomingRequest(session);
                    MainPivot.SelectedIndex = 0;
                }
                catch (Exception ex)
                {
                    AppLog.E("App", "Incoming-transfer view failed", ex);
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
                    var transferPage = Frame.Content as TransferPage;
                    if (transferPage != null)
                    {
                        transferPage.UpdateProgress();
                        return;
                    }

                    if (_server != null && _server.ActiveSession != null)
                    {
                        var session = _server.ActiveSession;
                        double percent = session.TotalBytes > 0
                            ? (session.TransferredBytes * 100.0 / session.TotalBytes)
                            : 0;
                        ReceiveProgressBar.Value = percent;
                        ReceiveProgressText.Text = S.ReceiveProgress(
                            (int)percent,
                            session.CompletedFiles,
                            session.TotalFiles,
                            FileSizeConverter.Format(session.TransferredBytes));
                        UpdateActiveTransferBanner();
                    }
                }
                catch (Exception ex)
                {
                    AppLog.E("App", "Receive-progress UI update failed", ex);
                }
            });
        }

        private void IncomingAcceptButton_Click(object sender, RoutedEventArgs e)
        {
            var session = _pendingIncomingSession;
            if (session == null || _server == null)
                return;

            IncomingAcceptButton.IsEnabled = false;
            IncomingRejectButton.IsEnabled = false;

            BeginReceiveTransfer(session);
        }

        private void BeginReceiveTransfer(TransferSession session)
        {
            if (session == null || _server == null)
                return;

            HideIncomingRequest();
            ReceiveProgressPanel.Visibility = Visibility.Collapsed;
            var transferArgs = new TransferPageArgs
            {
                Session = session,
                IsReceive = true,
                CancelAction = () =>
                {
                    if (_server != null)
                        _server.CancelTransfer();
                },
                DismissAction = () => ClearActiveTransfer(session)
            };
            SetActiveTransfer(transferArgs);
            Frame.Navigate(typeof(TransferPage), transferArgs);
            _server.AcceptTransfer();
        }

        private void IncomingRejectButton_Click(object sender, RoutedEventArgs e)
        {
            IncomingAcceptButton.IsEnabled = false;
            IncomingRejectButton.IsEnabled = false;

            try
            {
                if (_server != null)
                    _server.DeclineTransfer();
            }
            catch { }

            ReceiveProgressPanel.Visibility = Visibility.Collapsed;
            HideIncomingRequest();
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
                    await LoadReceivedSummaryAsync();

                    var transferPage = Frame.Content as TransferPage;
                    if (transferPage != null)
                    {
                        transferPage.UpdateProgress();
                        SetReadyToReceiveStatus();
                        return;
                    }

                    // Update progress panel
                    ReceiveProgressBar.Value = 100;
                    ReceiveProgressTitle.Text = session.HasErrors
                        ? S.CompletedWithErrors
                        : S.TransferComplete;
                    ReceiveProgressText.Text = S.FilesReceived(session.CompletedFiles);
                    UpdateActiveTransferBanner();

                    // Restore server status
                    SetReadyToReceiveStatus();

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

        private void SetActiveTransfer(TransferPageArgs args)
        {
            _activeTransferArgs = args;
            UpdateActiveTransferBanner();
        }

        private void ClearActiveTransfer(TransferSession session)
        {
            if (_activeTransferArgs != null && _activeTransferArgs.Session == session)
                _activeTransferArgs = null;

            UpdateActiveTransferBanner();
        }

        private void RestoreTransferButton_Click(object sender, RoutedEventArgs e)
        {
            RestoreActiveTransfer();
        }

        private void RestoreActiveTransfer()
        {
            if (_activeTransferArgs == null || _activeTransferArgs.Session == null)
            {
                UpdateActiveTransferBanner();
                return;
            }

            ActiveTransferBanner.Visibility = Visibility.Collapsed;
            Frame.Navigate(typeof(TransferPage), _activeTransferArgs);
        }

        private void UpdateActiveTransferBanner()
        {
            if (ActiveTransferBanner == null)
                return;

            if (_activeTransferArgs == null || _activeTransferArgs.Session == null)
            {
                ActiveTransferBanner.Visibility = Visibility.Collapsed;
                return;
            }

            var transferPage = Frame.Content as TransferPage;
            if (transferPage != null)
            {
                ActiveTransferBanner.Visibility = Visibility.Collapsed;
                return;
            }

            var session = _activeTransferArgs.Session;
            ActiveTransferBanner.Visibility = Visibility.Visible;
            ActiveTransferBanner.Background = GetActiveTransferBrush(session.State);
            ActiveTransferTitleText.Text = GetActiveTransferTitle(session, _activeTransferArgs.IsReceive);
            ActiveTransferDetailText.Text = GetActiveTransferDetail(session, _activeTransferArgs.IsReceive);
            RestoreTransferButton.Content = S.Maximize.ToUpperInvariant();
        }

        private Brush GetActiveTransferBrush(TransferState state)
        {
            if (state == TransferState.Completed)
                return BrushGreen;
            if (state == TransferState.Failed)
                return BrushRed;
            if (state == TransferState.Cancelled)
                return BrushGray;
            return BrushAccent;
        }

        private string GetActiveTransferTitle(TransferSession session, bool isReceive)
        {
            if (session.State == TransferState.Completed)
                return S.TransferCompleteTitle;
            if (session.State == TransferState.Failed)
                return S.TransferError.ToLowerInvariant();
            if (session.State == TransferState.Cancelled)
                return S.Cancelled.ToLowerInvariant();
            return isReceive ? S.ReceivingTitle : S.SendingTitle;
        }

        private string GetActiveTransferDetail(TransferSession session, bool isReceive)
        {
            string peer = session.RemoteDevice != null &&
                !string.IsNullOrWhiteSpace(session.RemoteDevice.Alias)
                ? session.RemoteDevice.Alias
                : S.UnknownDevice;
            string peerLine = isReceive ? S.FromPeer(peer) : S.ToPeer(peer);

            if (session.State == TransferState.Completed)
            {
                return peerLine + " - " +
                    S.TransferSummary(session.TotalFiles, FileSizeConverter.Format(session.TotalBytes));
            }

            int percent = session.TotalBytes > 0
                ? (int)Math.Min(100, session.TransferredBytes * 100 / session.TotalBytes)
                : 0;

            return peerLine + " - " + percent + "% - " +
                S.FileCount(session.TotalFiles);
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
                DeviceAliasSettings.Text = newAlias;
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
        private void ChooseReceiveFolder_Click(object sender, RoutedEventArgs e)
        {
            var picker = new Windows.Storage.Pickers.FolderPicker();
            picker.FileTypeFilter.Add("*");
            picker.PickFolderAndContinue();
        }

        public void ContinueReceiveFolderPicker(Windows.ApplicationModel.Activation.FolderPickerContinuationEventArgs args)
        {
            if (args.Folder == null) return;
            StorageConfig.RememberReceiveFolder(args.Folder);
            RefreshStorageButtons();
        }

        private async void OtherDestButton_Click(object sender, RoutedEventArgs e)
        {
            _sdAvailable = await StorageConfig.IsSdCardAvailableAsync();
            ToggleDestination("other");
        }

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

        private void LangRuButton_Click(object sender, RoutedEventArgs e)
        {
            S.SetLanguage("ru");
            ApplyStrings();
        }

        private void ApplyStrings()
        {
            // Pivot headers — lowercase, WP convention
            Tab1Header.Header = S.ReceiveTab;
            Tab2Header.Header = S.SendTab;
            Tab3Header.Header = S.SettingsTab;

            // Receive tab: tile labels use inner TextBlock names, not Button.Content.
            HistoryTileLabel.Text = S.History;
            AboutTileLabel.Text = S.About;
            AddressTileLabel.Text = S.AddressTile;
            HistoryLinkBtn.Content = S.History + " \u2192";
            ReceivedFilesLabel.Text = S.ReceivedFiles;
            NoReceivedFilesText.Text = S.WaitingForFiles;
            ReceivedStatLabel.Text = S.ReceivedStat;
            TotalStatLabel.Text = S.TotalStat;
            NearbyStatLabel.Text = S.NearbyStat;

            // Send tab
            SelectedFilesText.Text = _selectedFiles.Count > 0
                ? S.FilesSelected(_selectedFiles.Count)
                : S.NoFilesSelected;
            PickerHintText.Text = S.PickerHint;
            TextMessageBox.PlaceholderText = S.TextMessagePlaceholder;
            NearbyDevicesLabel.Text = S.NearbyDevices.ToUpperInvariant();
            NoDevicesText.Text = S.SearchingDevices;
            SendButton.Content = S.SendFiles;
            ApplyDeviceSelectionMode();

            // Settings tab — lowercase section labels
            DeviceSectionLabel.Text = S.Device;
            AliasTextBox.PlaceholderText = S.MyDevice;
            NetworkSectionLabel.Text = S.Network;
            PrefsSectionLabel.Text = S.AppSection;
            QuickSaveToggle.Header = S.QuickSave;
            RequirePinToggle.Header = S.RequirePin;
            ReceivePinLabel.Text = S.PinCode;
            RegeneratePinButton.Content = S.RegeneratePin;
            AutostartToggle.Header = S.Autostart;
            StorageSectionLabel.Text = S.Storage;
            ImagesLabel.Text = S.Images;
            VideosLabel.Text = S.Videos;
            AudioLabel.Text = S.Audio;
            OtherFilesLabel.Text = S.OtherFiles;
            LangSectionLabel.Text = S.Language;
            LangEsButton.Content = S.Spanish;
            LangEnButton.Content = S.English;
            LangRuButton.Content = S.Russian;
            ApplyLanguageSelectionState(S.CurrentLanguageCode);
            ApplyIncomingRequestStrings();
            UpdatePinSettingsVisibility();
            UpdateActiveTransferBanner();

            // Server status
            if (!IsAutostartEnabled())
                ServerStatusText.Text = S.ReceiverStopped;
            else if (_servicesStarted && _server != null)
                ServerStatusText.Text = S.ReadyToReceive;
            else
                ServerStatusText.Text = S.Starting;

            RefreshStorageButtons();
        }

        private void ApplyLanguageSelectionState(string languageCode)
        {
            StyleLanguageButton(LangEsButton, languageCode == "es");
            StyleLanguageButton(LangEnButton, languageCode == "en");
            StyleLanguageButton(LangRuButton, languageCode == "ru");
        }

        private static void StyleLanguageButton(Button button, bool selected)
        {
            button.Background = selected ? BrushAccent : BrushTransparent;
            button.BorderBrush = BrushAccent;
            button.Foreground = selected ? BrushWhite : BrushAccent;
            button.BorderThickness = new Thickness(1);
        }

        #endregion

        private async void AddressTile_Tapped(object sender, TappedRoutedEventArgs e)
        {
            string address = GetReceiveAddress();
            if (string.IsNullOrWhiteSpace(address))
            {
                await ShowMessageAsync(S.ReceiveAddressTitle, S.ReceiveAddressUnavailable);
                return;
            }

            AppLog.V(TAG, "Receive address shown: " + address);
            await ShowMessageAsync(S.ReceiveAddressTitle, S.ReceiveAddressMessage(address));
        }

        private string GetReceiveAddress()
        {
            if (LocalIpText != null && !string.IsNullOrWhiteSpace(LocalIpText.Text))
                return LocalIpText.Text;

            string address = FormatReceiveAddress(DiscoveryService.GetLocalIpAddress());
            if (LocalIpText != null)
                LocalIpText.Text = address;
            return address;
        }

        private void HistoryTile_Tapped(object sender, TappedRoutedEventArgs e)
        {
            AppLog.V(TAG, "Navigating to HistoryPage");
            Frame.Navigate(typeof(HistoryPage));
        }

        private void AboutTile_Tapped(object sender, TappedRoutedEventArgs e)
        {
            AppLog.V(TAG, "Navigating to AboutPage");
            Frame.Navigate(typeof(AboutPage));
        }

        private void DevicesList_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var panel = DevicesList.ItemsPanelRoot as ItemsWrapGrid;
            if (panel != null && e.NewSize.Width > 0)
            {
                double itemW = Math.Floor((e.NewSize.Width - 8) / 2.0);
                panel.ItemWidth = itemW > 80 ? itemW : 80;
            }
        }

        private void QuickSaveToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_settingsLoading) return;
            AppPreferences.QuickSave = QuickSaveToggle.IsOn;
            AppLog.V(TAG, "QuickSave=" + QuickSaveToggle.IsOn);
        }

        private void RequirePinToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_settingsLoading) return;

            AppPreferences.RequirePin = RequirePinToggle.IsOn;
            if (RequirePinToggle.IsOn)
                AppPreferences.EnsureReceivePin();

            UpdatePinSettingsVisibility();
            AppLog.V(TAG, "RequirePin=" + RequirePinToggle.IsOn);
        }

        private async void AutostartToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_settingsLoading) return;

            AppPreferences.Autostart = AutostartToggle.IsOn;
            AppLog.V(TAG, "Autostart=" + AutostartToggle.IsOn);

            if (AutostartToggle.IsOn)
            {
                await EnsureReceiverServicesAsync();
            }
            else
            {
                SuspendServices();
                SetReceiverStoppedByPreference();
            }
        }

        private void RegeneratePinButton_Click(object sender, RoutedEventArgs e)
        {
            AppPreferences.RegenerateReceivePin();
            UpdatePinSettingsVisibility();
            AppLog.I(TAG, "Receive PIN regenerated");
        }

        private void LoadPreferenceSettings()
        {
            _settingsLoading = true;
            try
            {
                QuickSaveToggle.IsOn = AppPreferences.QuickSave;
                RequirePinToggle.IsOn = AppPreferences.RequirePin;
                AutostartToggle.IsOn = AppPreferences.Autostart;

                if (RequirePinToggle.IsOn)
                    AppPreferences.EnsureReceivePin();

                UpdatePinSettingsVisibility();
            }
            finally
            {
                _settingsLoading = false;
            }
        }

        private async Task EnsureReceiverServicesAsync()
        {
            ServerStatusText.Text = S.Starting;
            StatusDot.Fill = BrushGray;

            if (!_servicesStarted)
                await InitializeServicesAsync();
            else
                await ResumeServicesAsync();

            StartRefreshTimer();
        }

        private void SetReceiverStoppedByPreference()
        {
            StopRefreshTimer();
            ServerStatusText.Text = S.ReceiverStopped;
            StatusDot.Fill = BrushGray;
            LocalIpText.Text = "";
            NearbyCountText.Text = "0";
            DeviceCountText.Text = "";
            NoDevicesText.Text = S.ReceiverStopped;
            NoDevicesText.Visibility = Visibility.Visible;
            DevicesList.ItemsSource = null;
        }

        private void UpdatePinSettingsVisibility()
        {
            if (ReceivePinPanel == null || ReceivePinText == null)
                return;

            bool enabled = RequirePinToggle != null && RequirePinToggle.IsOn;
            ReceivePinPanel.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
            ReceivePinText.Text = enabled ? AppPreferences.EnsureReceivePin() : "";
        }

        private static bool IsQuickSaveEnabled()
        {
            return AppPreferences.QuickSave;
        }

        private static bool IsAutostartEnabled()
        {
            return AppPreferences.Autostart;
        }

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

    internal sealed class IncomingRequestFileItem
    {
        public string FileIcon { get; set; }
        public string FileName { get; set; }
        public string SizeText { get; set; }
    }
}
