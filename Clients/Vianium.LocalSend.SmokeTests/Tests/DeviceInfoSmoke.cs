// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0
// SPDX-FileCopyrightText: 2026 Angel Careaga <hello@angelcareaga.com>

using System.Threading.Tasks;
using Vianium.LocalSend.Json;
using Vianium.LocalSend.Protocol;

namespace Vianium.LocalSend.SmokeTests
{
    /// <summary>
    /// LocalSend exchanges three distinct DeviceInfo payload shapes
    /// (multicast announcement, /info response, /register body). Every
    /// shape must roundtrip lossless through the JSON encoder/decoder.
    /// We construct the DTO directly (no CreateLocal()) so the tests
    /// stay isolated from the device's ApplicationData store.
    /// </summary>
    internal static class DeviceInfoSmoke
    {
        public static Task<SmokeResult> MulticastRoundtrip()
        {
            var src = MakeSample();

            JsonObject encoded = src.ToMulticastJson();
            // Multicast payload MUST carry the 'announce' flag — that's
            // what peers use to distinguish announce vs response packets.
            if (!encoded.ContainsKey("announce"))
                return Fail("MulticastRoundtrip", "multicast payload missing 'announce'");

            string wire = encoded.ToString();
            JsonValue parsed = SimpleJsonParser.Parse(wire);
            DeviceInfo back = DeviceInfo.FromJson(parsed);

            return Verify("MulticastRoundtrip", src, back, requireAnnounce: true);
        }

        public static Task<SmokeResult> InfoRoundtrip()
        {
            var src = MakeSample();

            JsonObject encoded = src.ToInfoJson();
            // /info response has no port or announce — peers reach back
            // over the TCP connection that just hit /info.
            if (encoded.ContainsKey("port"))
                return Fail("InfoRoundtrip", "/info payload must not include 'port'");
            if (encoded.ContainsKey("announce"))
                return Fail("InfoRoundtrip", "/info payload must not include 'announce'");

            string wire = encoded.ToString();
            JsonValue parsed = SimpleJsonParser.Parse(wire);
            DeviceInfo back = DeviceInfo.FromJson(parsed);

            // Verify on the field set /info actually carries
            if (back.Alias != src.Alias) return Fail("InfoRoundtrip", "alias lost");
            if (back.Version != src.Version) return Fail("InfoRoundtrip", "version lost");
            if (back.DeviceModel != src.DeviceModel) return Fail("InfoRoundtrip", "deviceModel lost");
            if (back.DeviceType != src.DeviceType) return Fail("InfoRoundtrip", "deviceType lost");
            if (back.Fingerprint != src.Fingerprint) return Fail("InfoRoundtrip", "fingerprint lost");
            return Pass("InfoRoundtrip");
        }

        public static Task<SmokeResult> RegisterRoundtrip()
        {
            var src = MakeSample();

            JsonObject encoded = src.ToRegisterJson();
            if (!encoded.ContainsKey("port"))
                return Fail("RegisterRoundtrip", "register payload missing 'port'");

            string wire = encoded.ToString();
            JsonValue parsed = SimpleJsonParser.Parse(wire);
            DeviceInfo back = DeviceInfo.FromJson(parsed);

            return Verify("RegisterRoundtrip", src, back, requireAnnounce: false);
        }

        public static Task<SmokeResult> HandlesMissingFields()
        {
            // Minimal payload — only what a v1 peer guarantees. FromJson
            // must fall back to spec defaults for everything else.
            JsonValue parsed = SimpleJsonParser.Parse(
                "{\"alias\":\"unnamed\",\"fingerprint\":\"deadbeef\"}");
            DeviceInfo info = DeviceInfo.FromJson(parsed);

            if (info == null) return Fail("HandlesMissingFields", "FromJson returned null on partial payload");
            if (info.Alias != "unnamed") return Fail("HandlesMissingFields", "alias not preserved");
            if (info.Fingerprint != "deadbeef") return Fail("HandlesMissingFields", "fingerprint not preserved");
            if (info.Port != ProtocolConstants.Port)
                return Fail("HandlesMissingFields",
                    "missing 'port' did not fall back to ProtocolConstants.Port; got " + info.Port);
            if (string.IsNullOrEmpty(info.Version))
                return Fail("HandlesMissingFields", "missing 'version' did not get a fallback");
            if (string.IsNullOrEmpty(info.DeviceType))
                return Fail("HandlesMissingFields", "missing 'deviceType' did not get a fallback");
            // Non-null defaults — Announce defaults to false (we are not
            // self-asserting an announce when one wasn't sent).
            if (info.Announce)
                return Fail("HandlesMissingFields", "missing 'announce' should default to false");

            return Pass("HandlesMissingFields");
        }

        private static DeviceInfo MakeSample()
        {
            var src = new DeviceInfo();
            src.Alias = "Windows Phone";
            src.Version = ProtocolConstants.Version;
            src.DeviceModel = "Windows Phone";
            src.DeviceType = "mobile";
            src.Fingerprint = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
            src.Port = ProtocolConstants.Port;
            src.Protocol = "http";
            src.Download = false;
            src.Announce = true;
            return src;
        }

        private static Task<SmokeResult> Verify(string name, DeviceInfo src, DeviceInfo back, bool requireAnnounce)
        {
            if (back == null) return Fail(name, "FromJson returned null");
            if (back.Alias != src.Alias) return Fail(name, "alias lost");
            if (back.Version != src.Version) return Fail(name, "version lost");
            if (back.DeviceModel != src.DeviceModel) return Fail(name, "deviceModel lost");
            if (back.DeviceType != src.DeviceType) return Fail(name, "deviceType lost");
            if (back.Fingerprint != src.Fingerprint) return Fail(name, "fingerprint lost");
            if (back.Port != src.Port) return Fail(name, "port lost: " + back.Port + " != " + src.Port);
            if (back.Protocol != src.Protocol) return Fail(name, "protocol lost");
            if (back.Download != src.Download) return Fail(name, "download lost");
            if (requireAnnounce && back.Announce != src.Announce) return Fail(name, "announce lost");
            return Pass(name);
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
