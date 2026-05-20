// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0
// SPDX-FileCopyrightText: 2026 Angel Careaga <hello@angelcareaga.com>

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Vianium.LocalSend.SmokeTests
{
    public sealed partial class MainPage : Page
    {
        private static readonly SolidColorBrush PassBrush =
            new SolidColorBrush(Color.FromArgb(0xFF, 0x10, 0xB9, 0x81));
        private static readonly SolidColorBrush FailBrush =
            new SolidColorBrush(Color.FromArgb(0xFF, 0xEF, 0x44, 0x44));

        private readonly ObservableCollection<SmokeRow> _rows;

        public MainPage()
        {
            this.InitializeComponent();
            _rows = new ObservableCollection<SmokeRow>();
            ResultsList.ItemsSource = _rows;
        }

        private async void OnRunClicked(object sender, RoutedEventArgs e)
        {
            RunButton.IsEnabled = false;
            BusyRing.IsActive = true;
            _rows.Clear();
            SummaryHeadline.Text = "Running...";
            SummaryDetail.Text = string.Empty;

            try
            {
                IList<SmokeResult> results = await SmokeTestRunner.RunAllAsync(OnResultReady);

                int passed = 0, failed = 0;
                long totalMs = 0;
                foreach (SmokeResult r in results)
                {
                    if (r.Passed) passed++;
                    else failed++;
                    totalMs += r.ElapsedMs;
                }

                SummaryHeadline.Text = failed == 0
                    ? string.Format("All {0} tests passed", passed)
                    : string.Format("{0} passed, {1} failed", passed, failed);
                SummaryDetail.Text = string.Format(
                    "{0} tests run in {1} ms ({2:0.0} s)",
                    results.Count, totalMs, totalMs / 1000.0);
            }
            catch (Exception ex)
            {
                SummaryHeadline.Text = "Runner crashed";
                SummaryDetail.Text = ex.Message;
            }
            finally
            {
                BusyRing.IsActive = false;
                RunButton.IsEnabled = true;
            }
        }

        private void OnResultReady(SmokeResult result)
        {
            // Marshaling not required: SmokeTestRunner.RunAllAsync awaits each
            // test, so the continuation runs on the UI thread that invoked
            // OnRunClicked. Tests must therefore avoid spawning their own
            // background threads that race the dispatcher.
            _rows.Add(new SmokeRow(result));
        }

        private sealed class SmokeRow : INotifyPropertyChanged
        {
            public SmokeRow(SmokeResult result)
            {
                Name = result.Name;
                Message = string.IsNullOrEmpty(result.Message) ? string.Empty : result.Message;
                Passed = result.Passed;
                ElapsedMs = result.ElapsedMs;
            }

            public string Name { get; private set; }
            public string Message { get; private set; }
            public bool Passed { get; private set; }
            public long ElapsedMs { get; private set; }

            public string StatusGlyph { get { return Passed ? "✓" : "✗"; } }
            public SolidColorBrush StatusBrush { get { return Passed ? PassBrush : FailBrush; } }
            public string ElapsedDisplay { get { return ElapsedMs + " ms"; } }
            public Visibility MessageVisibility
            {
                get { return string.IsNullOrEmpty(Message) ? Visibility.Collapsed : Visibility.Visible; }
            }

            public event PropertyChangedEventHandler PropertyChanged;
        }
    }
}
