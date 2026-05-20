// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0
// SPDX-FileCopyrightText: 2026 Angel Careaga <hello@angelcareaga.com>

using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Vianium.LocalSend.Strings;

namespace Vianium.LocalSend.Pages
{
    public sealed partial class ChangelogPage : Page
    {
        public ChangelogPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            ApplyStrings();
        }

        private void ApplyStrings()
        {
            PageTitle.Text = S.Changelog;

            // v1.0.1
            V101Header.Text = "VERSION 1.0.1";
            V101Fix1Title.Text = S.V101StorageTitle;
            V101Fix1Desc.Text = S.V101StorageDesc;
            V101Fix2Title.Text = S.V101MigrateTitle;
            V101Fix2Desc.Text = S.V101MigrateDesc;
            V101Fix3Title.Text = S.V101SdTitle;
            V101Fix3Desc.Text = S.V101SdDesc;
            V101Fix4Title.Text = S.V101CrashTitle;
            V101Fix4Desc.Text = S.V101CrashDesc;
            V101Fix5Title.Text = S.V101PortTitle;
            V101Fix5Desc.Text = S.V101PortDesc;

            // v1.0.0
            V100Header.Text = "VERSION 1.0.0";
            V100Desc.Text = S.V100Desc;
        }
    }
}
