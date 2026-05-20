// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0
// SPDX-FileCopyrightText: 2026 Angel Careaga <hello@angelcareaga.com>

namespace Vianium.LocalSend.Crypto
{
    /// <summary>
    /// Simple XOR-based string obfuscation. Prevents plaintext protocol
    /// constants from being visible in a decompiled binary.
    /// NOT cryptographic security — just raises the bar for casual reverse engineering.
    /// </summary>
    internal static class StringShield
    {
        private const byte Key = 0x5A;

        /// <summary>
        /// Encode a string to an obfuscated byte array (call at dev time to generate constants).
        /// </summary>
        public static byte[] Encode(string s)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(s);
            for (int i = 0; i < bytes.Length; i++)
                bytes[i] ^= (byte)(Key ^ (i & 0xFF));
            return bytes;
        }

        /// <summary>
        /// Decode an obfuscated byte array back to string (call at runtime).
        /// </summary>
        public static string Decode(byte[] data)
        {
            var result = new byte[data.Length];
            for (int i = 0; i < data.Length; i++)
                result[i] = (byte)(data[i] ^ (byte)(Key ^ (i & 0xFF)));
            return System.Text.Encoding.UTF8.GetString(result, 0, result.Length);
        }
    }
}
