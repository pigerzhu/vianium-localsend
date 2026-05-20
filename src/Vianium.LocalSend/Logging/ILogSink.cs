// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0
// SPDX-FileCopyrightText: 2026 Angel Careaga <hello@angelcareaga.com>

namespace Vianium.LocalSend.Logging
{
    /// <summary>
    /// A logging destination. <see cref="AppLog"/> fans every record out
    /// to a set of these.
    ///
    /// Implementations MUST be non-blocking and thread-safe:
    /// <see cref="Write"/> is called from arbitrary threads (socket
    /// callbacks, background tasks, the UI thread). Slow work — file I/O
    /// above all — must be deferred to a background worker, never run
    /// inline. A throwing sink is caught and isolated by the dispatcher,
    /// but a sink that blocks would stall the caller.
    /// </summary>
    internal interface ILogSink
    {
        /// <summary>Cheap pre-check so callers can skip building messages.</summary>
        bool IsEnabled(LogLevel level);

        /// <summary>Accept one record. Must return promptly.</summary>
        void Write(LogEntry entry);
    }
}
