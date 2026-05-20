// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0
// SPDX-FileCopyrightText: 2026 Angel Careaga <hello@angelcareaga.com>

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;

namespace Vianium.LocalSend.Logging
{
    /// <summary>
    /// Size-bounded rotating file sink for on-device logs.
    ///
    /// Records are queued in memory and drained to disk by a single
    /// background loop every <see cref="FlushIntervalMs"/> ms — the
    /// Windows.Storage file API is async-only, so a write can never be
    /// done inline on the caller's thread. When appending the next batch
    /// would push the active file past <see cref="_maxFileBytes"/>, the
    /// generations are rotated:
    /// <code>
    ///   localsend.log → localsend.1.log → localsend.2.log → …
    /// </code>
    /// up to <see cref="_maxFiles"/> files total, after which the oldest
    /// is dropped.
    ///
    /// Every <c>await</c> uses <c>ConfigureAwait(false)</c> so that
    /// <see cref="FlushAsync"/> can be safely blocked on from the UI
    /// thread during app suspend or an unhandled-exception handler
    /// without deadlocking on the dispatcher.
    /// </summary>
    internal sealed class RotatingFileSink : ILogSink
    {
        private const int FlushIntervalMs = 2000;
        private const int MaxQueuedLines = 8192;

        private readonly LogLevel _minLevel;
        private readonly string _folderName;
        private readonly string _baseName;
        private readonly string _extension;
        private readonly long _maxFileBytes;
        private readonly int _maxFiles;

        private readonly Queue<string> _queue = new Queue<string>();
        private readonly object _queueLock = new object();
        private readonly SemaphoreSlim _writeGate = new SemaphoreSlim(1, 1);

        private StorageFolder _folder;
        private bool _running;
        private int _droppedLines;

        public RotatingFileSink(LogLevel minLevel, string folderName, string baseName,
            string extension, long maxFileBytes, int maxFiles)
        {
            _minLevel = minLevel;
            _folderName = string.IsNullOrEmpty(folderName) ? "logs" : folderName;
            _baseName = string.IsNullOrEmpty(baseName) ? "localsend" : baseName;
            _extension = string.IsNullOrEmpty(extension) ? ".log" : extension;
            _maxFileBytes = maxFileBytes > 4096 ? maxFileBytes : 4096;
            _maxFiles = maxFiles < 2 ? 2 : maxFiles;
        }

        /// <summary>File name of the current (newest) log, e.g. "localsend.log".</summary>
        public string ActiveFileName { get { return _baseName + _extension; } }

        /// <summary>Sub-folder of LocalFolder that holds the log files.</summary>
        public string FolderName { get { return _folderName; } }

        public bool IsEnabled(LogLevel level)
        {
            return level >= _minLevel;
        }

        public void Write(LogEntry entry)
        {
            if (entry == null || entry.Level < _minLevel) return;
            lock (_queueLock)
            {
                if (_queue.Count >= MaxQueuedLines)
                {
                    // Back-pressure: disk is too slow or stuck. Drop the
                    // record rather than let the queue grow without bound,
                    // and report the loss on the next successful flush.
                    _droppedLines++;
                    return;
                }
                _queue.Enqueue(entry.ToLine());
            }
        }

        /// <summary>Start the background drain loop. Idempotent.</summary>
        public void Start()
        {
            lock (_queueLock)
            {
                if (_running) return;
                _running = true;
            }
            var ignore = RunLoopAsync();
        }

        /// <summary>Stop the background drain loop. A final <see cref="FlushAsync"/> is still honored.</summary>
        public void Stop()
        {
            lock (_queueLock) { _running = false; }
        }

        private async Task RunLoopAsync()
        {
            while (true)
            {
                lock (_queueLock) { if (!_running) break; }
                try
                {
                    await Task.Delay(FlushIntervalMs).ConfigureAwait(false);
                    await FlushAsync().ConfigureAwait(false);
                }
                catch
                {
                    // The drain loop must never die — a failed tick is
                    // retried on the next interval.
                }
            }
        }

        /// <summary>
        /// Drain everything queued so far to disk. Safe to <c>await</c>
        /// or to block on (<c>.Wait()</c>) from any thread.
        /// </summary>
        public async Task FlushAsync()
        {
            string[] batch;
            int dropped;
            lock (_queueLock)
            {
                if (_queue.Count == 0 && _droppedLines == 0) return;
                batch = _queue.ToArray();
                _queue.Clear();
                dropped = _droppedLines;
                _droppedLines = 0;
            }

            await _writeGate.WaitAsync().ConfigureAwait(false);
            try
            {
                await WriteBatchAsync(batch, dropped).ConfigureAwait(false);
            }
            catch
            {
                // Disk full / access denied / transient WinRT failure —
                // diagnostics must never crash the app, so the batch is
                // dropped silently.
            }
            finally
            {
                _writeGate.Release();
            }
        }

        private async Task WriteBatchAsync(string[] batch, int dropped)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < batch.Length; i++)
                sb.AppendLine(batch[i]);
            if (dropped > 0)
                sb.AppendLine("[logger] dropped " + dropped.ToString(CultureInfo.InvariantCulture)
                    + " line(s) — in-memory queue overflowed");

            string text = sb.ToString();
            if (text.Length == 0) return;

            StorageFolder folder = await GetFolderAsync().ConfigureAwait(false);
            StorageFile file = await folder.CreateFileAsync(ActiveFileName,
                CreationCollisionOption.OpenIfExists).AsTask().ConfigureAwait(false);

            var props = await file.GetBasicPropertiesAsync().AsTask().ConfigureAwait(false);
            long currentSize = (long)props.Size;
            long incomingBytes = Encoding.UTF8.GetByteCount(text);

            if (currentSize > 0 && currentSize + incomingBytes > _maxFileBytes)
            {
                await RotateAsync(folder).ConfigureAwait(false);
                file = await folder.CreateFileAsync(ActiveFileName,
                    CreationCollisionOption.ReplaceExisting).AsTask().ConfigureAwait(false);
            }

            await FileIO.AppendTextAsync(file, text).AsTask().ConfigureAwait(false);
        }

        private async Task<StorageFolder> GetFolderAsync()
        {
            if (_folder != null) return _folder;
            var local = ApplicationData.Current.LocalFolder;
            _folder = await local.CreateFolderAsync(_folderName,
                CreationCollisionOption.OpenIfExists).AsTask().ConfigureAwait(false);
            return _folder;
        }

        private async Task RotateAsync(StorageFolder folder)
        {
            // Drop the oldest generation (localsend.{maxFiles-1}.log).
            string oldest = _baseName + "." + (_maxFiles - 1).ToString(CultureInfo.InvariantCulture) + _extension;
            StorageFile oldestFile = await TryGetFileAsync(folder, oldest).ConfigureAwait(false);
            if (oldestFile != null)
                await oldestFile.DeleteAsync().AsTask().ConfigureAwait(false);

            // Shift the survivors down one slot: .(n-2) -> .(n-1) ... .1 -> .2
            for (int i = _maxFiles - 2; i >= 1; i--)
            {
                string src = _baseName + "." + i.ToString(CultureInfo.InvariantCulture) + _extension;
                string dst = _baseName + "." + (i + 1).ToString(CultureInfo.InvariantCulture) + _extension;
                StorageFile f = await TryGetFileAsync(folder, src).ConfigureAwait(false);
                if (f != null)
                    await f.RenameAsync(dst, NameCollisionOption.ReplaceExisting)
                        .AsTask().ConfigureAwait(false);
            }

            // The active file becomes generation 1.
            StorageFile active = await TryGetFileAsync(folder, ActiveFileName).ConfigureAwait(false);
            if (active != null)
                await active.RenameAsync(_baseName + ".1" + _extension, NameCollisionOption.ReplaceExisting)
                    .AsTask().ConfigureAwait(false);
        }

        /// <summary>
        /// WP 8.1's <see cref="StorageFolder"/> has no <c>TryGetItemAsync</c>;
        /// emulate it with <c>GetFileAsync</c> and swallow the not-found case.
        /// </summary>
        private static async Task<StorageFile> TryGetFileAsync(StorageFolder folder, string name)
        {
            try
            {
                return await folder.GetFileAsync(name).AsTask().ConfigureAwait(false);
            }
            catch (System.IO.FileNotFoundException)
            {
                return null;
            }
        }

        /// <summary>
        /// Every log file that currently exists — the active file plus any
        /// rotated archives — newest first. Used by the in-app viewer to
        /// share the complete history, since a crash may have rotated out
        /// of the active file.
        /// </summary>
        public async Task<IList<StorageFile>> GetLogFilesAsync()
        {
            var result = new List<StorageFile>();
            try
            {
                var folder = await GetFolderAsync().ConfigureAwait(false);
                var active = await TryGetFileAsync(folder, ActiveFileName).ConfigureAwait(false);
                if (active != null) result.Add(active);
                for (int i = 1; i < _maxFiles; i++)
                {
                    string name = _baseName + "." + i.ToString(CultureInfo.InvariantCulture) + _extension;
                    var f = await TryGetFileAsync(folder, name).ConfigureAwait(false);
                    if (f != null) result.Add(f);
                }
            }
            catch
            {
                // Folder unavailable — return whatever was gathered.
            }
            return result;
        }

        /// <summary>
        /// Every log line across all generations, oldest first. The read is
        /// serialized against the background flush (same write gate) so it
        /// cannot collide with an append in progress. When
        /// <paramref name="maxLines"/> &gt; 0 only the most recent that many
        /// lines are returned.
        /// </summary>
        public async Task<IList<string>> ReadAllLinesAsync(int maxLines)
        {
            var all = new List<string>();

            await _writeGate.WaitAsync().ConfigureAwait(false);
            try
            {
                var folder = await GetFolderAsync().ConfigureAwait(false);

                // Oldest archive first, then … then the active file last.
                for (int i = _maxFiles - 1; i >= 1; i--)
                {
                    string name = _baseName + "." + i.ToString(CultureInfo.InvariantCulture) + _extension;
                    var f = await TryGetFileAsync(folder, name).ConfigureAwait(false);
                    if (f != null)
                    {
                        var lines = await FileIO.ReadLinesAsync(f).AsTask().ConfigureAwait(false);
                        all.AddRange(lines);
                    }
                }

                var active = await TryGetFileAsync(folder, ActiveFileName).ConfigureAwait(false);
                if (active != null)
                {
                    var lines = await FileIO.ReadLinesAsync(active).AsTask().ConfigureAwait(false);
                    all.AddRange(lines);
                }
            }
            catch
            {
                // Unreadable log folder — return whatever was gathered.
            }
            finally
            {
                _writeGate.Release();
            }

            if (maxLines > 0 && all.Count > maxLines)
                all = all.GetRange(all.Count - maxLines, maxLines);
            return all;
        }
    }
}
