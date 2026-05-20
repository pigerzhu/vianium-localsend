// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0
// SPDX-FileCopyrightText: 2026 Angel Careaga <hello@angelcareaga.com>

using System;
using System.Globalization;
using System.Text;

namespace Vianium.LocalSend.Logging
{
    /// <summary>
    /// One immutable log record. Carries the structured fields so each
    /// sink can filter or render however it needs; <see cref="ToLine"/>
    /// produces the canonical single-line text form and memoizes it, so
    /// fanning the same entry out to several sinks formats it only once.
    /// </summary>
    internal sealed class LogEntry
    {
        public readonly DateTime TimestampUtc;
        public readonly long ElapsedMs;
        public readonly LogLevel Level;
        public readonly string Tag;
        public readonly string Message;
        public readonly Exception Exception;
        public readonly int ThreadId;

        private string _line;

        public LogEntry(DateTime timestampUtc, long elapsedMs, LogLevel level,
            string tag, string message, Exception exception, int threadId)
        {
            TimestampUtc = timestampUtc;
            ElapsedMs = elapsedMs;
            Level = level;
            Tag = string.IsNullOrEmpty(tag) ? "?" : tag;
            Message = message ?? string.Empty;
            Exception = exception;
            ThreadId = threadId;
        }

        public static char LevelChar(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Verbose: return 'V';
                case LogLevel.Debug: return 'D';
                case LogLevel.Info: return 'I';
                case LogLevel.Warning: return 'W';
                case LogLevel.Error: return 'E';
                default: return '?';
            }
        }

        /// <summary>
        /// Canonical line: <c>[HH:mm:ss.fff +Nms L/Tag tN] message</c>.
        /// Exception type, message and stack trace, when present, follow
        /// on indented continuation lines. Memoized — safe to call from
        /// every sink without recomputing.
        /// </summary>
        public string ToLine()
        {
            if (_line != null) return _line;

            var sb = new StringBuilder(112);
            sb.Append('[');
            sb.Append(TimestampUtc.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture));
            sb.Append(" +");
            sb.Append(ElapsedMs.ToString(CultureInfo.InvariantCulture));
            sb.Append("ms ");
            sb.Append(LevelChar(Level));
            sb.Append('/');
            sb.Append(Tag);
            sb.Append(" t");
            sb.Append(ThreadId.ToString(CultureInfo.InvariantCulture));
            sb.Append("] ");
            sb.Append(Message);

            if (Exception != null)
            {
                sb.Append(Environment.NewLine);
                sb.Append("    ");
                sb.Append(Exception.GetType().Name);
                sb.Append(": ");
                sb.Append(Exception.Message);
                if (!string.IsNullOrEmpty(Exception.StackTrace))
                {
                    sb.Append(Environment.NewLine);
                    sb.Append(Exception.StackTrace);
                }
            }

            _line = sb.ToString();
            return _line;
        }
    }
}
