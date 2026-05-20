// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0
// SPDX-FileCopyrightText: 2026 Angel Careaga <hello@angelcareaga.com>

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Vianium.LocalSend.Logging;
using Vianium.LocalSend.Strings;

namespace Vianium.LocalSend.Pages
{
    /// <summary>
    /// On-device log viewer. Two sources: the current-session in-memory
    /// buffer, or the full on-disk history (every rotated file). Both can
    /// be filtered by level; the on-disk history survives crashes and app
    /// restarts. The share action bundles every log file so a crash that
    /// has rotated out of the active file is still included.
    /// </summary>
    public sealed partial class DiagnosticsPage : Page
    {
        private const string TAG = "Diagnostics";
        private const int FileViewMaxLines = 4000;

        private static readonly SolidColorBrush BrushError =
            new SolidColorBrush(Color.FromArgb(255, 248, 81, 73));
        private static readonly SolidColorBrush BrushWarn =
            new SolidColorBrush(Color.FromArgb(255, 219, 171, 10));
        private static readonly SolidColorBrush BrushInfo =
            new SolidColorBrush(Color.FromArgb(255, 230, 237, 243));
        private static readonly SolidColorBrush BrushDebug =
            new SolidColorBrush(Color.FromArgb(255, 139, 148, 158));
        private static readonly SolidColorBrush BrushVerbose =
            new SolidColorBrush(Color.FromArgb(255, 110, 118, 129));

        private DataTransferManager _dataTransferManager;
        private IList<StorageFile> _shareFiles;
        private bool _loaded;

        public DiagnosticsPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            AppLog.V(TAG, "DiagnosticsPage opened");

            ApplyStrings();
            if (LevelFilter.Items.Count == 0)
                PopulateFilters();

            // Register the share source for this view; detached on leave.
            _dataTransferManager = DataTransferManager.GetForCurrentView();
            _dataTransferManager.DataRequested += OnDataRequested;

            _loaded = true;
            var ignore = RefreshListAsync();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            _loaded = false;
            if (_dataTransferManager != null)
            {
                _dataTransferManager.DataRequested -= OnDataRequested;
                _dataTransferManager = null;
            }
        }

        private void ApplyStrings()
        {
            TitleText.Text = S.Diagnostics;
            SourceLabel.Text = S.LogSourceLabel;
            FilterLabel.Text = S.LogLevelLabel;
            EmptyText.Text = S.LogEmpty;
            RefreshButton.Label = S.LogRefresh;
            ShareButton.Label = S.LogShare;
            ClearButton.Label = S.LogClear;
        }

        private void PopulateFilters()
        {
            // Source: in-memory current session vs. the full on-disk history.
            SourceFilter.Items.Add(new ComboBoxItem { Content = S.LogSourceMemory });
            SourceFilter.Items.Add(new ComboBoxItem { Content = S.LogSourceFile });
            SourceFilter.SelectedIndex = 0;

            // Level: each item's Tag carries the minimum LogLevel it selects.
            LevelFilter.Items.Add(MakeLevelItem(S.LogFilterAll, LogLevel.Verbose));
            LevelFilter.Items.Add(MakeLevelItem(S.LogFilterDebug, LogLevel.Debug));
            LevelFilter.Items.Add(MakeLevelItem(S.LogFilterInfo, LogLevel.Info));
            LevelFilter.Items.Add(MakeLevelItem(S.LogFilterWarn, LogLevel.Warning));
            LevelFilter.Items.Add(MakeLevelItem(S.LogFilterError, LogLevel.Error));
            LevelFilter.SelectedIndex = 2; // "Info and up" — a sensible default.
        }

        private static ComboBoxItem MakeLevelItem(string label, LogLevel level)
        {
            return new ComboBoxItem { Content = label, Tag = level };
        }

        private LogLevel SelectedMinLevel()
        {
            var item = LevelFilter.SelectedItem as ComboBoxItem;
            if (item != null && item.Tag is LogLevel)
                return (LogLevel)item.Tag;
            return LogLevel.Info;
        }

        private bool FileMode
        {
            get { return SourceFilter.SelectedIndex == 1; }
        }

        private void LevelFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loaded) { var ignore = RefreshListAsync(); }
        }

        private void SourceFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loaded) { var ignore = RefreshListAsync(); }
        }

        private async Task RefreshListAsync()
        {
            try
            {
                LogLevel min = SelectedMinLevel();
                List<LogLineItem> items = FileMode
                    ? await BuildFromFileAsync(min)
                    : BuildFromMemory(min);

                LogList.ItemsSource = items;
                bool any = items.Count > 0;
                LogList.Visibility = any ? Visibility.Visible : Visibility.Collapsed;
                EmptyState.Visibility = any ? Visibility.Collapsed : Visibility.Visible;

                if (any)
                {
                    try { LogList.ScrollIntoView(items[items.Count - 1]); }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                AppLog.E(TAG, "Refreshing the log view failed", ex);
            }
        }

        private List<LogLineItem> BuildFromMemory(LogLevel min)
        {
            LogEntry[] entries = AppLog.GetRecentEntries(min);
            var items = new List<LogLineItem>(entries.Length);
            for (int i = 0; i < entries.Length; i++)
            {
                items.Add(new LogLineItem
                {
                    Text = entries[i].ToLine(),
                    Brush = BrushFor(entries[i].Level)
                });
            }
            SubtitleText.Text = S.LogStatus(AppLog.EntryCount,
                AppLog.LogFolderName + "/" + AppLog.ActiveLogFileName);
            return items;
        }

        private async Task<List<LogLineItem>> BuildFromFileAsync(LogLevel min)
        {
            IList<string> lines = await AppLog.ReadLogFilesAsync(FileViewMaxLines);
            var items = new List<LogLineItem>(lines.Count);

            // Exception detail lines have no header — they inherit the level
            // of the entry above them so a stack trace is never filtered
            // away from its Error line.
            LogLevel lastLevel = LogLevel.Info;
            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];
                if (string.IsNullOrEmpty(line))
                    continue;

                LogLevel lvl;
                if (line[0] == '[')
                {
                    lvl = DetectLevel(line);
                    lastLevel = lvl;
                }
                else
                {
                    lvl = lastLevel;
                }

                if (lvl < min)
                    continue;

                items.Add(new LogLineItem { Text = line, Brush = BrushFor(lvl) });
            }

            SubtitleText.Text = S.LogFileStatus(items.Count);
            return items;
        }

        /// <summary>
        /// Recovers the level from a formatted line. The shape is fixed:
        /// <c>[HH:mm:ss.fff +Nms L/Tag tN] message</c> — the level char sits
        /// immediately after the first "ms " and before a '/'.
        /// </summary>
        private static LogLevel DetectLevel(string line)
        {
            int ms = line.IndexOf("ms ", StringComparison.Ordinal);
            if (ms > 0 && ms + 4 < line.Length && line[ms + 4] == '/')
            {
                switch (line[ms + 3])
                {
                    case 'E': return LogLevel.Error;
                    case 'W': return LogLevel.Warning;
                    case 'I': return LogLevel.Info;
                    case 'D': return LogLevel.Debug;
                    case 'V': return LogLevel.Verbose;
                }
            }
            return LogLevel.Info;
        }

        private static SolidColorBrush BrushFor(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Error: return BrushError;
                case LogLevel.Warning: return BrushWarn;
                case LogLevel.Info: return BrushInfo;
                case LogLevel.Debug: return BrushDebug;
                default: return BrushVerbose;
            }
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await AppLog.FlushToFileAsync();
            await RefreshListAsync();
        }

        private async void ShareButton_Click(object sender, RoutedEventArgs e)
        {
            await AppLog.FlushToFileAsync();

            // Bundle EVERY log file — a crash may have rotated out of the
            // active file into localsend.1.log … localsend.4.log.
            _shareFiles = await AppLog.GetLogFilesAsync();
            if (_shareFiles == null || _shareFiles.Count == 0)
            {
                await ShowMessageAsync(S.Diagnostics, S.LogNoFile);
                return;
            }

            try
            {
                DataTransferManager.ShowShareUI();
            }
            catch (Exception ex)
            {
                AppLog.E(TAG, "ShowShareUI failed", ex);
            }
        }

        private void OnDataRequested(DataTransferManager sender, DataRequestedEventArgs args)
        {
            if (_shareFiles == null || _shareFiles.Count == 0)
                return;

            var request = args.Request;
            request.Data.Properties.Title = S.LogShareTitle;
            request.Data.Properties.Description = S.LogShareDescription(_shareFiles.Count);

            var items = new List<IStorageItem>();
            for (int i = 0; i < _shareFiles.Count; i++)
                items.Add(_shareFiles[i]);
            request.Data.SetStorageItems(items);
        }

        private async void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new MessageDialog(S.LogClearConfirm, S.Diagnostics);
            dialog.Commands.Add(new UICommand(S.LogClear, async cmd =>
            {
                AppLog.ClearMemory();
                AppLog.I(TAG, "In-memory log buffer cleared by user");
                await RefreshListAsync();
            }));
            dialog.Commands.Add(new UICommand(S.Cancel));
            dialog.DefaultCommandIndex = 1;
            dialog.CancelCommandIndex = 1;
            await dialog.ShowAsync();
        }

        private static async Task ShowMessageAsync(string title, string message)
        {
            try
            {
                var dialog = new MessageDialog(message, title);
                await dialog.ShowAsync();
            }
            catch
            {
                // A failed dialog must not bubble out of a diagnostics screen.
            }
        }
    }

    /// <summary>One rendered log row: pre-formatted text plus a level colour.</summary>
    internal sealed class LogLineItem
    {
        public string Text { get; set; }
        public SolidColorBrush Brush { get; set; }
    }
}
