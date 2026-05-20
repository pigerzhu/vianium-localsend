// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0
// SPDX-FileCopyrightText: 2026 Angel Careaga <hello@angelcareaga.com>

using System;

namespace Vianium.LocalSend.Logging
{
    /// <summary>
    /// In-memory circular buffer of the most recent records. Backs the
    /// in-app log viewer (<c>DiagnosticsPage</c>) and the manual export.
    /// Lock-guarded; once <see cref="Capacity"/> is reached the oldest
    /// record is overwritten.
    /// </summary>
    internal sealed class MemorySink : ILogSink
    {
        private readonly LogEntry[] _buffer;
        private readonly LogLevel _minLevel;
        private readonly object _lock = new object();
        private int _head;
        private int _count;

        public MemorySink(int capacity, LogLevel minLevel)
        {
            if (capacity < 1) capacity = 1;
            _buffer = new LogEntry[capacity];
            _minLevel = minLevel;
        }

        public int Capacity { get { return _buffer.Length; } }

        public int Count { get { lock (_lock) { return _count; } } }

        public bool IsEnabled(LogLevel level)
        {
            return level >= _minLevel;
        }

        public void Write(LogEntry entry)
        {
            if (entry == null || entry.Level < _minLevel) return;
            lock (_lock)
            {
                _buffer[_head] = entry;
                _head = (_head + 1) % _buffer.Length;
                if (_count < _buffer.Length) _count++;
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                Array.Clear(_buffer, 0, _buffer.Length);
                _head = 0;
                _count = 0;
            }
        }

        /// <summary>
        /// Snapshot of the buffered records, oldest first, keeping only
        /// those at or above <paramref name="minLevel"/>.
        /// </summary>
        public LogEntry[] Snapshot(LogLevel minLevel)
        {
            lock (_lock)
            {
                int start = (_head - _count + _buffer.Length) % _buffer.Length;
                var tmp = new LogEntry[_count];
                int n = 0;
                for (int i = 0; i < _count; i++)
                {
                    var e = _buffer[(start + i) % _buffer.Length];
                    if (e != null && e.Level >= minLevel)
                        tmp[n++] = e;
                }
                if (n == tmp.Length) return tmp;
                var result = new LogEntry[n];
                Array.Copy(tmp, result, n);
                return result;
            }
        }
    }
}
