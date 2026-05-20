// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0
// SPDX-FileCopyrightText: 2026 Angel Careaga <hello@angelcareaga.com>

using Windows.Security.Cryptography;

namespace Vianium.LocalSend.Protocol
{
    internal static class TokenGenerator
    {
        public static string Generate()
        {
            var bytes = CryptographicBuffer.GenerateRandom(32);
            return CryptographicBuffer.EncodeToHexString(bytes).ToLowerInvariant();
        }

        public static string GenerateSessionId()
        {
            return System.Guid.NewGuid().ToString();
        }
    }
}
