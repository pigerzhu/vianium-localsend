// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0
// SPDX-FileCopyrightText: 2026 Angel Careaga <hello@angelcareaga.com>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace Vianium.LocalSend.Logging
{
    /// <summary>
    /// Static logging facade for Vianium.LocalSend.
    ///
    /// Architecture (modeled on the Vianium.Browser kernel logger): every
    /// call builds a single immutable <see cref="LogEntry"/> and fans it
    /// out to a fixed set of <see cref="ILogSink"/>s. A failure in one
    /// sink is caught and isolated — it can never break logging or the
    /// app. The sinks are:
    /// <list type="bullet">
    ///   <item><see cref="DebugSink"/> — the Visual Studio Output window.</item>
    ///   <item><see cref="MemorySink"/> — a circular buffer that backs the
    ///     in-app log viewer (<c>DiagnosticsPage</c>).</item>
    ///   <item><see cref="RotatingFileSink"/> — size-bounded, rotating
    ///     log files under <c>LocalFolder\logs\</c>.</item>
    /// </list>
    ///
    /// DEBUG builds log <see cref="LogLevel.Verbose"/> and up; Release
    /// builds log <see cref="LogLevel.Info"/> and up. <see cref="V"/> and
    /// <see cref="Hex"/> are additionally compiled out of Release entirely.
    ///
    /// Call <see cref="Init"/> once, as early as possible (the App
    /// constructor), to bring the file sink online.
    /// </summary>
    internal static class AppLog
    {
        // ── Tunables ───────────────────────────────────────────────
        private const int MemoryCapacity = 1000;
        private const long MaxLogFileBytes = 256 * 1024;
        private const int MaxLogFiles = 5;

        /// <summary>Records below this level are dropped before any sink sees them.</summary>
        public static LogLevel MinLevel =
#if DEBUG
            LogLevel.Verbose;
#else
            LogLevel.Info;
#endif

        private static readonly long _startTicks = DateTime.UtcNow.Ticks;

        private static readonly MemorySink _memory =
            new MemorySink(MemoryCapacity, LogLevel.Verbose);
        private static readonly DebugSink _debug =
            new DebugSink(LogLevel.Verbose);
        private static readonly RotatingFileSink _file =
            new RotatingFileSink(LogLevel.Verbose, "logs", "localsend", ".log",
                MaxLogFileBytes, MaxLogFiles);

        // Reference is swapped atomically by Init(); readers take a local
        // copy, so no lock is needed on the hot path.
        private static ILogSink[] _sinks = { _debug, _memory };

        private static bool _initialized;
        private static readonly object _initLock = new object();

        /// <summary>
        /// Bring the rotating file sink online and start its background
        /// drain loop. Idempotent — safe to call more than once.
        /// </summary>
        public static void Init()
        {
            lock (_initLock)
            {
                if (_initialized) return;
                _initialized = true;
                _sinks = new ILogSink[] { _debug, _memory, _file };
                _file.Start();
            }

            I("AppLog", "Logging online — sinks: Debug, Memory(" + MemoryCapacity +
                "), RotatingFile(" + (MaxLogFileBytes / 1024) + "KB x" + MaxLogFiles +
                "), minLevel=" + MinLevel);
        }

        // ── Level entry points ─────────────────────────────────────

        /// <summary>Verbose — DEBUG builds only; the whole call is stripped from Release.</summary>
        [Conditional("DEBUG")]
        public static void V(string tag, string message)
        {
            Dispatch(LogLevel.Verbose, tag, message, null);
        }

        /// <summary>Debug — diagnostic detail; compiled in, filtered by <see cref="MinLevel"/>.</summary>
        public static void D(string tag, string message)
        {
            Dispatch(LogLevel.Debug, tag, message, null);
        }

        /// <summary>Info — notable lifecycle/flow events.</summary>
        public static void I(string tag, string message)
        {
            Dispatch(LogLevel.Info, tag, message, null);
        }

        /// <summary>Warning — recoverable problems worth attention.</summary>
        public static void W(string tag, string message)
        {
            Dispatch(LogLevel.Warning, tag, message, null);
        }

        /// <summary>Error — failures, with optional exception detail. Always logged.</summary>
        public static void E(string tag, string message, Exception ex = null)
        {
            Dispatch(LogLevel.Error, tag, message, ex);
        }

        /// <summary>Start a timing scope; call <see cref="AppTimer.Stop"/> to log the elapsed time.</summary>
        public static AppTimer Time(string tag, string operation)
        {
            return new AppTimer(tag, operation);
        }

        /// <summary>Log a hex dump of <paramref name="data"/> (DEBUG builds only).</summary>
        [Conditional("DEBUG")]
        public static void Hex(string tag, string label, byte[] data, int maxBytes = 32)
        {
            if (data == null)
            {
                V(tag, label + ": null");
                return;
            }

            int show = Math.Min(data.Length, maxBytes);
            var sb = new StringBuilder(show * 2 + 24);
            sb.Append(label);
            sb.Append(" (");
            sb.Append(data.Length);
            sb.Append("B): ");
            for (int i = 0; i < show; i++)
                sb.Append(data[i].ToString("X2"));
            if (show < data.Length)
                sb.Append("...");
            V(tag, sb.ToString());
        }

        // ── Viewer / export support ────────────────────────────────

        /// <summary>Snapshot of buffered records (oldest first) at or above <paramref name="minLevel"/>.</summary>
        public static LogEntry[] GetRecentEntries(LogLevel minLevel)
        {
            return _memory.Snapshot(minLevel);
        }

        /// <summary>Most recent log lines as formatted text, newest last.</summary>
        public static string[] GetRecentLogs(int count)
        {
            LogEntry[] all = _memory.Snapshot(LogLevel.Verbose);
            if (count >= all.Length)
            {
                var full = new string[all.Length];
                for (int i = 0; i < all.Length; i++)
                    full[i] = all[i].ToLine();
                return full;
            }

            if (count < 0) count = 0;
            var result = new string[count];
            int offset = all.Length - count;
            for (int i = 0; i < count; i++)
                result[i] = all[offset + i].ToLine();
            return result;
        }

        /// <summary>Number of records currently held in the in-memory buffer.</summary>
        public static int EntryCount { get { return _memory.Count; } }

        /// <summary>Drop every record from the in-memory buffer (does not touch the files).</summary>
        public static void ClearMemory()
        {
            _memory.Clear();
        }

        /// <summary>Sub-folder of LocalFolder that holds the rotating log files.</summary>
        public static string LogFolderName { get { return _file.FolderName; } }

        /// <summary>File name of the current log, e.g. "localsend.log".</summary>
        public static string ActiveLogFileName { get { return _file.ActiveFileName; } }

        /// <summary>Every log file on disk (active + rotated archives), newest first.</summary>
        public static Task<IList<StorageFile>> GetLogFilesAsync()
        {
            return _file.GetLogFilesAsync();
        }

        /// <summary>
        /// All on-disk log lines across every generation, oldest first,
        /// capped to the most recent <paramref name="maxLines"/> lines.
        /// </summary>
        public static Task<IList<string>> ReadLogFilesAsync(int maxLines)
        {
            return _file.ReadAllLinesAsync(maxLines);
        }

        // ── Flush ──────────────────────────────────────────────────

        /// <summary>
        /// Force the rotating file sink to drain its queue to disk now.
        /// Awaitable — use from app-suspend and similar deferred paths.
        /// The legacy <paramref name="fileName"/> parameter is ignored.
        /// </summary>
        public static async Task FlushToFileAsync(string fileName = null)
        {
            try
            {
                await _file.FlushAsync();
            }
            catch
            {
                // Flushing diagnostics must never throw into a caller.
            }
        }

        /// <summary>
        /// Best-effort synchronous flush for the unhandled-exception and
        /// suspend paths, where there is no opportunity to await. The
        /// sink's awaits are context-free, so blocking here cannot
        /// deadlock the UI thread; it simply gives the write a bounded
        /// window to land before the process is torn down.
        /// </summary>
        public static void FlushBlocking(int timeoutMs = 1500)
        {
            try
            {
                _file.FlushAsync().Wait(timeoutMs);
            }
            catch
            {
                // Last-resort path — swallow everything.
            }
        }

        // ── Core dispatch ──────────────────────────────────────────

        private static void Dispatch(LogLevel level, string tag, string message, Exception ex)
        {
            if (level < MinLevel) return;

            long elapsedMs = (DateTime.UtcNow.Ticks - _startTicks) / TimeSpan.TicksPerMillisecond;
            var entry = new LogEntry(DateTime.UtcNow, elapsedMs, level, tag, message, ex,
                Environment.CurrentManagedThreadId);

            ILogSink[] sinks = _sinks;
            for (int i = 0; i < sinks.Length; i++)
            {
                try
                {
                    if (sinks[i].IsEnabled(level))
                        sinks[i].Write(entry);
                }
                catch
                {
                    // A broken sink must never break logging for the rest.
                }
            }
        }
    }

    /// <summary>Lightweight scope for measuring an operation's duration.</summary>
    internal sealed class AppTimer
    {
        private readonly string _tag;
        private readonly string _operation;
        private readonly long _startTicks;

        internal AppTimer(string tag, string operation)
        {
            _tag = tag;
            _operation = operation;
            _startTicks = DateTime.UtcNow.Ticks;
            AppLog.V(_tag, _operation + " started");
        }

        /// <summary>Elapsed milliseconds since the timer was created.</summary>
        public long ElapsedMs
        {
            get { return (DateTime.UtcNow.Ticks - _startTicks) / TimeSpan.TicksPerMillisecond; }
        }

        /// <summary>Log the elapsed time at Info level.</summary>
        public void Stop()
        {
            AppLog.I(_tag, _operation + " completed in " + ElapsedMs + "ms");
        }
    }
}
