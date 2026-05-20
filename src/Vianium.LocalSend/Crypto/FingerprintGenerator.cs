// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0
// SPDX-FileCopyrightText: 2026 Angel Careaga <hello@angelcareaga.com>

using System;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.Core;

namespace Vianium.LocalSend.Crypto
{
    internal static class FingerprintGenerator
    {
        public static string Generate()
        {
            var randomBytes = CryptographicBuffer.GenerateRandom(32);
            var sha256 = HashAlgorithmProvider.OpenAlgorithm(HashAlgorithmNames.Sha256);
            var hash = sha256.HashData(randomBytes);
            return CryptographicBuffer.EncodeToHexString(hash).ToLowerInvariant();
        }
    }
}
