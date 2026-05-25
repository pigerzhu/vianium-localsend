// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0
// SPDX-FileCopyrightText: 2026 Angel Careaga <hello@angelcareaga.com>

using System;
using System.Threading;
using Windows.System.Display;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Vianium.LocalSend.Converters;
using Vianium.LocalSend.Logging;
using Vianium.LocalSend.Protocol;
using Vianium.LocalSend.Strings;

namespace Vianium.LocalSend.Pages
{
    public sealed partial class TransferPage : Page
    {
        private const string TAG = "TransferPage";
        private const double FallbackProgressWidth = 390;

        private TransferSession _session;
        private CancellationTokenSource _cts;
        private Action _cancelAction;
        private Action _dismissAction;
        private bool _isReceive;
        private bool _isTerminal;
        private DateTime _startedAtUtc;
        private DisplayRequest _displayRequest;

        public TransferPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            RequestDisplayActive();

            var args = e.Parameter as TransferPageArgs;
            if (args == null || args.Session == null)
            {
                AppLog.W(TAG, "Opened with no TransferPageArgs");
                return;
            }

            _session = args.Session;
            _cts = args.CancellationSource;
            _cancelAction = args.CancelAction;
            _dismissAction = args.DismissAction;
            _isReceive = args.IsReceive;
            _startedAtUtc = DateTime.UtcNow;

            AppLog.I(TAG, "Opened - " + (_isReceive ? "receive" : "send") +
                " " + _session.TotalFiles + " file(s)");

            ApplyStaticStrings();
            UpdateProgress();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            if (_isTerminal && e.NavigationMode == NavigationMode.Back)
                DismissTransfer();
            ReleaseDisplayRequest();
        }

        private void RequestDisplayActive()
        {
            try
            {
                if (_displayRequest == null)
                    _displayRequest = new DisplayRequest();
                _displayRequest.RequestActive();
            }
            catch (Exception ex)
            {
                AppLog.W(TAG, "DisplayRequest.RequestActive failed: " + ex.Message);
            }
        }

        private void ReleaseDisplayRequest()
        {
            try
            {
                if (_displayRequest != null)
                {
                    _displayRequest.RequestRelease();
                    _displayRequest = null;
                }
            }
            catch (Exception ex)
            {
                AppLog.W(TAG, "DisplayRequest.RequestRelease failed: " + ex.Message);
            }
        }

        private void ApplyStaticStrings()
        {
            TransferTitleText.Text = _isReceive ? S.ReceivingTitle : S.SendingTitle;
            TransferPeerText.Text = GetPeerLine();
            RemainingLabelText.Text = S.Remaining.ToUpperInvariant();
            SpeedLabelText.Text = S.Speed.ToUpperInvariant();
            MinimizeButton.Content = S.Minimize.ToUpperInvariant();
            CancelButton.Content = S.Cancel.ToUpperInvariant();
            DoneButton.Content = S.Done.ToUpperInvariant();
        }

        public void UpdateProgress()
        {
            if (_session == null) return;

            long total = _session.TotalBytes;
            long transferred = _session.TransferredBytes;
            double percent = total > 0 ? transferred * 100.0 / total : 0;
            if (percent > 100) percent = 100;

            if (_session.State == TransferState.Completed)
            {
                ShowCompleteState();
                return;
            }

            if (_session.State == TransferState.Cancelled ||
                _session.State == TransferState.Failed)
            {
                ShowTerminalProgressState();
                return;
            }

            ProgressLayout.Visibility = Visibility.Visible;
            CompleteLayout.Visibility = Visibility.Collapsed;
            TransferTitleText.Text = _isReceive ? S.ReceivingTitle : S.SendingTitle;
            TransferPeerText.Text = GetPeerLine();
            ProgressPercentText.Text = ((int)percent) + "%";
            SetProgressWidth(percent);
            UpdateCurrentFile();
            UpdateTransferStats(total, transferred);
        }

        private void ShowCompleteState()
        {
            _isTerminal = true;
            ProgressLayout.Visibility = Visibility.Collapsed;
            CompleteLayout.Visibility = Visibility.Visible;
            CompleteTitleText.Text = S.TransferCompleteTitle;
            CompletePeerText.Text = GetPeerLine();
            CompleteFilesText.Text = S.FileCount(_session.TotalFiles);
            CompleteSizeText.Text = FileSizeConverter.Format(_session.TotalBytes);
        }

        private void ShowTerminalProgressState()
        {
            _isTerminal = true;
            ProgressLayout.Visibility = Visibility.Visible;
            CompleteLayout.Visibility = Visibility.Collapsed;
            TransferTitleText.Text = _session.State == TransferState.Cancelled
                ? S.Cancelled.ToLowerInvariant()
                : S.TransferError.ToLowerInvariant();
            ProgressPercentText.Text = _session.State == TransferState.Cancelled ? "" : "!";
            MinimizeButton.Visibility = Visibility.Collapsed;
            CancelButton.Content = S.Back.ToUpperInvariant();
            SetProgressWidth(0);
        }

        private string GetPeerLine()
        {
            string remote = _session != null && _session.RemoteDevice != null &&
                !string.IsNullOrWhiteSpace(_session.RemoteDevice.Alias)
                ? _session.RemoteDevice.Alias
                : S.UnknownDevice;

            return _isReceive ? S.FromPeer(remote) : S.ToPeer(remote);
        }

        private void SetProgressWidth(double percent)
        {
            double width = ProgressTrack.ActualWidth > 0
                ? ProgressTrack.ActualWidth
                : FallbackProgressWidth;
            ProgressFill.Width = width * percent / 100.0;
        }

        private void UpdateCurrentFile()
        {
            var current = GetCurrentFile();
            if (current == null)
            {
                CurrentFilePanel.Visibility = Visibility.Collapsed;
                return;
            }

            CurrentFilePanel.Visibility = Visibility.Visible;
            CurrentFileIndexText.Text = S.CurrentFileIndex(current.Index, _session.TotalFiles);
            CurrentFileNameText.Text = current.Info.File.FileName;
            CurrentFileSizeText.Text = FileSizeConverter.Format(current.Info.File.Size);
        }

        private CurrentFileSnapshot GetCurrentFile()
        {
            CurrentFileSnapshot firstQueued = null;
            CurrentFileSnapshot lastCompleted = null;
            int index = 0;

            foreach (var item in _session.Files.Values)
            {
                index++;
                if (item.Status == FileTransferStatus.Transferring)
                    return new CurrentFileSnapshot { Index = index, Info = item };

                if (item.Status == FileTransferStatus.Queued && firstQueued == null)
                    firstQueued = new CurrentFileSnapshot { Index = index, Info = item };

                if (item.Status == FileTransferStatus.Completed)
                    lastCompleted = new CurrentFileSnapshot { Index = index, Info = item };
            }

            return firstQueued ?? lastCompleted;
        }

        private void UpdateTransferStats(long total, long transferred)
        {
            double elapsed = Math.Max(0.5, (DateTime.UtcNow - _startedAtUtc).TotalSeconds);
            double bytesPerSecond = transferred / elapsed;

            SpeedValueText.Text = transferred > 0
                ? FileSizeConverter.Format((long)bytesPerSecond) + "/s"
                : "--";

            if (transferred > 0 && bytesPerSecond > 1 && total > transferred)
            {
                int remainingSeconds = (int)Math.Ceiling((total - transferred) / bytesPerSecond);
                RemainingValueText.Text = S.TimeRemaining(remainingSeconds);
            }
            else
            {
                RemainingValueText.Text = "--";
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            AppLog.I(TAG, "Transfer minimized");
            GoBackOrHome();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isTerminal)
            {
                DismissTransfer();
                GoBackOrHome();
                return;
            }

            AppLog.I(TAG, "User cancelled transfer");
            try
            {
                if (_cancelAction != null)
                    _cancelAction();
                else if (_cts != null)
                    _cts.Cancel();
            }
            catch (Exception ex)
            {
                AppLog.W(TAG, "Cancel action failed: " + ex.Message);
            }

            if (_session != null)
                _session.State = TransferState.Cancelled;
            UpdateProgress();
        }

        private void DoneButton_Click(object sender, RoutedEventArgs e)
        {
            DismissTransfer();
            GoBackOrHome();
        }

        private void DismissTransfer()
        {
            try
            {
                if (_dismissAction != null)
                    _dismissAction();
            }
            catch (Exception ex)
            {
                AppLog.W(TAG, "Dismiss action failed: " + ex.Message);
            }
        }

        private void GoBackOrHome()
        {
            if (Frame.CanGoBack)
                Frame.GoBack();
            else
                Frame.Navigate(typeof(MainPage));
        }
    }

    internal sealed class TransferPageArgs
    {
        public TransferSession Session { get; set; }
        public CancellationTokenSource CancellationSource { get; set; }
        public bool IsReceive { get; set; }
        public Action CancelAction { get; set; }
        public Action DismissAction { get; set; }
    }

    internal sealed class CurrentFileSnapshot
    {
        public int Index { get; set; }
        public FileTransferInfo Info { get; set; }
    }
}
