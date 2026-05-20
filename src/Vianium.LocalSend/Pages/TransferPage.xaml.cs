// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0
// SPDX-FileCopyrightText: 2026 Angel Careaga <hello@angelcareaga.com>

using System;
using System.Collections.Generic;
using System.Threading;
using Windows.System.Display;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
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

        private TransferSession _session;
        private CancellationTokenSource _cts;
        private bool _isComplete;
        private DisplayRequest _displayRequest;

        public TransferPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // Keep the screen awake for the whole transfer. If the device
            // locks, WP 8.1 suspends — and can then terminate — the app
            // mid-transfer, which users perceive and report as a crash.
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

            var args = e.Parameter as TransferPageArgs;
            if (args != null)
            {
                _session = args.Session;
                _cts = args.CancellationSource;
                AppLog.I(TAG, "Opened — " + (_session != null ? _session.TotalFiles : 0) +
                    " file(s) to " + (_session != null && _session.RemoteDevice != null
                        ? _session.RemoteDevice.Alias : "?"));
                UpdateUI();
            }
            else
            {
                AppLog.W(TAG, "Opened with no TransferPageArgs");
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            // Release the screen-on lock taken in OnNavigatedTo.
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

        private void UpdateUI()
        {
            if (_session == null) return;

            DeviceInfoText.Text = _session.RemoteDevice != null
                ? _session.RemoteDevice.Alias : "Unknown";
            TransferTitleText.Text = S.Sending;
            ActionButton.Content = S.Cancel;
            RefreshFileList();
            UpdateProgress();
        }

        private void RefreshFileList()
        {
            var items = new List<FileDisplayItem>();
            foreach (var kvp in _session.Files)
            {
                var f = kvp.Value;
                items.Add(new FileDisplayItem
                {
                    FileName = f.File.FileName,
                    SizeText = FileSizeConverter.Format(f.File.Size),
                    StatusIcon = GetStatusIcon(f.Status),
                    StatusColor = GetStatusBrush(f.Status)
                });
            }
            FilesList.ItemsSource = items;
        }

        public void UpdateProgress()
        {
            if (_session == null) return;

            long total = _session.TotalBytes;
            long transferred = _session.TransferredBytes;

            double percent = total > 0 ? (transferred * 100.0 / total) : 0;
            OverallProgressBar.Value = percent;
            ProgressText.Text = (int)percent + "%";
            FilesCountText.Text = S.Files(_session.CompletedFiles, _session.TotalFiles);
            DataSizeText.Text = FileSizeConverter.Format(transferred) + " / " +
                FileSizeConverter.Format(total);

            if (_session.State == TransferState.Completed ||
                _session.State == TransferState.Failed ||
                _session.State == TransferState.Cancelled)
            {
                _isComplete = true;
                ActionButton.Content = S.Back;

                if (_session.State == TransferState.Completed)
                {
                    TransferTitleText.Text = S.Completed;
                    OverallProgressBar.Value = 100;
                }
                else if (_session.State == TransferState.Cancelled)
                {
                    TransferTitleText.Text = S.Cancelled;
                }
                else
                {
                    TransferTitleText.Text = S.TransferError;
                }

                RefreshFileList();
            }
        }

        private void ActionButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isComplete)
            {
                if (Frame.CanGoBack) Frame.GoBack();
            }
            else
            {
                AppLog.I(TAG, "User cancelled transfer");
                if (_cts != null) _cts.Cancel();
                if (_session != null) _session.State = TransferState.Cancelled;

                _isComplete = true;
                ActionButton.Content = S.Back;
                TransferTitleText.Text = S.Cancelled;
            }
        }

        private static string GetStatusIcon(FileTransferStatus status)
        {
            switch (status)
            {
                case FileTransferStatus.Queued: return "\uE121";
                case FileTransferStatus.Transferring: return "\uE118";
                case FileTransferStatus.Completed: return "\uE10B";
                case FileTransferStatus.Failed: return "\uE10A";
                case FileTransferStatus.Skipped: return "\uE108";
                default: return "";
            }
        }

        private static SolidColorBrush GetStatusBrush(FileTransferStatus status)
        {
            switch (status)
            {
                case FileTransferStatus.Completed:
                    return new SolidColorBrush(Windows.UI.Color.FromArgb(255, 63, 185, 80));
                case FileTransferStatus.Failed:
                    return new SolidColorBrush(Windows.UI.Color.FromArgb(255, 248, 81, 73));
                case FileTransferStatus.Transferring:
                    return new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 120, 215));
                default:
                    return new SolidColorBrush(Windows.UI.Color.FromArgb(255, 139, 148, 158));
            }
        }
    }

    internal sealed class TransferPageArgs
    {
        public TransferSession Session { get; set; }
        public CancellationTokenSource CancellationSource { get; set; }
    }

    internal sealed class FileDisplayItem
    {
        public string FileName { get; set; }
        public string SizeText { get; set; }
        public string StatusIcon { get; set; }
        public SolidColorBrush StatusColor { get; set; }
    }
}
