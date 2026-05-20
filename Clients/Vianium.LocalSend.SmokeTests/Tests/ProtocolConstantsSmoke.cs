// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0
// SPDX-FileCopyrightText: 2026 Angel Careaga <hello@angelcareaga.com>

using System.Threading.Tasks;
using Vianium.LocalSend.Protocol;

namespace Vianium.LocalSend.SmokeTests
{
    /// <summary>
    /// Pins the LocalSend wire constants. If anyone changes a port, an
    /// API prefix, or a multicast address without bumping the protocol
    /// version, every cross-platform peer breaks silently. This smoke
    /// is the canary.
    ///
    /// Source of truth: <see href="https://github.com/localsend/protocol"/>.
    /// </summary>
    internal static class ProtocolConstantsSmoke
    {
        public static Task<SmokeResult> MatchSpec()
        {
            if (ProtocolConstants.Port != 53317)
                return Fail("MatchSpec",
                    "Port must be 53317 per LocalSend v2 spec; got " + ProtocolConstants.Port);

            if (ProtocolConstants.MulticastAddress != "224.0.0.167")
                return Fail("MatchSpec",
                    "MulticastAddress must be 224.0.0.167; got " + ProtocolConstants.MulticastAddress);

            if (ProtocolConstants.ApiPrefix != "/api/localsend/v2")
                return Fail("MatchSpec",
                    "ApiPrefix must be /api/localsend/v2; got " + ProtocolConstants.ApiPrefix);

            if (ProtocolConstants.ApiPrefixV1 != "/api/localsend/v1")
                return Fail("MatchSpec",
                    "ApiPrefixV1 must be /api/localsend/v1; got " + ProtocolConstants.ApiPrefixV1);

            if (string.IsNullOrEmpty(ProtocolConstants.Version) ||
                !ProtocolConstants.Version.StartsWith("2."))
                return Fail("MatchSpec",
                    "Version must report a 2.x string; got '" + ProtocolConstants.Version + "'");

            // DeviceType must be one of the v2 enum values: 'mobile',
            // 'desktop', 'web', 'headless', 'server'. WP8.1 reports 'mobile'.
            string dt = ProtocolConstants.DeviceType;
            if (dt != "mobile" && dt != "desktop" && dt != "web" && dt != "headless" && dt != "server")
                return Fail("MatchSpec",
                    "DeviceType must be a v2 enum value; got '" + dt + "'");

            // 'http' (plain) is acceptable for LAN; 'https' is the v2 default
            // when a TLS cert is provisioned. WP8.1 ships plain HTTP.
            string proto = ProtocolConstants.Protocol;
            if (proto != "http" && proto != "https")
                return Fail("MatchSpec",
                    "Protocol must be 'http' or 'https'; got '" + proto + "'");

            return Pass("MatchSpec");
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
