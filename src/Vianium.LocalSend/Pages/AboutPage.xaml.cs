// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0
// SPDX-FileCopyrightText: 2026 Angel Careaga <hello@angelcareaga.com>

using System;
using System.Threading.Tasks;
using Windows.System;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;
using Vianium.LocalSend.Logging;
using Vianium.LocalSend.Strings;

namespace Vianium.LocalSend.Pages
{
    public sealed partial class AboutPage : Page
    {
        private const string TAG = "AboutPage";
        private const string RepositoryUrl = "https://github.com/vianium/vianium-localsend";
        private const string AuthorUrl = "https://angelcareaga.com";
        private const string LocalSendUrl = "https://localsend.org/";
        private const string SupportUrl = "https://discord.gg/NccbAK6jeb";

        public AboutPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            ApplyStrings();
        }

        private void ChangelogLink_Tapped(object sender, TappedRoutedEventArgs e)
        {
            Frame.Navigate(typeof(ChangelogPage));
        }

        private void DiagnosticsLink_Tapped(object sender, TappedRoutedEventArgs e)
        {
            Frame.Navigate(typeof(DiagnosticsPage));
        }

        private async void RepositoryLink_Tapped(object sender, TappedRoutedEventArgs e)
        {
            await OpenUrlAsync(RepositoryUrl);
        }

        private async void AuthorLink_Tapped(object sender, TappedRoutedEventArgs e)
        {
            await OpenUrlAsync(AuthorUrl);
        }

        private async void LocalSendLink_Tapped(object sender, TappedRoutedEventArgs e)
        {
            await OpenUrlAsync(LocalSendUrl);
        }

        private async void SupportLink_Tapped(object sender, TappedRoutedEventArgs e)
        {
            await OpenUrlAsync(SupportUrl);
        }

        private async Task OpenUrlAsync(string url)
        {
            try
            {
                await Launcher.LaunchUriAsync(new Uri(url));
            }
            catch (Exception ex)
            {
                AppLog.W(TAG, "Could not open URL " + url + ": " + ex.Message);
            }
        }

        private void ApplyStrings()
        {
            BrandText.Text = "LOCALSEND";
            TaglineText.Text = S.AboutTagline;
            VersionTitleText.Text = S.Version.ToUpperInvariant();
            VersionValueText.Text = S.AboutVersionValue;
            ProtocolTitleText.Text = S.ProtocolShort.ToUpperInvariant();
            ProtocolValueText.Text = "LocalSend v2.x";
            DescriptionText.Text = S.AboutDescription;

            RepositoryLabelText.Text = S.Repository.ToUpperInvariant();
            RepositoryLinkText.Text = "github.com/vianium/vianium-localsend";
            LicenseSummaryText.Text = S.AboutLicenseSummary;
            PrivacySummaryText.Text = S.AboutPrivacySummary;

            ProjectSectionText.Text = S.Project.ToUpperInvariant();
            WhyText.Text = S.AboutWhy;
            InteropText.Text = S.AboutInteroperability;

            FeaturesSectionText.Text = S.Features.ToUpperInvariant();
            SetBullet(Feature1Text, S.FeatureDiscovery);
            SetBullet(Feature2Text, S.FeatureStreaming);
            SetBullet(Feature3Text, S.FeatureSecureTransport);
            SetBullet(Feature4Text, S.FeatureAnyFiles);
            SetBullet(Feature5Text, S.FeatureTextMessages);
            SetBullet(Feature6Text, S.FeatureHistory);
            SetBullet(Feature7Text, S.FeatureLanguages);
            SetBullet(Feature8Text, S.FeatureStorage);

            TechnologySectionText.Text = S.Technology.ToUpperInvariant();
            TechIntroText.Text = S.AboutTechnologyIntro;
            SetBullet(Tech1, S.TechHttpServer);
            SetBullet(Tech2, S.TechHttpsClient);
            SetBullet(Tech3, S.TechDiscovery);
            SetBullet(Tech4, S.TechStreaming);
            SetBullet(Tech5, S.TechJson);

            CompatibilitySection.Text = S.Compatibility;
            CompatibilityDesktopText.Text = S.CompatibilityDesktop;
            CompatibilityMobileText.Text = S.CompatibilityMobile;
            CompatibilityClientsText.Text = S.CompatibilityClients;
            CompatibilityPhoneText.Text = "Windows Phone 8.1";
            CompatibilityNote.Text = S.CompatibilityNote;
            HistorySectionText.Text = S.History.ToUpperInvariant();
            HistoryText.Text = S.AboutHistory;

            CreditsSection.Text = S.Credits;
            AuthorText.Text = S.AuthorCredit;

            ChangelogLink.Text = S.Changelog + " >";
            DiagnosticsLink.Text = S.Diagnostics + " >";
        }

        private static void SetBullet(TextBlock textBlock, string text)
        {
            textBlock.Text = "\u2022  " + text;
        }
    }
}
