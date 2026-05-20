// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0
// SPDX-FileCopyrightText: 2026 Angel Careaga <hello@angelcareaga.com>

namespace Vianium.LocalSend.Protocol
{
    internal static class ProtocolConstants
    {
        public const int Port = 53317;
        public const string MulticastAddress = "224.0.0.167";
        public const string ApiPrefix = "/api/localsend/v2";
        public const string ApiPrefixV1 = "/api/localsend/v1";
        public const string Version = "2.1";
        public const string DeviceType = "mobile";
        public const string Protocol = "http";
    }
}
