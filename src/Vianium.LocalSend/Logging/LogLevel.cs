// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0
// SPDX-FileCopyrightText: 2026 Angel Careaga <hello@angelcareaga.com>

namespace Vianium.LocalSend.Logging
{
    /// <summary>
    /// Severity ladder for <see cref="AppLog"/>. Lower value = noisier.
    /// Mirrors the level model of the Vianium.Browser kernel logger,
    /// with <c>Verbose</c> added below <c>Debug</c> for hex/wire dumps.
    /// </summary>
    internal enum LogLevel
    {
        Verbose = 0,
        Debug = 1,
        Info = 2,
        Warning = 3,
        Error = 4
    }
}
