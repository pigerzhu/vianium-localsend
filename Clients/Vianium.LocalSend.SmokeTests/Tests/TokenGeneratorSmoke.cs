// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0
// SPDX-FileCopyrightText: 2026 Angel Careaga <hello@angelcareaga.com>

using System;
using System.Threading.Tasks;
using Vianium.LocalSend.Protocol;

namespace Vianium.LocalSend.SmokeTests
{
    /// <summary>
    /// Verifies the contract that LocalSend places on the per-file
    /// upload token (64 lowercase hex chars from 32 random bytes) and
    /// the per-session id (RFC 4122 GUID string). A naive RNG would
    /// betray itself by returning identical or short tokens; this
    /// smoke draws 16 tokens and checks the entropy floor.
    /// </summary>
    internal static class TokenGeneratorSmoke
    {
        public static Task<SmokeResult> EntropyAndFormat()
        {
            var seen = new System.Collections.Generic.HashSet<string>();
            for (int i = 0; i < 16; i++)
            {
                string token = TokenGenerator.Generate();
                if (token == null || token.Length != 64)
                    return Fail("EntropyAndFormat",
                        "expected 64 hex chars, got " +
                        (token == null ? "null" : token.Length.ToString()));
                if (!IsLowercaseHex(token))
                    return Fail("EntropyAndFormat",
                        "token contains non-hex or uppercase characters: " + token);
                if (!seen.Add(token))
                    return Fail("EntropyAndFormat",
                        "duplicate token in 16 draws — RNG is broken");
            }
            return Pass("EntropyAndFormat");
        }

        public static Task<SmokeResult> SessionIdIsGuid()
        {
            string sid = TokenGenerator.GenerateSessionId();
            Guid parsed;
            if (!Guid.TryParse(sid, out parsed))
                return Fail("SessionIdIsGuid",
                    "GenerateSessionId did not return a parseable GUID: " + sid);
            if (parsed == Guid.Empty)
                return Fail("SessionIdIsGuid", "session id is empty GUID");

            // Two consecutive draws must differ (defensive: Guid.NewGuid
            // implementations have a vanishingly small but non-zero
            // collision probability; we only check inequality, not
            // version bits, since the spec only requires uniqueness).
            string sid2 = TokenGenerator.GenerateSessionId();
            if (sid == sid2)
                return Fail("SessionIdIsGuid", "two GenerateSessionId calls returned the same value");

            return Pass("SessionIdIsGuid");
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
