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

            // v1.1.0
            V110Header.Text = "VERSION 1.1.0";
            SetChange(V110Fix1Title, V110Fix1Desc, S.V110BrandingTitle, S.V110BrandingDesc);
            SetChange(V110Fix2Title, V110Fix2Desc, S.V110DesignTitle, S.V110DesignDesc);
            SetChange(V110Fix3Title, V110Fix3Desc, S.V110TransferTitle, S.V110TransferDesc);
            SetChange(V110Fix4Title, V110Fix4Desc, S.V110ReliabilityTitle, S.V110ReliabilityDesc);
            SetChange(V110Fix5Title, V110Fix5Desc, S.V110PreferencesTitle, S.V110PreferencesDesc);
            SetChange(V110Fix6Title, V110Fix6Desc, S.V110LocalizationTitle, S.V110LocalizationDesc);

            // v1.0.1
            V101Header.Text = "VERSION 1.0.1";
            SetChange(V101Fix1Title, V101Fix1Desc, S.V101StorageTitle, S.V101StorageDesc);
            SetChange(V101Fix2Title, V101Fix2Desc, S.V101MigrateTitle, S.V101MigrateDesc);
            SetChange(V101Fix3Title, V101Fix3Desc, S.V101SdTitle, S.V101SdDesc);
            SetChange(V101Fix4Title, V101Fix4Desc, S.V101CrashTitle, S.V101CrashDesc);
            SetChange(V101Fix5Title, V101Fix5Desc, S.V101PortTitle, S.V101PortDesc);

            // v1.0.0
            V100Header.Text = "VERSION 1.0.0";
            V100Desc.Text = S.V100Desc;
        }

        private static void SetChange(TextBlock title, TextBlock description,
            string titleText, string descriptionText)
        {
            title.Text = titleText;
            description.Text = descriptionText;
        }
    }
}
