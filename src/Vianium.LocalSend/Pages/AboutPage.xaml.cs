// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0
// SPDX-FileCopyrightText: 2026 Angel Careaga <hello@angelcareaga.com>

using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;
using Vianium.LocalSend.Strings;

namespace Vianium.LocalSend.Pages
{
    public sealed partial class AboutPage : Page
    {
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

        private void ApplyStrings()
        {
            // Version
            VersionLabel.Text = S.Version + ": 1.0.1";
            ProtocolLabel.Text = S.Protocol + "LocalSend v2.1";
            PlatformLabel.Text = S.Platform + ": Windows Phone 8.1";
            ChangelogLink.Text = S.Changelog + " >";
            DiagnosticsLink.Text = S.Diagnostics + " >";

            // Description
            DescriptionText.Text = S.AboutDescription;

            // Compatibility
            CompatibilitySection.Text = S.Compatibility;
            SendReceiveLabel.Text = S.SendReceiveWith;
            CompatibilityNote.Text = S.CompatibilityNote;

            // About this port
            PortSection.Text = S.AboutThisPort;
            FirstAndOnlyText.Text = S.FirstAndOnly;
            IncludesText.Text = S.IncludesDescription;
            BuiltFromScratchText.Text = S.BuiltFromScratch;

            // Developer
            DeveloperSection.Text = S.Developer;
            RoleText.Text = S.SoftwareEngineer;
            BuiltWithLabel.Text = S.BuiltWith;
            Tech1.Text = "\u2022  " + S.TechHttpServer;
            Tech2.Text = "\u2022  " + S.TechHttpsClient;
            Tech3.Text = "\u2022  " + S.TechDiscovery;
            Tech4.Text = "\u2022  " + S.TechStreaming;
            Tech5.Text = "\u2022  " + S.TechJson;

            // Credits
            CreditsSection.Text = S.Credits;
            OriginalLabel.Text = S.OriginalProtocol;

            // License
            LicenseSection.Text = S.License;
            LicenseText.Text = S.LicenseText;
        }
    }
}
