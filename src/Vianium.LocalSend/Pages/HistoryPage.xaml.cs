// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0
// SPDX-FileCopyrightText: 2026 Angel Careaga <hello@angelcareaga.com>

using System;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Vianium.LocalSend.Data;
using Vianium.LocalSend.Logging;
using Vianium.LocalSend.Strings;

namespace Vianium.LocalSend.Pages
{
    public sealed partial class HistoryPage : Page
    {
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
            HistoryTitleText.Text = S.HistoryTitle;
            EmptyText.Text = S.NoHistory;
            ClearButton.Content = S.ClearHistory;
            BackButton.Content = S.Back;
        }

        private async System.Threading.Tasks.Task RefreshListAsync()
        {
            var entries = await TransferHistory.LoadAsync();
            HistoryList.ItemsSource = entries;
            HistoryCountText.Text = S.Transfers(entries.Count);
            EmptyState.Visibility = entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            ClearButton.IsEnabled = entries.Count > 0;
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

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack) Frame.GoBack();
        }
    }
}
