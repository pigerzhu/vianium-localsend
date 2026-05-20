// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0
// SPDX-FileCopyrightText: 2026 Angel Careaga <hello@angelcareaga.com>

using System.Threading.Tasks;
using Vianium.LocalSend.Crypto;

namespace Vianium.LocalSend.SmokeTests
{
    /// <summary>
    /// The device fingerprint advertised over UDP is the SHA-256 of 32
    /// random bytes — 64 lowercase hex characters. We exercise the
    /// generator across multiple draws to assert format and entropy.
    /// </summary>
    internal static class FingerprintGeneratorSmoke
    {
        public static Task<SmokeResult> EntropyAndFormat()
        {
            var seen = new System.Collections.Generic.HashSet<string>();
            for (int i = 0; i < 8; i++)
            {
                string fp = FingerprintGenerator.Generate();
                if (fp == null || fp.Length != 64)
                    return Fail("EntropyAndFormat",
                        "expected 64 hex chars, got " +
                        (fp == null ? "null" : fp.Length.ToString()));
                if (!IsLowercaseHex(fp))
                    return Fail("EntropyAndFormat",
                        "fingerprint contains non-hex characters: " + fp);
                if (!seen.Add(fp))
                    return Fail("EntropyAndFormat",
                        "duplicate fingerprint in 8 draws — RNG/SHA-256 chain is broken");
            }
            return Pass("EntropyAndFormat");
        }

        private static bool IsLowercaseHex(string s)
        {
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if ((c < '0' || c > '9') && (c < 'a' || c > 'f'))
                    return false;
            }
            return true;
        }

        private static Task<SmokeResult> Pass(string name)
        {
            return Task.FromResult(SmokeResult.Pass(name, 0));
        }

        private static Task<SmokeResult> Fail(string name, string reason)
        {
            return Task.FromResult(SmokeResult.Fail(name, 0, reason));
        }
    }
}
