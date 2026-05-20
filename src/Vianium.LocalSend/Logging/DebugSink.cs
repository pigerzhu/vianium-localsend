// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0
// SPDX-FileCopyrightText: 2026 Angel Careaga <hello@angelcareaga.com>

using System.Diagnostics;

namespace Vianium.LocalSend.Logging
{
    /// <summary>
    /// Sink that forwards each record to <see cref="Debug.WriteLine(string)"/>,
    /// i.e. the Visual Studio Output window during a debug session. Stripped
    /// of effect in Release builds where <see cref="Debug"/> is a no-op.
    /// </summary>
    internal sealed class DebugSink : ILogSink
    {
        private readonly LogLevel _minLevel;

        public DebugSink(LogLevel minLevel)
        {
            _minLevel = minLevel;
        }

        public bool IsEnabled(LogLevel level)
        {
            return level >= _minLevel;
        }

        public void Write(LogEntry entry)
        {
            if (entry != null && entry.Level >= _minLevel)
                Debug.WriteLine(entry.ToLine());
        }
    }
}
