// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0
// SPDX-FileCopyrightText: 2026 Angel Careaga <hello@angelcareaga.com>

using System;
using System.Collections.Generic;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Vianium.LocalSend.Converters;
using Vianium.LocalSend.Data;
using Vianium.LocalSend.Logging;
using Vianium.LocalSend.Strings;

namespace Vianium.LocalSend.Pages
{
    public sealed partial class HistoryPage : Page
    {
        private const double TransferMergeWindowSeconds = 45;

        private static readonly SolidColorBrush AccentBrush =
            new SolidColorBrush(Windows.UI.Color.FromArgb(255, 27, 161, 226));

        private static readonly SolidColorBrush SubtleBrush =
            new SolidColorBrush(Windows.UI.Color.FromArgb(255, 139, 148, 158));

        public HistoryPage()
        {
            this.InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            AppLog.V("HistoryPage", "Opened");
            ApplyStrings();
            await RefreshListAsync();
        }

        private void ApplyStrings()
        {
            HistoryTitleText.Text = S.HistoryTitle.ToLowerInvariant();
            EmptyText.Text = S.NoHistory;
            ClearButtonText.Text = S.ClearHistory.ToUpperInvariant();
        }

        private async System.Threading.Tasks.Task RefreshListAsync()
        {
            var entries = await TransferHistory.LoadAsync();
            var transfers = BuildTransfers(entries);
            HistoryList.ItemsSource = BuildRows(transfers);
            HistoryCountText.Text = FormatTransferCount(transfers.Count);

            bool hasEntries = transfers.Count > 0;
            HistoryList.Visibility = hasEntries ? Visibility.Visible : Visibility.Collapsed;
            EmptyState.Visibility = hasEntries ? Visibility.Collapsed : Visibility.Visible;
            ClearButton.IsEnabled = hasEntries;
        }

        private async void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new MessageDialog(S.ConfirmClear, S.Confirm);
            dialog.Commands.Add(new UICommand(S.Clear, async cmd =>
            {
                await TransferHistory.ClearAsync();
                await RefreshListAsync();
            }));
            dialog.Commands.Add(new UICommand(S.Cancel));
            dialog.DefaultCommandIndex = 1;
            dialog.CancelCommandIndex = 1;
            await dialog.ShowAsync();
        }

        private List<HistoryTransfer> BuildTransfers(List<HistoryEntry> entries)
        {
            var sorted = new List<HistoryEntry>(entries ?? new List<HistoryEntry>());
            sorted.Sort((a, b) =>
            {
                DateTime at = a != null ? a.Timestamp : DateTime.MinValue;
                DateTime bt = b != null ? b.Timestamp : DateTime.MinValue;
                return bt.CompareTo(at);
            });

            var transfers = new List<HistoryTransfer>();
            HistoryTransfer current = null;

            foreach (var entry in sorted)
            {
                if (entry == null) continue;

                DateTime localTime = entry.Timestamp == DateTime.MinValue
                    ? DateTime.Now
                    : entry.Timestamp.ToLocalTime();
                string peerName = GetPeerName(entry);

                if (current != null && current.CanMerge(entry, peerName, localTime))
                {
                    current.Add(entry, localTime);
                }
                else
                {
                    current = HistoryTransfer.Create(entry, peerName, localTime);
                    transfers.Add(current);
                }
            }

            return transfers;
        }

        private List<HistoryRow> BuildRows(List<HistoryTransfer> transfers)
        {
            var rows = new List<HistoryRow>();
            DateTime? currentDate = null;

            foreach (var transfer in transfers)
            {
                DateTime transferDate = transfer.LatestLocalTime.Date;
                if (!currentDate.HasValue || currentDate.Value != transferDate)
                {
                    rows.Add(HistoryRow.CreateHeader(FormatDateHeader(transferDate)));
                    currentDate = transferDate;
                }

                rows.Add(HistoryRow.CreateTransfer(transfer));
            }

            return rows;
        }

        private string GetPeerName(HistoryEntry entry)
        {
            if (!string.IsNullOrWhiteSpace(entry.SenderAlias) &&
                !string.Equals(entry.SenderAlias, "Unknown", StringComparison.OrdinalIgnoreCase))
            {
                return entry.SenderAlias;
            }

            if (!string.IsNullOrWhiteSpace(entry.FileName))
                return entry.FileName;

            return S.Unknown;
        }

        private string FormatDateHeader(DateTime date)
        {
            DateTime today = DateTime.Now.Date;
            if (date == today)
                return S.Today;
            if (date == today.AddDays(-1))
                return S.Yesterday;

            string month = S.MonthShort(date.Month);
            return S.IsEnglish ? month + " " + date.Day : date.Day + " " + month;
        }

        private string FormatTransferCount(int count)
        {
            return S.Transfers(count);
        }

        private static string FormatFileCount(int count)
        {
            return S.FileCount(count);
        }

        private sealed class HistoryTransfer
        {
            public bool IsSent { get; private set; }
            public string PeerName { get; private set; }
            public DateTime LatestLocalTime { get; private set; }
            public int FileCount { get; private set; }
            public long TotalBytes { get; private set; }
            public string Icon { get; private set; }

            public static HistoryTransfer Create(HistoryEntry entry, string peerName, DateTime localTime)
            {
                var transfer = new HistoryTransfer
                {
                    IsSent = entry.IsSent,
                    PeerName = peerName,
                    LatestLocalTime = localTime,
                    Icon = entry.FileIcon
                };
                transfer.Add(entry, localTime);
                return transfer;
            }

            public bool CanMerge(HistoryEntry entry, string peerName, DateTime localTime)
            {
                if (entry.IsSent != IsSent) return false;
                if (!string.Equals(PeerName, peerName, StringComparison.OrdinalIgnoreCase)) return false;
                if (LatestLocalTime.Date != localTime.Date) return false;

                double seconds = Math.Abs((LatestLocalTime - localTime).TotalSeconds);
                return seconds <= TransferMergeWindowSeconds;
            }

            public void Add(HistoryEntry entry, DateTime localTime)
            {
                FileCount++;
                TotalBytes += entry.FileSize;

                if (localTime > LatestLocalTime)
                    LatestLocalTime = localTime;

                if (string.IsNullOrEmpty(Icon))
                    Icon = entry.FileIcon;
            }
        }

        private sealed class HistoryRow
        {
            public string HeaderText { get; private set; }
            public Visibility HeaderVisibility { get; private set; }
            public Visibility TransferVisibility { get; private set; }
            public string Icon { get; private set; }
            public string Title { get; private set; }
            public string DirectionText { get; private set; }
            public string DetailText { get; private set; }
            public string TimeText { get; private set; }
            public SolidColorBrush DirectionBrush { get; private set; }

            public static HistoryRow CreateHeader(string text)
            {
                return new HistoryRow
                {
                    HeaderText = text,
                    HeaderVisibility = Visibility.Visible,
                    TransferVisibility = Visibility.Collapsed
                };
            }

            public static HistoryRow CreateTransfer(HistoryTransfer transfer)
            {
                bool isSent = transfer.IsSent;
                return new HistoryRow
                {
                    HeaderVisibility = Visibility.Collapsed,
                    TransferVisibility = Visibility.Visible,
                    Icon = transfer.Icon,
                    Title = transfer.PeerName,
                    DirectionText = (isSent ? "\u2191 " : "\u2193 ") +
                        (isSent ? S.DirectionSent : S.DirectionReceived),
                    DetailText = "\u00B7 " + FormatFileCount(transfer.FileCount) +
                        " \u00B7 " + FileSizeConverter.Format(transfer.TotalBytes),
                    TimeText = transfer.LatestLocalTime.ToString("HH:mm"),
                    DirectionBrush = isSent ? AccentBrush : SubtleBrush
                };
            }
        }
    }
}
