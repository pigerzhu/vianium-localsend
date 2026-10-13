// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0
// SPDX-FileCopyrightText: 2026 Angel Careaga <hello@angelcareaga.com>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Vianium.LocalSend.SmokeTests
{
    /// <summary>
    /// Outcome of a single smoke test case. Immutable.
    /// </summary>
    public struct SmokeResult
    {
        public SmokeResult(string name, bool passed, string message, long elapsedMs)
        {
            Name = name;
            Passed = passed;
            Message = message;
            ElapsedMs = elapsedMs;
        }

        public readonly string Name;
        public readonly bool Passed;
        public readonly string Message;
        public readonly long ElapsedMs;

        public static SmokeResult Pass(string name, long elapsedMs)
        {
            return new SmokeResult(name, true, string.Empty, elapsedMs);
        }

        public static SmokeResult Pass(string name, long elapsedMs, string detail)
        {
            return new SmokeResult(name, true, detail, elapsedMs);
        }

        public static SmokeResult Fail(string name, long elapsedMs, string message)
        {
            return new SmokeResult(name, false, message, elapsedMs);
        }
    }

    /// <summary>
    /// Aggregates the LocalSend smoke-test cases and runs them one by
    /// one, streaming each result to an optional callback so a host UI
    /// can render rows as they complete.
    /// </summary>
    public static class SmokeTestRunner
    {
        public delegate Task<SmokeResult> SmokeCase();

        private static readonly List<KeyValuePair<string, SmokeCase>> _cases =
            BuildCaseList();

        /// <summary>
        /// Number of registered smoke cases. Stable; safe to call before
        /// <see cref="RunAllAsync"/> kicks off.
        /// </summary>
        public static int TotalCases { get { return _cases.Count; } }

        public static async Task<IList<SmokeResult>> RunAllAsync(Action<SmokeResult> onResult)
        {
            var results = new List<SmokeResult>(_cases.Count);
            foreach (var kvp in _cases)
            {
                SmokeResult result = await RunOneAsync(kvp.Key, kvp.Value);
                results.Add(result);
                if (onResult != null)
                {
                    try { onResult(result); }
                    catch { /* host UI is responsible for its own crashes */ }
                }
            }
            return results;
        }

        public static async Task<SmokeResult> RunOneAsync(string name, SmokeCase test)
        {
            Stopwatch sw = Stopwatch.StartNew();
            try
            {
                SmokeResult inner = await test();
                sw.Stop();
                // Re-tag with the registry name and propagate inner timing
                // when the test reported its own (otherwise use measured).
                long elapsed = inner.ElapsedMs > 0 ? inner.ElapsedMs : sw.ElapsedMilliseconds;
                return new SmokeResult(name, inner.Passed, inner.Message, elapsed);
            }
            catch (Exception ex)
            {
                sw.Stop();
                return SmokeResult.Fail(name, sw.ElapsedMilliseconds,
                    ex.GetType().Name + ": " + ex.Message);
            }
        }

        private static List<KeyValuePair<string, SmokeCase>> BuildCaseList()
        {
            var list = new List<KeyValuePair<string, SmokeCase>>();

            // 1. JSON parser — foundation of every protocol payload
            list.Add(Make("Json: parse primitives",        SimpleJsonParserSmoke.ParsePrimitives));
            list.Add(Make("Json: parse object + array",    SimpleJsonParserSmoke.ParseObjectAndArray));
            list.Add(Make("Json: parse escaped strings",   SimpleJsonParserSmoke.ParseEscapedStrings));
            list.Add(Make("Json: parse numbers",           SimpleJsonParserSmoke.ParseNumbers));
            list.Add(Make("Json: roundtrip object",        SimpleJsonParserSmoke.RoundtripObject));

            // 2. Crypto primitives
            list.Add(Make("Crypto: token entropy + format",       TokenGeneratorSmoke.EntropyAndFormat));
            list.Add(Make("Crypto: session id is GUID",            TokenGeneratorSmoke.SessionIdIsGuid));
            list.Add(Make("Crypto: fingerprint entropy + format",  FingerprintGeneratorSmoke.EntropyAndFormat));

            // 3. Protocol contracts vs the LocalSend v2 spec
            list.Add(Make("Protocol: constants match spec",       ProtocolConstantsSmoke.MatchSpec));

            // 4. DTO roundtrips — wire encoder/decoder contract
            list.Add(Make("DTO: DeviceInfo multicast roundtrip",  DeviceInfoSmoke.MulticastRoundtrip));
            list.Add(Make("DTO: DeviceInfo info roundtrip",       DeviceInfoSmoke.InfoRoundtrip));
            list.Add(Make("DTO: DeviceInfo register roundtrip",   DeviceInfoSmoke.RegisterRoundtrip));
            list.Add(Make("DTO: DeviceInfo handles missing fields", DeviceInfoSmoke.HandlesMissingFields));
            list.Add(Make("DTO: FileDto roundtrip",                FileDtoSmoke.Roundtrip));
            list.Add(Make("DTO: FileDto mime guesser",             FileDtoSmoke.MimeGuesser));

            // 5. HTTP request parser — the server's front door
            list.Add(Make("HTTP: parse GET request",      RequestParserSmoke.ParseGet));
            list.Add(Make("HTTP: parse POST + body",       RequestParserSmoke.ParsePostWithBody));
            list.Add(Make("HTTP: parse multi headers",     RequestParserSmoke.ParseMultipleHeaders));
            list.Add(Make("HTTP: reject empty payload",    RequestParserSmoke.RejectEmpty));

            list.Add(Make("HTTP: chunk framing and trailers", RequestParserSmoke.ChunkFraming));

            // 6. End-to-end protocol flow
            list.Add(Make("Flow: prepare-upload request shape", ProtocolFlowSmoke.PrepareUploadShape));
            list.Add(Make("Flow: prepare-upload response shape", ProtocolFlowSmoke.PrepareUploadResponseShape));
            list.Add(Make("Flow: register payload roundtrip",   ProtocolFlowSmoke.RegisterPayloadRoundtrip));

            return list;
        }

        private static KeyValuePair<string, SmokeCase> Make(string name, SmokeCase test)
        {
            return new KeyValuePair<string, SmokeCase>(name, test);
        }
    }
}
